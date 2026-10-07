# Spawn decisions & distribution

Game Debugger → Statistics에서 **Spawn decisions & distribution**을 선택한다. 시간 그래프와 별개의 요청별 Inspector이며 동일한 실행 기록을 사용한다.

## 무엇을 확인하는가

- 요청: Request ID, 게임 시간, 요청 출처, Squad preset, 계획 수 / 실제 생성 수, Battlefield revision.
- 당시 XY 공간: Grid 설정과 점유 셀의 Player / Turret / Enemy 수, 전선 경계, 후보 지점, 실제 생성 위치.
- 후보 표: 지점 ID, Grid cell, 초기 Enemy 수, 전선 거리, 배정된 완전한 preset 세트 수, 후보 제외/선택 사유.
- 계획/실제 결과 표: 지점별 몬스터 종류와 계획 수, 관측된 실제 생성 수, Executor가 실제로 반환한 거부 사유.
- 실제 객체 표: Network ID, 몬스터 종류, 지점 ID, 스폰 순간 World XY. 이후 이동 좌표가 아니며 현재 Scene 상태로 덮어쓰지 않는다.

요청 목록에서 선택하면 당시 snapshot을 고정한다. **Follow latest**를 켜면 새로운 요청을 따라간다. 표는 가로/세로 스크롤과 가상화된 행을 사용한다. 상세는 Foldout으로 분리하며 설명·기록을 전체 문자열로 반복하지 않는다.

**Monster** 목록으로 종류를 선택하면 계획/실제 객체 표와 맵의 실제 생성 점을 같은 기준으로 필터링한다. 후보 판단 자체는 분대 전체에 대한 것이므로 후보 표는 그대로 유지한다.

## 맵과 Scene 연계

- 주황: 당시 Frontline, 파란 점: 배정된 후보, 회색 점: 미배정 후보, 녹색 점: 실제 생성 위치. 미배정에는 적합 후보의 예산 소진도 포함되므로 제외 여부는 실제 Reason 열로 확인한다.
- 셀 색: Enemy 점유는 붉은색 밀도, Player/Turret만 점유한 셀은 녹색.
- 휠 확대/축소, 드래그/중간 버튼 이동, **Frame snapshot** 초기화, 지점 클릭 ↔ 후보 상세 선택. 4px를 넘는 드래그는 클릭 선택으로 처리하지 않는다.
- 요청 선택·Follow·몬스터 필터·검색·지도 탐색은 영역별로 저장한다. 고정한 요청의 자료가 보존 상한으로 사라지면 unavailable로 표시하며 다른 요청으로 자동 바꾸지 않는다. 다른 요청 선택/Follow latest로 이동한다.
- `Copy request evidence`로 선택한 관측 DTO의 JSON을 복사할 수 있다. 이는 게임 저장 또는 Replay가 아니다.
- 후보 또는 실제 객체 행을 선택한 후 **Select ... in Scene**을 누르면 현재 활성 Match의 객체를 선택하고 Scene View를 맞춘다.
- 다른 Match의 종료/가져오기 기록에서는 Scene 선택을 비활성화한다. 이미 제거된 객체나 중복 지점 ID는 선택 실패를 설명한다.
- 실제 점은 스폰 반지름의 무작위 분산 결과다. 당시 Grid 후보 판정은 지점 중심에서 수행되므로, 반지름 안의 모든 위치를 검사했다고 표시하지 않는다.

## 실제 판단 정책과 관측 경계

지점 수집은 `EnemySpawnExecutor.SpawnPointList` 조회 시 같은 Scene 전체에서 갱신한다. 부모/이름을 Left/Right에 고정하지 않고 런타임 추가·복제·제거를 반영한다. 비활성 지점도 레지스트리에 보관한다. Editor는 빈/복제 ID를 authoring 값에 저장하고, 런타임 복제는 등록 시 객체 수명 동안 고유 ID를 유지한다. 다른 Scene은 포함하지 않는다.

등록 지점 수를 늘려도 전체 요청 예산은 자동 증가하지 않는다. 기존 공간 배정 정책은 유지하며, 사용 가능한 지점을 대상으로:

