# Core 최신 로컬 검토·수동 확인·팀원 전달

기준일: 2026-11-05 / 작업 브랜치 `codex/core-feature-integration-20261105` / 구현 기준 `9dd5022be`.

이 문서가 이번 수동 검토의 시작점입니다. 어제부터 이어온 Core 목표와 후속 낮밤·Steam·우주선·디버그 HUD 변경을 합쳤습니다. 날짜로 커밋을 추정하지 않고 main 기준 `1a03e9a91` 이후 실제 코드/문서를 대조했습니다. 이 구간은 작업 구간 커밋 39개, C# 변경 경로 187개이며 .meta는 별도입니다. **로컬 구현 기준이고 PR/push/merge·사이트 배포는 하지 않았습니다.**

여기서 **남은 Core 작업**은 PL이 직접 구현할 수 있지만 아직 구현하지 않은 작업만 뜻합니다. 구현된 기능의 사람 확인, 다른 팀원 담당, 기획 결정 대기는 따로 적습니다.

## 1. 먼저 실행할 프로젝트와 Scene

- 실제 프로젝트: `D:/Dev/Unity/Unity_3D_CBC`.
- Startup: `Assets/Scenes/Bootstrap/Startup.unity`.
- 필드: `Assets/Scenes/Gameplay/Initial_Stage.unity`.
- 우주선: `Assets/Scenes/Spaceship/SpaceShipGrayBox.unity`.
- Game Debugger: Unity 메뉴 **Tools > TeamHJD > The Developer > Game Debugger**.
- Startup 열기: **Tools > TeamHJD > The Developer > Open Core Startup Scene**.

현재 Play를 끝내고 저장할 Scene 변경을 먼저 보존한 다음 새 Play로 시작합니다. Editor 재시작은 필요하지 않습니다. `Validation/`의 격리 프로젝트/실행 파일은 검증용이며 이 수동 확인의 프로젝트로 열지 않습니다. 공유 기본 설정은 개발 플랫폼입니다. Steam 시험을 선택하지 않아도 일반 Core 확인은 가능합니다.

## 2. PL이 직접 확인할 것 — 7단계

기능이 되는지 처음부터 증명하는 검사는 이미 수행했습니다. 아래는 실제 조작·연출·제작 Scene에서 사람이 승인할 항목입니다. 같은 왕복을 여러 번 반복하거나 5분을 실시간으로 기다릴 필요는 없습니다.

| 순서 | 실행 | 직접 확인할 것 | 이미 자동 확인한 부분 |
| --- | --- | --- | --- |
| 1 | Startup에서 Play → 기존 Start 버튼 | 로고·기존 폰트/버튼 배치. 우주선 입장 때 검은 화면, `임시 로딩 화면`, 하단 바가 의도한 모양인지 | 실제 Start 호출·Scene 진입·로딩 렌더·입력 잠금/해제 |
| 2 | 시작 우주선에서 이동 | 실내가 보이고 카메라 거리/배율이 자연스러운지. 첫 허브는 이동만 가능하며 전투/설치가 없는 구성이 맞는지 | 실제 Player·Camera 생성, 이동 입력, 공격/설치 차단, 시야 배율18·필드 Camera 설정 일치 |
| 3 | 기존 우주선 문으로 첫 출격 | 문 접근/애니메이션·충돌 조작감. 약0.9초 페이드, 로딩 글자/바 없음. 첫 도착이 필드의 우주선 문 밖인지 | 실제 문 물리 접촉→정식 Match·Player 생성, 허브 actor/Camera 정리, 출격 위치와 후속 복귀 위치 일치 |
| 4 | 필드에서 우주선에 들어갔다가 다시 나오기 | 카메라가 먼 거리를 날아오는 장면이 안 보이는지. 우주선 안 이동은 되고 필드/날씨 시간은 멈추며, 나온 뒤 재개되는 느낌. 문 밖으로 복귀해 즉시 재입장하지 않는지 | 같은 Match/actor 유지·양방향 실제 물리 접촉·정지/재개·입력 복구·중복 Camera/자동 재입장 방지. 기능은 자동 검증했고 여기서는 실제 보행 동선을 봄 |
| 5 | Play 종료 → Initial_Stage를 직접 열어 새 Play | 제작 Scene의 기존 Player/HUD·첫 자동 스폰·터렛이 현재 팀원 작업물대로 보이는지. 가까운 터렛에서 ON/OFF·수리·강화의 HUD/거리 판정 조작감 | 직접 Stage 조립·자동 Encounter, 실제 위치/Host 검증·CU 초기화·터렛 시간/전력/수리 회귀. 원격 Player 조작 완료를 뜻하지 않음 |
| 6 | 좌측 하단 날씨 HUD 확인 → Reset 한 번 | 작은 Day/Night, `5:00`, 아래 Reset의 글자 크기/배치. Reset 후 현재 낮밤은 유지하고 시간이 다시5분이 되는지. 기존 예고 큐에 따라 같은 날씨가 연속 나올 수 있음 | 300초 기본값·자연 만료·다음 단계·Reset1회당 날씨 명령1회·이전 예약 취소·정지 중 시간 보존·1280×720 실제 렌더 |
| 7 | 필드에서 Esc 열기/닫기. 필요하면 Debugger 배속/Pause/Restore | 메뉴/기존 HUD의 겹침·조작감. 배속/정지에서 적·터렛·탄환·Player 행동이 눈에 띄게 따로 움직이지 않는지 | Input System Esc, 메뉴 잠금 해제와 독립 사망 잠금 유지, 공용 필드 시계/종료·실제 스킬/투사체 연결. 우주선 페이드는 배속/정지와 무관한 실시간0.9초 |

