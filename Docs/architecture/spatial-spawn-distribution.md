# Spatial Spawn Distribution

수정일: 2026-11-08
상태: 기존 공간 분산 정책과 #540 상황별 편성 연결 참고. 적용 대상은 Battlefield snapshot을 사용하는 Encounter 요청이다.

[Enemy 시스템](enemy-system-guide.md) · [Encounter 편성·예산](encounter-planning-guide.md)

## #540에서 추가된 단계

고정 편성은 지정된 세트를 아래 공간 정책으로 분산한다. 상황별 선택은 Runtime이 스폰 지점별 최근접 유효 Turret을 고른 뒤, **그 타깃 셀 주변**의 병력/Player/공격 유형을 보고 지역 예산과 편성을 선택한다. 출현 셀의 현재·예정 병력은 아래 분산/수용량 검사에 사용한다.

타깃 셀별 예산 그룹과 출현 셀별 공간 공유는 서로 다르다. 같은 타깃 셀을 향하는 여러 지점은 지역 예산을 공유하고, 같은 출현 셀의 여러 지점은 수용량·서비스 부하를 공유한다. 조건·비용·가중치·Static/Dynamic 예산은 [Encounter 가이드](encounter-planning-guide.md)를 기준으로 읽는다.

## 목표와 경계

스포너 개수가 아니라 **적합 Grid 셀의 공간 분포**를 기준으로 생성 기회를 분산한다. 같은 셀에 지점을 복제해도 그 셀이 배수의 생성 비중을 얻지 않는다. 활성 적 밀도가 낮은 셀을 선호하지만, 적이 스폰 셀을 떠났다는 이유로 매번 같은 가까운 지점만 선택하지 않는다.

Core는 Grid의 원시 점유와 전선 기하만 계산한다. 생성 정책과 이력은 Enemy/Encounter가 소유하고, Editor는 그 결과만 관측한다. Influence Map·전술적 우세·최종 난이도 Director는 이번 변경에 포함하지 않는다.

## 흐름과 수명

```mermaid
flowchart LR
  Core[현재 Match Battlefield snapshot] --> Planner[SpawnCompositionPlanner]
  Registry[같은 Scene의 SpawnPointRegistry] --> Planner
  Planner --> Policy[SpawnPointSpatialPolicy]
  History[Match별 SpawnDistributionHistory] --> Policy
  Policy --> Plan[셀 선택 → 셀 내부 지점 선택 → 완전한 분대 세트]
  Plan --> Executor[EnemySpawnExecutor / NGO Server]
  Executor --> Actual[실제 생성된 객체]
  Actual -->|RecordExecuted| History
  Policy --> Diagnostics[불변 판단 DTO → Game Debugger]
  Actual --> Diagnostics
```

- `EnemyNetworkTestLauncher`의 Match Bind/Unbind가 `EncounterRuntime`을 생성/해제한다.
- Runtime의 Planner 한 개가 Policy와 History를 소유한다. 정적 서비스나 Editor 기록에 의존하지 않는다.
- 다른 Match ID 또는 XY Grid 설정 변경(중심·폭·높이·셀 수)이면 이력을 초기화한다. 표시용 Z 변경, revision/전선 모양 변경만으로 이력을 지우지 않는다.
- 제거·비활성·공간 제외된 후보는 이력에서 정리한다. 새로 추가/재활성된 셀은 현재 적합 셀의 최소 service부터 시작한다. 같은 셀의 신규 지점도 해당 셀의 기존 지점 최소 service부터 시작한다. 과거 미참여 기간을 보상하는 catch-up 스폰은 하지 않는다.

## 배정 규칙

1. 생성 불가, Grid 밖, Player/Turret 점유 셀, 중복/누락 ID를 제외한다. 반지름 전체가 아니라 지점 중심의 셀 판정이라는 기존 계약은 유지한다.
2. 적합 후보를 Grid cell ID로 묶는다. 같은 셀의 현재 Enemy 수와 이번 요청 예정량은 **공유**한다.
3. 셀 내부 대표 후보: `point service + 이 요청의 지점 예정량`이 가장 작은 지점, 동률이면 전선 최단 거리 → ordinal ID. 같은 셀 안에서도 특정 지점만 반복 사용하지 않는다.
4. 셀 비교: `cell service + 현재 Enemy 수 + 이번 요청의 셀 예정량`이 작은 셀. 동률이면 현재/예정 Enemy 부하가 작은 셀 → 대표 지점의 전선 거리 → ordinal ID.
5. 선택한 셀에 프리셋 한 세트를 배정하고 **셀 공유 예정량**을 증가시킨다. 전체 예산을 초과하거나 프리셋을 부분 분해하지 않는다.
6. Executor 종료 후 실제 생성 객체 수만 service에 더한다. 미실행·실패 계획은 소비하지 않고 부분 성공은 성공한 객체 수만 반영한다. 서버의 공통 Encounter 경로에서 요청당 한 번 commit한다.

`service`는 생성 분산을 위한 **가상 서비스 부하**다. 초기 후보에는 실제 생성 누계와 같지만 신규 후보에는 시작 기준값이 포함된다. 처치/생존 수 또는 난이도 점수가 아니다. 적이 이동하거나 죽어도 이미 사용한 생성 기회가 초기화되지 않는다. 현재 적 수를 함께 비교하므로 정확히 동일한 지점별 수량을 매 순간 강제하지 않는다.

전선 거리는 생성 기회와 밀도가 같을 때 쓰는 보조 기준이다. 가까운 전선의 한 지점을 항상 최우선으로 고르는 정책이 아니다. 전선이 없으면 거리 값 0과 안정적인 ID를 사용하며 전술 위험도처럼 해석하지 않는다.

## 진단과 확인

Statistics → Spawn decisions / distribution → Candidates에서 다음을 확인한다.

| 열 | 뜻 |
| --- | --- |
| Enemies | 요청 당시 셀의 현재 적 수 |
| Cell service | 요청 시작 시 셀의 서비스 부하 |
| Cell pending | 요청 전체 배정 후 해당 셀의 예정 적 수. 셀 내부 모든 후보에서 동일 |
| Final cell score | service + 현재 적 + 최종 예정량. 개별 세트 선택 순간의 점수가 아님 |
| Point service | 요청 시작 시 그 지점의 서비스 부하 |
| Sets / Frontline units / Decision | 이 지점의 세트 수, 전선 거리, 선택/미선택 사유 |

이전 JSON 기록 및 제외 후보는 service 값을 추정하지 않고 N/A로 표시한다. 계획/실제 결과를 함께 비교해야 하며, 후보 service와 최종 예정량을 실제 생성 통계에 더하지 않는다.

동일 조건의 반복 실험에서는 **지점별 합계만 아니라 셀별 합계**를 비교한다. 한 셀에 지점 4개, 다른 셀에 1개라면 셀 합계는 비슷하되 앞의 네 지점은 셀 비중을 나눠 갖는 것이 정상이다. 따라서 모든 지점의 개별 생성 수가 같은 것은 목표가 아니다.

검증 및 회귀 기록: [2026-10-08 수정 결과](../planning/2026-10-08-spawn-distribution-fix.md).