1. Grid 밖, Player/Turret 점유 셀, 중복/누락 ID를 제외한다.
2. 기존 셀 Enemy 수 + 이번 요청에 이미 배정한 Enemy 수가 가장 적은 후보를 고른다.
3. 동률이면 전선까지의 최단 거리, 다시 동률이면 ordinal 지점 ID 순서로 고른다.
4. 프리셋의 완전한 세트만 배정한다. 잔여 예산은 새로운 부분 세트를 만들지 않는다.

계획 이후 disabled/captured 상태, 카탈로그/프리팹 누락, 분대 등록 실패는 Executor의 별도 거부 사유다. 없는 원인을 Inspector가 추정하지 않는다. 카탈로그/권한/Match 검증에서 공간 정책에 진입하기 전에 거부된 요청은 공간 snapshot이 없을 수 있다.

**EnemyHealth가 서버에서 확정한 사망**은 EnemyDied, 일반 Despawn / 종료 / 추적 해제는 EnemyRemoved다. 두 사건을 처치 하나로 합치지 않는다. 이전 death 미계측 기록은 N/A로 구분한다.

Death 계측 지원은 현재 EnemyHealth 기반 적에 대한 계약이다. EnemyHealth 없이 다른 사망 체계를 사용하는 새 프리팹을 추가한다면 해당 Feature도 실제 사망 사실을 연결해야 한다. 일반 Despawn으로 사망을 추정하거나, 진단 때문에 Health 컴포넌트를 스폰 정책의 강제 필수 조건으로 만들지는 않았다.

## 저장과 상한

- Runtime: 불변 Core Contracts DTO `SpawnDecisionSnapshot`; Unity 오브젝트 참조나 게임 명령을 소유하지 않는다.
- 런타임 이벤트 큐도 무거운 공간 payload는 최신 32개만 유지한다. 오래된 payload를 제거해도 요청 수량·종류·시간 이벤트는 유지한다.
- Editor: 독립 deep-copy `GameDebugSpawnDecision`; 요청 완료 이벤트와 함께 Recorder가 수집한다.
- 실행 기록당 최신 **32개 요청**. 이전 요청은 `DroppedSpawnDecisions`로 표시한다.
- 요청별 최대 후보 256, plan 512, actual 512, 점유 셀 / 전선 각각 1024. 넘은 수는 `DroppedItems`로 표시한다.
- 큰 Grid의 표시 선만 최대 약 128분할 간격으로 샘플링한다. 실제 정책의 Grid cell과 값은 변경하지 않는다.
- JSON 비교 기록에 포함하며 크기 / 유한 좌표 / 수량 / 컬렉션 상한을 검증한다. 기존 schema 1은 빈 snapshot 목록으로 읽는다.
- 상한 때문에 actual 상세가 잘린 경우 표의 actual은 **captured actual**이며 전체 요청의 Spawned 합계를 대체하지 않는다.

## 설계 참고

Unity CPU Profiler의 선택한 표본 → 상세 패널 / 연관 객체 조회 구조와, Unreal Insights의 선택·필터·사건으로 이동하는 구조를 참고했다. 엔진의 성능 측정 데이터를 흉내 내는 것이 아니라 실제 Feature 관측 사실에 같은 탐색 방식을 적용한다.

- [Unity CPU Usage Profiler](https://docs.unity3d.com/6000.0/Documentation/Manual/ProfilerCPU.html)
- [Unreal Timing Insights](https://dev.epicgames.com/documentation/en-us/unreal-engine/timing-insights-in-unreal-engine)

## 코드 읽는 순서

1. `SpawnPointSpatialPolicy.Allocate(..., out observations)`: 실제 후보 판단과 배정 관측.
2. `SpawnCompositionPlanner.LastCandidates`: disabled/누락 ID 및 공간 정책 관측 결합.
3. `EncounterRuntime.TrySpawn`: 계획 → Executor 실제 위치 → 불변 snapshot.
4. `EnemyNetworkTestLauncher.Emit` → `GameDebugRecorder.Accept`: 완료 이벤트와 Editor 보존.
5. `GameDebugSpawnInspector.SetRun / SelectRequest`: 요청 선택, 표/맵/Scene 연결.

검증: 신규 EditMode 테스트는 보존 상한, replay 중복 제거, 독립 복사, JSON round-trip / invalid 좌표, 이전 기록, Inspector 요청 선택을 대상으로 한다. 실제 생성·사망은 NGO Scene 통합 검증에서 확인한다.