1~4는 Startup 진입의 연출 인수, 5~7은 Initial_Stage 제작물과 UI 인수입니다. AppRoot 수량/Match ID/예약 수량을 사람이 매번 세는 검사는 다시 요구하지 않습니다. 문제가 있을 때만 Overview의 `APP · GAME FLOW`, Battlefield, Encounter에서 현재 상태와 Console의 첫 오류를 같이 보면 됩니다.

### 선택 확인과 지금 시도하지 않을 것

- 낮→밤 화면 효과를 직접 보고 싶으면 Game Debugger 8×에서 약37.5초로300게임초를 진행할 수 있습니다. 자동 만료는 단축 격리 Scene으로 이미 검증했으며 반드시 기다려야 하는 수동 항목이 아닙니다. 확인 뒤 **Restore/1×**로 돌립니다.
- Normal 프리셋에 감속/원거리 적이 없으면 이를 위해 콘텐츠를 새로 배치할 필요는 없습니다. 감속·발사 전후·정지 중 이벤트/복원은 자동 검사 근거가 있습니다.
- CU가 실제로 파괴된 상황이 생기면 결과 화면의 배치·재시작/허브 귀환 느낌을 볼 수 있습니다. CU 패배의 단일 결과와 작업 종료는 이미 자동 확인했습니다. Boss 승리까지 구현됐다는 뜻은 아닙니다.
- `Overview > CHECKPOINT · HOST`의 `world/weather` 누락, 명시 저장/복원 비활성은 **현재 정상 거절**입니다. 실제 전체 저장→새 Match 복원은 지금 수동으로 성공시킬 수 없습니다. 가짜 writer를 추가해 우회하지 않습니다.
- 실제 Steam 두 계정의 로비/초대 Overlay는 별도 계정과 앱 실행 권한이 있을 때만 추가 인수합니다. SDK1계정 초기화/로비 생성은 이미 확인했습니다. **로비 참가가 NGO 게임 접속은 아닙니다.**

## 3. 지금까지 반영한 로컬 기능

