# Stage 1 PoC 통합 준비표

기준일: 2026-10-07 · 수정일: 2026-10-08 · 병합 기준: `main` `a7f57bc9` · 1차 패치: 로컬 `feature/core` 미커밋  
상태: 1차 통합 코드 및 CLI의 명시적 `Initial_Stage` 자동 생성 테스트 통과, 2·3차 팀 통합 계획. **동일 Build의 전체 전투·재진입·지역 해금 테스트 완료 보고가 아니다.**

[문서 목차](../README.md) · [Sprint 기준](SPRINT_1_STAGE_1_POC.md) · [제품 범위](../product/PROJECT_PLAN.md) · [10월 3일 후속 검토](2026-10-03-poc-followup.md) · [10월 5일 스폰 논의](2026-10-05-monster-map-spawn-review.md)

2차 Match/Encounter 연결과 Editor 시각 진단의 세부 제안은 [2차 통합·시각 진단 설계](2026-10-08-stage1-phase2-integration-and-visual-diagnostics.md)를 본다. 이 제안은 아래 1~3차 경계와 Issue 담당을 변경하지 않는다.

## 1. 이번 통합에서 무엇을 판정하는가

같은 Stage 1 실행 환경에서 Player 이동·공격, 미리 배치된 Turret의 전력 제약·전투, Enemy 생성·목표 선택·전투, Battlefield의 점유 topology·전선 표시를 이어서 관찰한다. 그 결과로 **전제 1(동적 전선)**과 **전제 2(지역 해금)**의 재미·이해도 질문 및 아직 시험하지 못한 기술 위험을 기록한다. 어느 후보를 먼저 또는 함께 구현·시험할지는 미정이므로, 이 표가 병행 구현을 새로 승인하지 않는다.

