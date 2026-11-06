> 보관 문서 · 2026-11-05 교체 전 스냅샷
> 기준 커밋: `1a03e9a9164e5db94cbc8775162467d08cf63a3b`. 당시 설명·검증 근거이며 현행 실행 지시가 아니다.

# Game Debugger 확장 배치 계획

기준일: 2026-10-08 · 상태: 구현·CLI 검증 / 시각 인수 별도
조사 기준: 현재 브랜치 `06fd1090`, 로컬 main `e493b46a`.

[문서 목차](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/README.md) · [기존 2차 설계](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/planning/2026-10-08-stage1-phase2-integration-and-visual-diagnostics.md) · [Core README](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Assets/PoC/Core/README.md)

현행 구현/검증은 [배치 결과](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/planning/2026-10-08-game-debugger-batch-results.md)와 [사용/API 가이드](https://github.com/Team-HJD-21/unity-6-the-developer/blob/1a03e9a9164e5db94cbc8775162467d08cf63a3b/Docs/tooling/game-debugger-guide.md)를 따른다. 아래 구현 전 스폰 조사 기록은 문제와 변경 이유의 근거이며 현재 코드 경로가 아니다.

## 목적과 경계

기존 Battlefield Debug를 Game Debugger의 한 모듈로 확장한다. 시간 제어, 현재 상태 대시보드, 연결 검사, 시간 구간별 생성량/생존 수, 터렛 가동 상태/전선 변화, 조건부 정지, 타임라인, 캡처/CSV와 실행 기록 비교를 모두 배치에 포함한다. Window 하나에 정책과 수집을 몰아넣지 않는다.

Editor는 조회·집계·표시와 명시적 테스트 명령을 맡는다. Runtime Feature가 스폰/권한/피해/종료의 사실을 발행한다. 기록되지 않는 값은 N/A이며 로그 문자열 파싱·현재 생존 수 차분으로 실제 생성량을 추측하지 않는다. Core Domain에 Enemy 정책 또는 UnityEditor 의존성을 추가하지 않는다.

기록 비교는 A/B 관측 비교이며 결정론적 리플레이/재현 보장이 아니다. 동일 seed라도 현재 Executor의 Unity Random 및 NGO/프레임 의존성이 있다. 시간 제어는 우선 로컬 Editor/Host 시험으로 한정하며 외부 Client의 정지·배속까지 보장하지 않는다.

## 구현 전 스폰 진입점: 기준 HEAD 조사 기록

| 진입점 | 호출 경로 | snapshot·조건·요청값 |
| --- | --- | --- |
| Battlefield Debug의 Request | `BattlefieldDebugWindow.DrawEncounterControls → IEncounterDebugCommandTarget.TryRequestEncounter → EnemyNetworkTestLauncher.TryRequestEncounter → EncounterRuntime.TrySpawn` | AppRoot 현재 snapshot 전달. Editor 입력 preset/count. 필요 시 Host 시작하지만 Executor 준비까지 대기하지는 않음 |
| Game View의 Enemy Network Test / Plan & Spawn | `EnemyNetworkTestLauncher.DrawConnectionStatus → bound snapshot provider → TryRequestEncounter → TrySpawn` | 같은 공간 정책. Server에서만 버튼 표시. Scene 직렬화 `initialSquadOrder / initialEnemyLimit` 사용 |
| Scene의 SpawnerTest 충돌 | `SpawnerTester.OnCollisionEnter2D → 별도 EncounterRuntime.Spawn → planner.TryPlan(무 snapshot) → Executor.Execute` | Player layer 충돌 진입. Normal/10 기본 요청. Host를 시작하지 않으며 Executor의 서버 조건에 의존. Grid bounds/아군 점유/Enemy 밀도/Frontline 배분을 적용하지 않음 |
| Match 시작 뒤 자동 첫 요청 | `InitialStageCompositionRoot.Compose → BindMatch → RunInitialEncounter → TrySpawn` | Scene 설정으로 실시간 지연(기본 0.5초), 필요 시 Host 시작, Executor 준비를 최대 10초 기다린 뒤 bound Match의 최신 snapshot 사용 |

Scene YAML에서 `SpawnerTest` 오브젝트와 `SpawnerTester` 컴포넌트가 활성인 것은 확인했다. 실제 충돌 콜백 발생은 Collider/Rigidbody/Layer 설정과 Play Mode 검사에 달려 있다. SpawnPoint 자체는 위치·반경·생성 가능 상태만 제공하며 자동 Spawn 요청 주체가 아니다.

세 수동 경로 외에 자동 첫 요청이 있으므로 총 네 진입점이다. Editor/Game UI의 성공 요청은 예약된 첫 자동 요청을 취소하지만 이미 완료된 자동 요청을 되돌리지 않는다. 이후 버튼은 추가 생성 요청이다. Scene 충돌 경로는 자동 예약 취소나 요청 ID 중복 방지와 연결되지 않아 충돌 재진입 때도 요청할 수 있다.

게임 UI의 Start Host는 Host 시작이지 직접 Spawn 명령이 아니다. 바인딩된 자동 요청이 있으면 별도 코루틴에서 이어질 수 있다. CurrentMatch가 없는 상태에서는 Game UI의 Plan & Spawn은 거부되지만 Scene Spawn은 Match를 검사하지 않는다.

확정 목표는 Editor/UI/Scene/자동 트리거가 공통 Match-scoped 요청 경계로 들어가고, 요청 원인(DebugWindow/RuntimeUI/SceneCollision/MatchStart)을 기록하는 것이다. Scene 충돌 트리거는 유지하되 현재 무 snapshot `Spawn()` 호출을 최신 Battlefield snapshot을 사용하는 공통 요청 경계로 바꾼다. 김진태와 조수빈의 연결 계약은 확인하되 충돌 트리거 자체를 삭제하거나 Enemy 정책을 임의로 교체하지 않는다.

## 확정한 실행 정책

- **Scene 충돌 스폰은 유지하고 Battlefield 기반 공통 경로로 통합한다.** Match/snapshot이 없거나 서버 Executor가 준비되지 않았다면 공간 검사를 우회해 생성하지 않고 요청 결과에 사유를 기록한다.
- **자동 진행 / 수동 시험 모드는 만들지 않는다.** 자동 첫 요청, Game UI 버튼, Debug Window 버튼과 Scene 충돌 요청을 함께 유지한다. 디버깅 창을 열었다고 자동 첫 요청을 끄지 않는다.
- 자동 요청이 완료된 뒤 새로운 버튼/충돌 요청은 추가 생성 요청으로 취급한다. 요청별 source/식별자/발생 시각/사용 snapshot revision과 실제 결과를 구분한다. 서로 다른 추가 요청을 중복으로 간주해 막지 않는다.
- 아직 예약된 자동 첫 요청을 수동 성공이 취소하는 기존 동작은 이번 결정만으로 바꾸지 않는다. 공통 경계로 연결한 Scene 요청에도 같은 일관된 동작을 적용한다. 모드 분리와 요청의 중복 실행 방지는 별개이며, 후자는 실제 요청 식별자 계약에 따라 다룬다.

## 전체 배치: G0~G8, 9개

| 배치 | 구현·커밋 단위 | 완료 기준·의존성 |
| --- | --- | --- |
| G0 — 계약/기준 고정 | 위 확정 정책에 따라 Scene 요청 통합 경계를 지정, 시간 기준·요청 원인·MatchId/revision·생성/제거/실패 진단 DTO 및 adapter 경계, 데이터가 없는 항목 명시 | 양현석/조수빈/김진태 연결 합의. 실제 Scene 터렛 baseline은 배치 G8과 연결. 자동/수동 모드를 추가하거나 기존 자동 요청을 끄지 않음 |
| G1 — 상위 창과 디자인 | Game Debugger UI Toolkit shell, 상단 공통 상태/시간 toolbar, 좌측 탐색, 중앙 단일/좌우·상하 2분할/4분할, 하단 경고. 영역별 기능·기록·필터·스크롤 보존. Battlefield 기능은 내부 패널, Scene Overlay와 기존 명령 유지. USS/차트 view는 Editor 영역에 분리 | 단일 Game Debugger 메뉴, 각 분할의 독립 설정·경계선 크기 조절, 좁은 dock/넓은 창, domain reload, Capture와 LIVE/PREVIEW 구분 |
| G2 — 시간·실행 제어 | 0.25/0.5/1/2/4배·직접 입력, Editor Pause/Resume/Step, 게임/실제 경과시간, fixed step 표시, 변경한 설정 복원. Editor 수명과 분리한 제어 scope | 종료/reload 후 누수 없음. 멀티 Client 위험 경고. timeScale=0을 전체 정지로 표시하지 않음. fixedDeltaTime 자동 변경은 기본 Off |
| G3 — 현재 상태·연결 검사 | Match/authority 연결 상태, Grid/Topology/Frontline, 터렛 상태 표·Scene 선택, 전력/가동/파괴, snapshot 나이. 비공선 부족/중복 SpawnPoint ID/범위 밖 입력/소비자 누락/타깃 미연결 검사 | 현재 snapshot/Feature 조회를 읽기 전용으로 사용. 권한/타깃 등 추가 계약 없으면 Unknown. 정책 재계산으로 후보 결과를 꾸미지 않음 |
| G4 — 관측 수집·요청 연결 | 조수빈 Executor의 실제 생성 성공/제거/실패 기록 수신, Match-scoped recorder, 요청 원인/계획과 결과 상관 ID. 모든 진입점의 telemetry coverage 표. 조수빈과 김진태의 Scene 요청을 공통 snapshot 경계로 연결 | 생성/제거 동시 발생에도 정확한 수, Client 중복 집계 금지. Despawn과 처치 구분. 원시 이벤트 상한·완료 실행 기록/활성 snapshot 분리 |
| G5 — 통계·차트 | 기본 게임시간 5초 bin(변경 가능)의 생성량 막대, 생존 수/누적 생성 선, preset/종류/지점 필터, 계획/실제/실패 비교, 터렛 가동 비율·전력·영역 면적/외곽 길이 추이 | 배속과 실제/게임 시간 단위 명시. 생존 수는 표본, 생성 수는 이벤트. 데이터 누락/중간 기록 시작을 0으로 오인하지 않음. 압력/우세 지표는 만들지 않음 |
| G6 — 타임라인·조건부 정지 | Match/요청/Spawn/제거/터렛 파괴·복구/전선 갱신 이력, 필터·북마크·Scene 강조. 생성 실패/터렛 파괴/OOB 조건별 정지·재무장 | 로컬 Editor pause만 실행. 같은 조건 매 프레임 반복 정지 방지. 외부 네트워크 전체가 멈췄다고 표시하지 않음 |
| G7 — 캡처·보고·비교 | 읽기 전용 Capture layout, CSV/JSON 기록 내보내기, Commit/Unity/Scene/seed/config/time policy 메타데이터, 두 실행 A/B 차트 비교 | 종료한 기록은 별도 read-only archive로 표시. Match 상태를 되살리지 않음. 값 변경 명령과 조회 분리, Export는 명시적 경로 선택. 자동 기록 파일을 Git에 섞지 않음 |
| G8 — 통합 검증·사용 문서 | Unity CLI 컴파일/핵심 집계·clock scope 테스트, 실제 터렛 Initial_Stage 테스트, 네 진입점/Host 준비/빈 snapshot/종료·재진입, 시각 QA와 짧은 공유 영상 | 테스트 성공/조건부/미실행 구분. 새 창/차트/CSV 사용법·Owner API 문서 최신화. 동일 Build 전투 완료와 도구 완료를 분리 |

의존성: G0 → G1 → G2/G3, G0 계약 → G4 → G5/G6 → G7, 모두 → G8. 테스트는 각 배치 전체 suite 반복 대신 작은 순수 테스트와 최종 통합 1회 중심으로 수행한다. 기존 컴파일 실패나 테스트 실패는 먼저 수정한다.

## 데이터와 수명 설계

- Editor shell과 기능 패널, 표시 설정, command adapter, telemetry recorder/aggregator, 차트 renderer를 각각 분리한다. 일반 전역 Service Locator나 두 번째 게임 Core를 만들지 않는다.
- 수치 표본은 기본 0.25초 실제 시간 간격으로 읽되, 실제 생성/제거는 이벤트로 수집한다. high timescale에서 놓친 생성 수를 표본 차분으로 보정하지 않는다.
- 이벤트에는 MatchId, 요청/계획 식별자(공급되는 경우), 발생 게임/실제 경과시간, Feature entity/type/location, 결과를 담는다. snapshot revision은 계획/요청에 사용한 버전이며 실제 생성 시점과 동일하다고 추측하지 않는다.
- 집계 빈도와 UI repaint 빈도를 분리한다. 막대/선 chart는 데이터나 viewport 변경 때만 그린다. 원시 이벤트/표본/실행 개수 제한과 overflow/기록 손실 상태를 표시한다.
- 과열은 topology 제거가 아니다. 가동 비율과 기하 영역 추이를 별개로 비교한다. 적 밀도/면적 값도 전술 우세로 명명하지 않는다.
- 완료 기록은 활성 Match와 분리한다. 새로운 Match의 live 값과 이전 Match의 archive 값은 배지·식별자로 구분하고 무조건 섞지 않는다.

## 포함되지만 Feature 계약이 필요한 사항

조수빈: 실제 spawn/despawn/failure 및 request/plan 진단·Match 수명, 서버 권한. 김진태·조수빈: Scene 충돌 요청 의도와 생성 지점/지역 규칙. 황재동: 터렛 snapshot/전력·작동 이벤트. 이영빈/조수빈: 타깃 등록/피해/처치 구분. 양현석은 공용 계약·Editor·집계를 소유하되 각 Feature 정책은 Owner가 유지한다.

실제 DPS/처치율/피해 누적처럼 아직 사실 이벤트가 없는 지표는 계약 전에는 N/A다. Unity Step은 렌더 프레임 진행이지 결정론적 Match tick 진행이 아니다. 자동 재실행/대규모 배치 시뮬레이션·완전 리플레이는 이번 9개 배치에 포함하지 않는다.

## 현재 구현의 상태

### 실사용 모니터 확장 (후속 요구 반영)

초기 막대 차트·원시 텍스트 중심 UI는 아래 범위로 대체한다. 기존 G0~G8은 초기 계획 기록이고 현재 수락 범위는 이 후속 항목을 포함한다.

1. 요청/생성/확정 사망의 공통 시간축 선 그래프, 축 단위, 최신 추적·가시 구간 Auto Y·양축 탐색·독립 큰 Statistics 창.
2. 실시간 카드와 스크롤 표·선택 상세, 설정/설명 foldout과 native Editor 스타일.
3. 실제 정책/실행 경로에서 얻은 요청 당시 후보·제외 사유·배정·계획/결과·생성 XY의 bounded snapshot 및 Statistics 지도/Inspector. 추정 점수를 만들지 않는다.
4. 동일 요청 계약을 간격/횟수로 반복하는 Editor 도구. 일시정지·Match 수명·실패·pane 해제 보호.
5. 전선 디자인/Unity·UE 레퍼런스 조사와 현재 기하의 경계 선택·법선·길이·면적 Inspector. 실제 교전/전술 우세와 구분하며 Core 계산 정책을 바꾸지 않는다.
6. Player 타깃 누락으로 인한 이동 정지 HOTFIX는 별도 커밋·명시적 Scene 검사로 관리.

자동 검사는 변경된 순수 모델/창 구성과 실제 Initial_Stage 요청·snapshot·사망 경로만 집중 실행한다. 반복 전체 suite 대신 결과 문서에 검사별 증거를 남긴다. 실제 Scene 클릭·도킹·가독성 QA는 사람이 확인할 항목으로 구분한다.

독립 Battlefield 창은 기능 차이가 없어 후속 정리에서 제거했다. `BattlefieldDebugPanel`은 일반 C# 패널이며 Game Debugger가 Attach/Dispose를 관리한다. 별도 EditorWindow를 숨겨 생성하지 않는다. Scene Overlay도 단일 Game Debugger를 연다. 이 후속 정리의 테스트 상태는 배치 결과의 별도 항목을 따른다.

G0~G7의 도구·관측 기능을 구현했다. Scene 충돌은 이제 `SpawnerTester.TryRequestEncounter → IEncounterDebugCommandTarget.TryRequestCurrentEncounter → bound snapshot → TryRequestEncounter → TrySpawn`을 사용하며 Match ID/권한 준비 조건과 같은 예약 취소 정책을 따른다. DebugWindow/RuntimeUI/SceneCollision/MatchStart를 모두 구조화해 기록한다. 자동/수동 모드는 추가하지 않았다.

G8의 CLI 검증은 통과했다. 실제 물리 충돌·UI 클릭·차트/도킹 시각 QA·영상은 별도 인수이며 전체 전투/승패 완료는 아니다. 아직 없는 PlanId/후보 점수/처치/피해/우세 지표는 N/A다. 현행 관측 계약은 조수빈의 전체 Match Runtime 설계를 대신하지 않는다. GitHub Issue 수정·PR·push는 실행하지 않았다.