| 묶음 | 들어간 것 | 주로 읽을 코드 |
| --- | --- | --- |
| App/게임 흐름 | Bootstrap 단일 App, Startup 시작, 허브 Player/Camera, 출격·명시 귀환·새 판, 입장 실패/취소 정리 | `AppBootstrap`, `AppRoot`, `GameFlowController`, `AppGameFlowHost`, `SpaceshipHubCompositionRoot` |
| 공용 시간/수명 | 필드 게임 시계·예약·정지/배속·종료. 1인 내부/협동 전원 내부 정책. UI/통신/허브 이동 분리 | `MatchTimeService`, `MatchScheduler`, `MatchPresenceService`, `MatchFieldSceneScope`, `FieldSimulationTime` |
| 관리/종료 | 실제 CU 파괴→단일 패배 결과→게임 작업 중지. 연결/결과 전달은 유지 | `MatchSession`, `MatchCoordinator`, `StoryModeRules` |
| 터렛/CU 연결 | 기존 HUD 요청→Host 실제 actor·거리·생존/상태 검증→기존 Feature API. CU 해금 명시 초기화. 전력 규칙 원본 유지 | `TurretCommandGateway/Service`, `TurretInteractionAuthorization`, `TurretMatchBinding`, `InitialStageCompositionRoot` |
| Player 연결 | 입력·사망·필드 잠금 분리, 공통 피해 계약, 강한 감속 우선/독립 만료, 무기·스킬·장치 시간/저장 | `PlayerControlState`, `PlayerInfo`, `PlayerSlowEffects`, 무기/스킬 `.Checkpoint.cs` |
| Enemy/Encounter 연결 | 현행 Runtime prefab/공간 분산, 정식 요청·환경/origin/예약·source 취소, 실제 탄환/분대/참조, 정지 중 공격 이벤트 지연/중복 방지 | `EnemyNetworkTestLauncher.SpawnContext`, `SpawnCompositionPlanner`, Enemy/Executor `.Simulation/.Checkpoint`, `EnemyProjectileAbility` |
| 안전장치 | 명시 binary 저장/복원·Prepare/Apply/Resolve/rollback, 논리 ID, 실제 Feature writer, 슬롯→새1인 Match 입장 | `CheckpointService`, `BinaryCheckpointStore`, `InitialStageCompositionRoot.Checkpoints`, `AppRoot.ResumeCheckpointAsync` |
| 네트워크 기반 | NGO 고정1~2인 공유 Scene 완료→전원 준비→Host 시작, Client 시간/환경/지역·결과 수신, 반복 판 | `NgoSceneFlow`, `NetworkMatchCoordinator`, `InitialStageNetworkBinding`, `.Networking.cs` |
| 낮밤/Weather 접점 | Day5분/Night5분, 기존 날씨 명령·환경 snapshot/다음 SpawnPlan, 중복 CountTick 예약 방지, 낮밤 writer v2/v1읽기 | `MatchDayNightCycle`, `WeatherManager.Core`, `MatchEnvironmentSceneScope` |
| Steam SDK | AppID4336820, 고정 Steamworks.NET/SDK, 사용자·앱 이용 권한·callback·종료,2인 로비 생성/참가/초대 API | `SteamPlatformService`, `SteamAppSettings`, `AppRoot` 플랫폼 pump |
| 화면/왕복 | 기존 Main/폰트/HUD 자산 재사용, 개인 메뉴/결과/일반 로딩, Host 포털 방문과0.9초 페이드, 카메라18, 출입구 PlayerStart | `AppScreenPresenter`, `ScreenManager`, `SpaceshipTravelService`, `SceneFadePresenter`, `SpaceshipEnter/Exit` |
| 디버그/정리 | Day/Night·타이머·Reset, Debugger 플랫폼/환경/저장 진단. 빈 MatchSimulation 제거, 큰 root의 network/spatial/checkpoint partial 분리 | `WeatherDebugHud`, `GameDebugPlatformPanel`, `GameDebugCheckpointPanel`, Core 구조 검토 문서 |

일반 Random API/RandomBinding은 기반 API를 유지하고 실제 Feature에 연결한 것입니다. Weather의 실제 난수 선택을 대신 교체하지 않았습니다. 같은 HP·전력·날씨 상태를 Core에 복제하는 새 GameManager를 만들지 않았습니다.

## 4. 자동 검증을 어디까지 믿을 수 있는가