정식 승리·패배 판정, 결과·성장·다음 Mission은 현행 [Sprint](SPRINT_1_STAGE_1_POC.md)와 [Product Plan](../product/PROJECT_PLAN.md)에서 PoC 완료 조건이 아니다. 현재 [#420](https://github.com/Team-HJD-21/unity-6-the-developer/issues/420)에는 과거의 `임시 승패`, `거점 확보·상실`, `10월 2일 Build` 완료 문구가 남아 있어 현행 기준과 충돌한다. #420을 닫기 전에 Owner가 Issue 본문·체크리스트를 현행 결정에 맞춰 조정하거나, 해당 항목을 미완료/후속 범위로 명시해야 한다. **문구 충돌을 이유로 실측 결과를 통과 처리하지 않는다.**

## 2. 현재 연결 상태: 코드/PR 근거와 미검증 분리

| 경계 | 병합 코드와 로컬 1차 패치의 상태 | 같은 Build에서 남은 확인/구현 | 주 협의자 |
| --- | --- | --- | --- |
| App → Match → Player | [`InitialStageCompositionRoot`](../../Assets/PoC/Core/Bootstrap/SceneComposition/InitialStageCompositionRoot.cs)가 `PlayerStart`, 프리팹, 카메라와 Match를 구성하고 Scene 파괴 시 정리한다. | 씬 재진입·종료와 실제 Player 이동/공격을 이번 기준 Build에서 재확인. | A·B |
| Turret → Battlefield | [`TurretBattlefieldInputSource`](../../Assets/Scripts/Tower/Turret/Core/TurretBattlefieldInputSource.cs)가 활성 컴포넌트의 배치 위치를 공간 입력으로 변환하고, Core가 topology·frontline을 만든다. Play Mode에서는 파괴된 터렛만 제외하며 비활성/과열로 사격이 멈춘 터렛의 배치 점은 유지한다. [#426](https://github.com/Team-HJD-21/unity-6-the-developer/issues/426)은 닫혔다. | 현재 `Initial_Stage`에 **파괴되지 않은 활성 컴포넌트의 비공선 터렛 3개 이상**을 놓고 삼각형·외곽선 및 전원/과열/파괴·복구 시 변화를 관찰. [#443](https://github.com/Team-HJD-21/unity-6-the-developer/issues/443) 열린 상태. | A·D·C |
| Match → Grid | 같은 Scene 조립자가 Player·Enemy 위치를 0.1초 간격으로 샘플링하고 **셀 점유가 바뀔 때** 동적 입력을 갱신한다. Editor가 Grid/점유를 표시한다. | 경계·씬 좌표·셀 점유와 여러 객체/씬 재시작 결과 확인. [#458](https://github.com/Team-HJD-21/unity-6-the-developer/issues/458) 열린 상태. | A·E |
| Battlefield → Encounter → Spawn | `main`에서는 [#473](https://github.com/Team-HJD-21/unity-6-the-developer/pull/473)의 수동 요청·실제 Slime 생성까지 관찰됐다. **로컬 1차 패치**는 [`IEncounterMatchConsumer`](../../Assets/PoC/Core/Contracts/IEncounterMatchConsumer.cs)로 Match의 최신 snapshot을 전달하고, [`SpawnPointSpatialPolicy`](../../Assets/PoC/Enemy/Scripts/Spawning/SpawnPointSpatialPolicy.cs)로 Grid 점유·기존 적 수·전선 거리를 배분에 사용한다. [`EnemyNetworkTestLauncher`](../../Assets/PoC/Enemy/Scripts/Testing/EnemyNetworkTestLauncher.cs)가 Match 시작 후 첫 Encounter를 자동 요청한다. CLI의 명시적 Scene 테스트에서 Host와 Slime 10마리/2 plan을 확인했다. | Scene에 터렛이 없어 전선 기반 실지점 선택은 미검증이다. Match 소유 Encounter, 별도 plan ID·중복 실행 방지, 반복 주기/난이도 정책은 [#459](https://github.com/Team-HJD-21/unity-6-the-developer/issues/459)의 후속 범위다. 1차 패치만으로 #459를 닫지 않는다. | A 생산자 / E 소비자 |
| Enemy ↔ Player/Turret 전투 | [#475](https://github.com/Team-HJD-21/unity-6-the-developer/pull/475) Enemy 공격·체력, [#479](https://github.com/Team-HJD-21/unity-6-the-developer/pull/479) Player 탄환/폭탄의 `EnemyHealth` 피해, [#476](https://github.com/Team-HJD-21/unity-6-the-developer/pull/476) Turret 전력 제어·목표 예약·피해 코드가 병합됐다. | #476 PR 자체가 추가 Play Mode/Windows Build 검증을 완료했다고 주장하지 않는다. Host에서 공격→피해→죽음/Despawn, 전력 부족·과열·냉각 및 타깃 재선택을 한 Build에서 재현. Turret 네트워크 동기화 완성으로 해석하지 않는다. | B·D·E |
| Region/진행 방식 | Product/Sprint에 재미 검증 후보로 기록됐다. | 해당 Build의 지역 해금/거점 전이가 실제 구현됐는지는 이번 조사에서 확인되지 않았다. Owner가 씬·행동·상태 변화의 증거를 제출하고, 미구현이면 전제 2의 질문/위험만 기록한다. 정식 승패 루프로 대체하지 않는다. | C·A |

Editor 창 [#446](https://github.com/Team-HJD-21/unity-6-the-developer/issues/446)·[#460](https://github.com/Team-HJD-21/unity-6-the-developer/issues/460)은 닫혔다. 이는 **진단 도구의 완료**이지 실제 Scene의 터렛 삼각형 형성이나 Encounter의 Grid 소비 검증을 뜻하지 않는다. [#437](https://github.com/Team-HJD-21/unity-6-the-developer/issues/437) 스폰 기반도 닫혔지만 #459의 전체 수직 경로는 별개다.

## 3. 재현 가능한 단일 Build 검사 순서

기준 Scene: [`Assets/PoC/Spaceship/Scenes/Initial_Stage.unity`](../../Assets/PoC/Spaceship/Scenes/Initial_Stage.unity). 현재 YAML에 `PlayerStart_Local`, `InitialStageCompositionRoot`, `Turret Battlefield Input Source`, `Encounter Debug Spawner`와 두 `Encounter Spawn` 지점, `Encounter Debug Network` 오브젝트 이름이 있는 것은 확인했다. **직렬화 참조의 유효성·씬 실행 성공은 이 이름 검사만으로 증명되지 않는다.** Spawn 지점 개수·레인 규칙도 최종 기획으로 확정하지 않는다.

| 순서 | 실행 및 관찰 | 기록할 증거 | 실패 시 첫 담당 |
| --- | --- | --- | --- |
| 0 | 깨끗한 기준 Commit/Build와 Unity 버전·참가자·Scene을 고정하고 컴파일/Console 기준 상태를 기록한다. | Commit SHA, Build 식별자, Console 오류 수 | A |
| 1 | `Initial_Stage` Play Mode에서 AppRoot/Match/Player가 한 번 생성되고 `MatchId`가 기록되는지 본다. Player 이동·카메라·기본 공격을 확인한다. | Hierarchy/로그/짧은 영상 | A·B |
| 2 | 미리 배치된 **파괴되지 않은 활성 컴포넌트** 터렛 3개 이상을 XY에서 비공선으로 둔 뒤 Debug Window의 vertices/triangles/frontline, Grid 점유, 전력 상태를 본다. 0개면 Scene 배치·등록·`IsDestroyed`·컴포넌트 활성 상태부터 분리 진단한다. | 변경 전/후 `MatchId`·revision·V/T/F·점유 셀·터렛 상태 | A·D |
| 3 | 터렛 전원 변경과 과열→냉각에서는 배치 점/topology가 **유지**되고, 파괴·복구에서는 제거·복귀하는지 관찰한다. 사격 가능 상태와 Battlefield 점유는 별도 개념이다. | 입력 동작, 이벤트 시각, 결과 값·화면 | A·D |
| 4 | 로컬 1차 패치의 첫 Encounter는 **순서 1 직후 자동 실행**되므로 그 로그와 결과를 먼저 확인한다. `Normal`/최대 10은 임시 시험값이다. 이후 수동 `Request Encounter from Current Match Snapshot`으로 점유·전선 변경 뒤 재계획을 별도 시험한다. Host 시작, point ID·planned/spawned 수와 후보 차이를 구분한다. | 자동/수동 요청 로그, Match/revision, Host 상태, 실제 Hierarchy 적 수·지점·실패 이유 | E·A |
| 5 | 적 이동/타깃 선택, Player·Turret 공격과 상호 피해·Enemy Despawn을 관찰한다. 전력 제한 선택이 전투와 전선에 어떤 영향을 주는지 기록한다. | 타깃·피해·Despawn·전력 수치·Console | E·B·D |
| 6 | 전제 1의 전선 변화가 **플레이어에게 읽히고 선택을 바꾸는지** 관찰한다. 지역 해금이 같은 Build에서 구현됐다면 전제 2를 별도 시나리오로 시험하고, 아니면 미시험 사유/필요 계약을 기록한다. | 관찰 메모, 참가자 피드백, 미시험 사유 | A·C·전체 팀 |
| 7 | Scene 이탈/재진입 또는 Match 종료 시 Match-scoped 상태·구독·Enemy/Player 정리를 확인한다. | 전후 Hierarchy·MatchId·Console | A·E |

수동 `Request Encounter`의 과거 성공은 **한 번의 디버그 스폰**만 증명한다. 로컬 1차 패치는 CLI `EnterPlayMode` 테스트에서 디버그 버튼 없는 Host·Slime 10마리 생성을 확인했다. 공간 배분의 Grid/전선 규칙은 순수 테스트 3개로 확인했지만 실제 Scene에는 터렛이 없으므로 전선 기반 지점 선택을 육안으로 확인하지 못했다. 첫 자동 요청의 성공은 반복 요청의 plan 중복 방지, 지역 해금, 공식 한 판 종료 또는 2인 네트워크 동기화까지 증명하지 않는다.

## 4. 팀 간 넘겨야 할 자료와 결정

| 생산자 → 소비자 | 공유할 최소 계약·검증값 | 미정/결정 책임 |
| --- | --- | --- |
| D Turret → A Battlefield | 터렛 ID, XY 위치, 컴포넌트 활성/사격 작동/과열/파괴, 변경 알림 시점과 전력 상태. `TurretSnapshot`은 조회 시점 복사본. | 현재 adapter의 **과열 시 배치 유지·파괴 시 제거** 정책을 실측 확인하고 최종 게임 규칙으로 유지할지 A·D가 결정 |
| A Battlefield → E Encounter | 현재 Match ID/revision, read-only topology·frontline·Grid 점유/조회, 업데이트 시점, out-of-bounds 처리. 로컬 1차 패치는 `Func<BattlefieldSpatialSnapshot>` 조회 경계와 XY 후보 변환을 사용한다. 정책에 Unity Scene 객체를 보관하지 않는다. | 1차 정책의 적절성, 이후 요청 cadence와 계획 ID/중복 처리: E 제안, A 계약 검토 |
| C Map → E Spawn / A 통합 | SpawnPoint ID·XY·레인/구역 대응, 접근 가능한 지역·통로, 스폰할 수 없는 지점. | 세 전선·보스 위치는 10월 5일 *계획*으로만 기록됨. 실제 Scene 참조/좌표/구역 규칙: C·E 확인 |
| B Player / D Turret ↔ E Enemy | Host에서 공격·피격 API, 대상 선택·타깃 해제, 사망/Despawn 뒤 참조 정리. | 실제 Scene에서 타격·권한 결과와 예외: B·D·E 합동 확인 |
| A 통합 → 전체 팀 | 한 기준 Build, 실행 순서, 측정값, 실패 재현법. | 전제 1·2의 시험 순서와 #420 체크리스트 조정: A가 팀 논의 후 기록 |

전선/점유의 시각화와 실제 Spawn 정책을 구별한다. Core는 분석 결과만 제공한다. 로컬 1차 정책은 Grid 밖 또는 Player/Turret와 같은 셀인 후보를 제외하고, 기존 Enemy 밀도가 낮은 후보에 세트를 우선 배분하며 동률이면 전선까지 거리·지점 ID를 사용한다. 전선 우세나 최종 난이도 밸런스를 확정하지 않는다.

## 5. 세 차례 통합의 큰 경계

| 단계 | 목표·이번 범위 | 다음 단계로 넘기는 것 |
| --- | --- | --- |
| **1차 — 공간 입력과 자동 첫 Encounter** | Core의 최신 Match snapshot → Enemy의 순수 공간 선택 정책 → 기존 Squad Preset/Executor/NGO Spawn. Scene Match 시작 후 지연된 첫 요청을 자동 실행하며, 디버그 버튼도 동일 정책으로 재시험한다. 점유 셀 회피·기존 적 밀도·전선 거리의 간단한 결정론적 규칙만 사용한다. | Unity 컴파일/Play Mode 및 실제 지점별 선택 증거. 정책 수치·전선 우세 공식 확정은 하지 않는다. |
| **2차 — 팀 합의와 Encounter 수명/진행 연동** | A·E는 Match 소유 Encounter의 생성·종료, 이벤트/주기 요청, SpawnPlan ID·중복/실패 처리, Network 권한을 결정한다. C·E는 지점 ID/레인/구역·지역 해금과 스폰 제한을 합의한다. B·D·E는 상호 피해와 타깃 변경을 같은 씬에서 연결한다. | 실제 Region/전선 변화에서 재계획하는 기준과 시험 시나리오. `Normal/10`을 난이도·전력 상황과 연결할지는 팀 합의 후 결정. |
| **3차 — 동일 Build PoC 통합·판정** | A가 기준 Commit/Build를 고정하고 전체 팀이 3절 시나리오를 실행한다. 전제 1·2의 재미/가독성 결과와 미시험 사유, Console·CI·Scene 재진입, Owner별 Blocker를 기록한다. #420의 오래된 승패 항목을 현행 Sprint에 맞춰 정리한다. | 통과/수정/폐기 및 다음 Milestone의 backlog. 정식 승패·성장·2인 네트워크 완성은 자동으로 승인하지 않는다. |

이 구분은 구현·검증 작업 묶음이며 새로운 제품 규칙이나 담당 재배정을 승인하는 문서가 아니다. 1차 패치는 **로컬 작업 중**이며 자동 생성 경로의 CLI 검증은 통과했다. 코드 리뷰와 같은 Scene의 타깃·전선·전투 검증 전에는 팀 PoC 완료로 표시하지 않는다.

## 6. 실행 기록 양식과 Issue 정리 기준

### 2026-10-08 로컬 1차 경로 검증

Unity 6000.3.23f1의 headless CLI에서 `BattlefieldGridBuilderTests` 7개, `SpawnPointSpatialPolicyTests` 3개, `InitialStageEncounterIntegrationTests` 1개가 통과했다. 마지막 테스트는 저장된 `Initial_Stage`를 열고 Play Mode로 전환하여 디버그 버튼 없이 NGO Host와 실제 Enemy 생성을 검사했다. 실행 로그는 `Match r2`, `2 plan(s)`, `10 spawned`(좌·우 각 5마리)를 기록했다. 컴파일 오류는 없었다.

같은 로그에서 `0 triangles`, `0 frontline edges`, `1 occupied cells` 및 각 분대의 `No targetable Player or Turret found` 경고가 확인됐다. 저장된 Player prefab에는 `TargetableComponent`가 없고 Scene에도 작동 중인 Turret이 없으므로, **스폰은 통과했지만 타깃 선택·전선 기반 실제 Scene 배분·공격/피해 루프는 통과하지 않았다.** B·D·E가 타깃 등록/체력 상태와 터렛 배치를 합의한 뒤 동일 Scene에서 다시 검사한다. 이 경고를 숨기거나 1차 생성 성공을 전체 PoC 완료로 해석하지 않는다.

아래를 **동일 Build 전체 통합 실행 후** [#420](https://github.com/Team-HJD-21/unity-6-the-developer/issues/420) 및 각 담당 Issue/PR에 붙인다. 위의 자동 생성 증거가 없는 나머지 항목은 `미검증`으로 둔다.

```text
실행일/참가자:
Unity 버전 / Commit SHA / Build ID / Scene:
환경: Editor Play Mode 또는 Windows Build, Host/Client 수:
1. App/Match/Player 생성·재진입: 통과/실패/미검증 — 근거:
2. Turret 전력·과열·공격: 통과/실패/미검증 — 근거:
3. Battlefield V/T/F 및 Grid 전후 값: 통과/실패/미검증 — 근거:
4. 자동 첫 Encounter와 수동 재요청: Match/revision, preset, max, point ID, planned/spawned:
5. Enemy 이동·타깃·상호 피해·Despawn: 통과/실패/미검증 — 근거:
6. 전제 1의 재미/가독성 관찰: 사실·플레이어 반응·개선 가설:
7. 전제 2: 시험함/미시험 — 구현 범위·관찰 또는 Blocker:
8. 종료·정리와 Console/CI/Build: 통과/실패/미검증 — 근거:
Blocker: 재현 단계, Owner, 연결 Issue, 우회 여부:
다음 결정: 전제 1·2 시험 순서/병행 범위, 유지·수정·폐기 판단자와 날짜:
```

지금 바로 닫을 수 있는 이슈라고 판단하지 않는다. [#443](https://github.com/Team-HJD-21/unity-6-the-developer/issues/443)과 [#458](https://github.com/Team-HJD-21/unity-6-the-developer/issues/458)은 **구현 주장과 수용 검증**을 대조한 뒤, [#459](https://github.com/Team-HJD-21/unity-6-the-developer/issues/459)는 **Grid를 소비하는 Encounter 정책 및 Match-scope/계획 실행**을 E가 완료한 뒤, #420은 같은 Build의 증거와 현행 기준에 맞춘 완료 조건을 확보한 뒤 각각 처리한다. 닫힌 #426/#437/#446/#460은 다시 열 이유를 새로운 재현 증거로 판단한다.