| 근거 | 확인한 범위 / 확대해석하면 안 되는 것 |
| --- | --- |
| 최신 `weather-hud-final.xml` **11/11**, 종료0 | HUD·자연 만료/Reset·낮밤/예약·v1/v2 복원, 실제 Startup/문 물리 왕복, 카메라/도착 위치,0.9초 실시간 페이드/실패 복구. 전체 팀 테스트11개라는 뜻이 아님 |
| `native-build-weather-hud.log/.exit` **Windows Build Success, 종료0** | 마지막 UI/시간 변경까지 Player 컴파일. 이 검증 전용 실행 파일을 제품 배포용 빌드로 쓰지 않음 |
| 앞선 Core 직접 영향18/18·파일 입장/취소5/5·실제 Player/CU15/15 | 실제 무기·장치·HP·감속·논리 ID·rollback·취소 정리. 마지막 UI 수정 후 전체 확대 회귀를 다시 모두 돌린 기록으로 합치지 않음 |
| UTP Host/Client 두 프로세스, 각 종료0 | 서로 다른 협동 Match3개·공유 귀환2회·시간/환경 세대 교체·마지막 actor 정리. **실제 원격 Player 조작·Steam 인터넷 게임 접속 검증이 아님**. 마지막 HUD 변경 후 두 프로세스를 다시 돌린 것은 아님 |
| 실제 Steam Editor/Standalone1계정 | SDK·앱 이용 권한·로비 생성/나가기·TimeScale0 callback·종료.2계정 초대/게임 접속 인수는 아님. 요청1회 Timeout 기록도 보존 |
| 실제 writer/file 복원 | Weather 검증 대역이 사용된 검사와 Core writer 검사는 통과. **운영 Weather writer 완료/전체 제품 Save 성공과 다름** |

앞선 확대 회귀에는301개 중1개 fixture 실패와 후속 보완 등 실행별 차이가 있습니다. 기존 main에서도 재현된 Enemy/Player 검사 실패도 따로 보존했습니다. 모든 검사를 한 번에 모두 녹색으로 만든 보고가 아닙니다. 상세한 당시 수치는 [Core 결과](2026-11-05-core-completion-results.md), [낮밤/Steam 결과](2026-11-05-day-night-steam-results.md), [HUD 최종 결과](../../../../02_architecture/09_world/weather-debug-hud.md)에 있습니다.

## 5. PL이 직접 구현할 수 있고 아직 구현하지 않은 Core 작업

현재 로컬 배치의 완료 기능을 다시 만드는 목록이 아닙니다. 다음 실제 서비스/협동 접속으로 넘어갈 때 Core에 남아 있는 구현을 적습니다.

| 우선 | 미구현 Core 산출물 | 지금 진행 가능한 범위 / 조건 |
| --- | --- | --- |
| 1 | **Steam 로비→실제 NGO 접속 연결** | Steam transport 후보 호환 확인, 로비의 검증된 peer→NGO 연결→기존 준비/시작 경계 연결, Steam ID/계정/Match 참가자 대응. SDK 로비만 구현된 현재와 구분. 두 계정 인수는 계정/권한 준비가 필요 |
| 2 | **Client 우주선 출입 요청·Host 승인 경로** | 요청자/Match/actor를 Host에서 검증하고 위치 보고·응답·Client 개인 Scene 이동을 연결하는 Core 요청 경계. 지금은 보고권 없는 Client actor를 이동 전에 거부함. 실제 원격 actor 생성은 Player 담당이므로 제품2인 완주 인수는 그 연결 후 |
| 3 | **Client 낮밤 남은 시간 동기화** | 현재 환경은 day/night만 보내고 HUD는 남은 시간이 없는 Client에 `--:--` 표시. 구간 epoch/길이/세대와 기존 공용 게임 시계 연결이 필요. Debug Reset은 Host 전용으로 유지할 수 있음 |
| 준비 후 | **Steam 인증→Backend 계정/프로필·온라인 진입 gate** | ticket 요청/취소·검증 결과·실제 계정/프로필·보상 권한 연결. 현재 SDK 이용 권한 확인과 별개. 운영 Backend endpoint/검증 서비스가 먼저 필요하며 SDK 로그인만으로 완료 처리하지 않음 |

이 중 Client 타이머는 이번 **Host 디버그 HUD 완료**와 별도의 협동 확장입니다. Backend는 준비되지 않은 서비스 주소를 임의로 만들지 않습니다. 위 Core 계약은 PL이 진행할 작업이고, 아래 다른 팀원 구현을 PL 미완료로 끼워 넣지 않습니다.

## 6. 다른 팀원 구현·기획 결정·외부 준비

| 분류 | 현재 필요한 것 | Core 상태 |
| --- | --- | --- |
| 김진태 담당 | Weather의 Random API 적용·날씨/예고 큐 writer `world/weather`, 미션 선택 콘솔→출격 연결 | Core 난수/시계/환경/저장/출격 API 제공. **포털5번은 PL의 별도 승인 수정으로 이미 연결**, 미수정이라고 전달하지 않음 |
| Player 담당 | 실제 원격 Player NetworkBehaviour 생성·이동/공격·수리 복제, Host 실제 actor 등록 | `RegisterAuthorityPlayerActor(matchId, playerId, actor)` 제공. 반환 lease 해제 필요 |
| Enemy/World Feature 담당 | 실제 GroundEmergence/AirDrop 어댑터·나무 source 고유 writer/생성·연출 | Core origin/예약/source 취소 경계 제공. 없는 어댑터를 일반 생성으로 몰래 대체하지 않음 |
| 기획/PL 결정 | 자동 저장 발동 사건, Boss/Stage 승리 producer·부활·제품 경제/밸런스 | 저장 watcher/요청/실행 API와 CU 패배는 구현. 지시대로 자동 저장 사건은 선정하지 않았고 이걸 구현 누락으로 세지 않음 |
| 외부 준비/사람 인수 | 두 Steam 계정 권한·초대 Overlay/인터넷, 최종 화면/조작감, 팀 전체 Stage1 인수 | SDK1계정·UTP 기반 검사로 대신 승인하지 않음 |
| 이후 Milestone | SOS/도중 참가·Host Migration·동료 Bot/PvP·채팅·랜덤 매칭의 제품 기능 | 이번 로컬 Core 접점 배치 완료 조건과 구분 |

**운영 전체 Save가 안 되는 직접 원인은 아직 실제 Weather writer가 없기 때문**입니다. PL이 해야 할 Core 저장 실행/원자적 복원 API가 비어 있어서가 아닙니다. 진태 writer 인수 뒤에는 전체 저장/새 Match Resume을 통합 검증해야 합니다.

## 7. 문서를 읽을 순서와 위치

| 순서 | 문서 | 용도 |
| --- | --- | --- |
| 1 | 이 문서 | 현재 수동 확인·완료/미구현·전달 목록 전체 |
| 2 | [Core 수명/API 지도](../../../../02_architecture/01_core/core-lifecycle-and-api-guide.md) | 생성→연결→실행→해제와 팀원 호출 지점 |
| 3 | [실제 Feature 통합](../../../../02_architecture/01_core/core-feature-integration-guide.md) | Player/터렛/Enemy 상태 원본, 시간/저장/네트워크 연결 |
| 4 | [Core 구조 검토](../../../../02_architecture/01_core/core-structure-review-2026-11-05.md) | 삭제/유지 판단, partial 분리, 호환 자료와 복잡성을 남긴 이유 |
| 5 | [낮밤](../../../../02_architecture/09_world/day-night-api.md) · [HUD](../../../../02_architecture/09_world/weather-debug-hud.md) · [우주선 왕복](../../../../02_architecture/03_scene-flow/spaceship-travel-api.md) | 최신5분·Reset·카메라/배치점·0.9초 전환 |
| 6 | [Steam API](../../../../02_architecture/04_networking/steam-api.md) · [네트워크](../../../../02_architecture/04_networking/network-match-api.md) | SDK/로비와 실제 게임 접속 차이, Host 준비/시작·actor 등록 |
| 7 | [시간/예약](../../../../02_architecture/02_match/match-time-api.md) · [관리 책임](../../../../02_architecture/02_match/match-management-responsibilities.md) · [저장 실행](../../../../02_architecture/05_checkpoints/checkpoint-execution-api.md) · [Random](../../../../02_architecture/01_core/core-random-api.md) | 개별 API 사용법/예제/소유권 |
| 8 | [요구·Issue 대응](2026-11-05-core-requirements-and-issue-map.md) · [Core 배치 기록](2026-11-05-core-completion-batches.md) | 기존 스프린트와 구현 대응, 실행별 근거. Issue 실시간 상태는 이번에 재조회/수정하지 않음 |

공유 제외 자료는 아래 실제 경로입니다. Git으로 전달되는 문서가 아니므로 필요한 파일을 직접 보내야 합니다.

- **팀원 전달 최종 폴더**: `Docs/Local/Review/CoreCompletion-2026-11-05/by-owner/` — 네 사람에게 자기 파일 하나씩 전달.
- **변경 파일/39커밋 전체 색인**: `Docs/Local/Review/CoreCompletion-2026-11-05/LOCAL_CHANGE_INDEX.md` — 삭제/추가/수정 경로를 빠짐없이 열람.
- **기존 전달 문서 보존본**: `Docs/Local/Work/CoreIntegration-20261105/handoff-baseline/`; 이번 전달본 정리 전 내용은 `Docs/Local/Work/DayNightSteam-20261105/handoff-before-current-review/`.
- **Core 실제 Feature 통합 증거**: `Docs/Local/Work/CoreIntegration-20261105/validation/`.
- **잔여 Core 보완/네트워크·저장·구조 검증**: `Docs/Local/Work/CoreCompletion-20261105/`.
- **낮밤·Steam·왕복·HUD 최신 XML/log/캡처**: `Docs/Local/Work/DayNightSteam-20261105/`.
- `CoreConsolidation-2026-11-05`는 구현 전 조사, `CoreSprint-2026-11-02`는 기존 전달 자료입니다. 현재 인수의 최종본과 혼동하지 않습니다. 기존 by-owner 사본은 최신 전달본과 맞춰 두었습니다.

별도 저장소 `D:/Dev/GithubIO/TeamHJD-Tech-Docs` 사이트의 이번 변경 반영/배포는 수행하지 않았습니다. 위는 Unity 저장소 로컬 Docs 기준입니다.

## 8. 팀원에게 실제 전달할 내용

| 받는 사람 | 보낼 파일 | 특히 알려야 하는 것 |
| --- | --- | --- |
| 김진태 — World/Weather/Spaceship | `by-owner/김진태_World_Weather_Spaceship.md` | 낮밤 Core 주도·CountTick 중복 금지, Core time-of-day와 Weather writer 분리, Reset은 기존 Weather 명령.3난수/4Weather writer/6콘솔은 미수정.5출입은 승인받아 이미 수정 |
| 이영빈 — Player/UI | `by-owner/이영빈_Player_UI.md` | 공용 시간·HP/감속/실제 writer·허브 이동 모드, 원격 actor 등록 API, PlayerStart 위치 변경·Camera18·0.9초 전환·기존 자산 UI |
| 황재동 — FieldTurret/CU | `by-owner/황재동_FieldTurret_CU.md` | 전력·수리 원본 유지, Core Host 요청 검증·CU 명시 해금 초기화, 공용 시간/실제 writer·참조 수명 |
| 조수빈 — Enemy/Encounter | `by-owner/조수빈_Enemy_Encounter.md` | 실제 시간/source/환경/생성 요청, 원거리 지연 이벤트·Enemy writer v2, 관측 EntityId는 논리 ID, 낮밤/Reset은 다음 계획 조건만 변경 |

현재 기능 구현은 목적별 로컬 커밋으로 분리돼 있습니다. 개인/Editor 미커밋 폰트·DefinitionId·ProjectSettings·ShaderGraphSettings, 이번 조회에서 보이는 `WeatherTransition.mat`, 검증용 Validation은 Core 기능 커밋에 섞지 않았습니다. Shader material 변경의 원인/소유자는 이번에 단정하지 않으며 되돌리지 않습니다. stash10개도 보존합니다. PR 전에는 이 미커밋 변경을 별도로 확인해야 합니다.
