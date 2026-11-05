# Core 사용 안내


**지금 확인/전달할 최종본:** [Startup/Initial_Stage 수동 확인7단계·전체 구현·남은 Core 작업](../../../Docs/planning/2026-11-05-core-local-review.md).


**인게임 날씨 디버그:** [Day/Night·5분 타이머·Reset 사용법](../../../Docs/architecture/weather-debug-hud.md). 우주선 페이드는 현재 0.3초씩 총 약 0.9초입니다.


**우주선 전환 최신 수정:** [동일 카메라·0.9초 페이드·첫 출격 위치](../../../Docs/planning/2026-11-05-spaceship-fade-results.md). 우주선↔필드에서는 진행 바를 표시하지 않습니다.


**우주선 첫 출격·왕복 패치:** [이동 경로·로딩·카메라 처리](../../../Docs/architecture/spaceship-travel-api.md).

**이번 추가 구현·자동 확인 결과:** [최종 결과와 최소 확인 목록](../../../Docs/planning/2026-11-05-day-night-steam-results.md)

**낮밤·Steam·시작 우주선 후속:** [배치와 담당 경계](../../../Docs/planning/2026-11-05-day-night-and-steam-batches.md) · [낮밤 API](../../../Docs/architecture/day-night-api.md) · [Steam 사용법](../../../Docs/architecture/steam-api.md). 기존 완료 보고 이후 추가 승인한 구현이다.

**이번 목표 결과·수동 확인:** [최종 정리](../../../Docs/planning/2026-11-05-core-completion-results.md)

**현재 상태:** [요구·Issue 대응과 남은 담당 경계](../../../Docs/planning/2026-11-05-core-requirements-and-issue-map.md). 감속·원거리 공격 상태·새 Match 파일 복원·2인 Scene 전환 누락을 보완했습니다. 제품 전체 저장은 실제 Weather writer가 필요하며 PL 수동 인수·PR 전입니다.

갱신: 2026-11-05. 최신 main의 Feature·기존 Scene·Prefab을 유지한 Core 기반 통합 브랜치다. main에 이미 반영된 터렛 권한/ID/관측 계약을 사용하도록 Core 연결을 복구했다. CU는 기존 IControlUnitVitals를 그대로 읽으며 Feature에 새 인터페이스 구현을 강제하지 않는다. [기반 분리와 검증 기록](../../../Docs/planning/2026-11-05-foundation-main-integration.md)을 현재 기준으로 읽는다.

- [이번 요구·Issue 대응](../../../Docs/planning/2026-11-05-core-requirements-and-issue-map.md) · [잔여 보완 배치 결과](../../../Docs/planning/2026-11-05-core-completion-batches.md)
- [실제 함수 호출·수명·Feature 연결 지도](../../../Docs/architecture/core-lifecycle-and-api-guide.md)
- [시간/예약 API](../../../Docs/architecture/match-time-api.md) · [필드 정지](../../../Docs/architecture/match-field-pause-api.md)
- [체크포인트 IO](../../../Docs/architecture/checkpoint-storage-api.md) · [저장/복원 실행](../../../Docs/architecture/checkpoint-execution-api.md)
- [Steam 도입 설계](../../../Docs/architecture/steam-integration-design.md)
- [범용 Random API·기존 교체 위치](../../../Docs/architecture/core-random-api.md) · [우주선 출입 보고 API](../../../Docs/architecture/core-presence-report-api.md) — 실제 연결과 진태 담당 후속은 [Feature 통합 안내](../../../Docs/architecture/core-feature-integration-guide.md) 참고.

## Random을 사용하는 팀원은 먼저 확인

[Random API 사용법](../../../Docs/architecture/core-random-api.md)의 첫 예제처럼 정적 헬퍼 한 줄로 연결한다. 새 부모 클래스·개별 해제 클래스는 필요 없다. Core가 반환값을 보관하고 Scene/Match 정리 때 해제한다. 날씨·스폰·공격은 Host Match, 로컬 HUD 효과는 App의 PresentationRandom을 사용한다.

```csharp
// 기존 IRandomConsumer 구현 컴포넌트의 필드와 메서드.
// using System; using TeamHJD.Game.Application; using TeamHJD.Game.Contracts;
private IRandomSource _random;

public IDisposable BindRandom(IRandomStreams streams)
    => RandomBinding.Bind(streams, "weather", ref _random);

// Core가 연결한 이후 Feature 실행 시 호출한다.
public bool RollSuccess(double chance) => _random.Chance(chance);
```

해제 후 참조는 남지만 `IsAvailable`이 false가 된다. 같은 필드의 재연결·순수 함수 전달·직접 조립 예제는 사용법을 따른다. 게임 난수는 Host에서만 사용한다. 현재 Player·Enemy·터렛 소비를 연결했으며 Weather의 실제 선택 교체는 김진태 담당이다.

## 먼저 실행해 볼 것

이번 변경의 수동 확인은 [최신7단계](../../../Docs/planning/2026-11-05-core-local-review.md#2-pl이-직접-확인할-것--7단계)를 따른다. 현재 브랜치는 PR 전이며 기존 HOLD 상태와 혼동하지 않는다.


1. Unity 메뉴 `Tools > TeamHJD > The Developer > Open Core Startup Scene`을 선택한다. 경로는 `Assets/Scenes/Bootstrap/Startup.unity`다.
2. Play 후 기존 Main의 **Start** 버튼을 누른다. 우주선 `SpaceShipGrayBox`로 이동한다. 이 경로는 실제 PlayMode 검사로 확인했다.
3. Game Debugger의 **Overview > APP · GAME FLOW**에서 위치와 전환 상태를 확인한다. 선택 Mission 출격/귀환도 같은 API를 호출한다.
4. `Initial_Stage`는 main의 기존 제작 Scene을 사용한다. 터렛 권한/ID/해금 초기화는 main의 승인된 계약으로 연결한다. 지원 Feature의 실제 시간 소비·저장 writer는 연결했다. 실제 Weather writer·원격 Player와 제품 전체 승패/복원 인수는 구분한다.

Build Settings의 첫 Scene은 `Startup`이다. Editor에서 다른 Scene을 직접 Play해도 해당 Scene을 유지하며 시작 화면으로 강제 이동하지 않는다. 저장 슬롯은 `AppRoot.ResumeCheckpointAsync`로 실제 Mission 복원을 요청한다. writer 누락은 명확히 거부하며 현재 운영 Scene은 `world/weather`가 없어 전체 저장이 차단된다. Exit는 Standalone 앱 종료이며 Editor의 Play 종료를 대체하지 않는다.

시작 화면은 `Reference/Main.unity`의 Canvas·카메라·EventSystem과 기존 이미지/폰트를 복제했다. 복제본에서 레거시 동작과 미연결 버튼 호출만 제거했다. 원본 Main의 시각 요소를 재사용한다. Initial_Stage에는 현행 Encounter 생성기를 선택하는 명시 설정을 추가했고 Build 첫 Scene은 Startup으로 정렬했다. 화면의 최종 배치·가독성은 사람이 확인해야 한다.

## 코드를 읽는 순서

| 순서 | 클래스와 함수 | 알 수 있는 것 |
| --- | --- | --- |
| 1 | [AppBootstrap](Bootstrap/AppBootstrap.cs): `ResetForNewRuntimeSession`, `CreateAppRoot`, `CreateServices` | Unity가 호출하는 진입점, 개발 서비스·실제 로컬 저장소 생성 |
| 2 | [AppRoot](Bootstrap/AppRoot.cs): `Initialize`, `OnSceneLoaded`, `ComposeScene`, `Dispose` | App 소유권과 Scene 조립·종료 |
| 3 | [AppServices](Application/AppServices.cs): 생성자, `CreateCheckpointScope`, `PumpNetwork` | typed 서비스 묶음, App/Match 수명 분리 |
| 4 | [StartupSceneRoot](Bootstrap/SceneComposition/StartupSceneRoot.cs): `Compose` | 시작 화면에 App 흐름과 기존 버튼을 전달하는 곳 |
| 5 | [GameFlowController](Application/GameFlow/GameFlowController.cs): `AdoptMainMenu`, `EnterSpaceshipAsync`, `LaunchMissionAsync` | 제품 진입 위치와 전환·실제 Mission 준비 확인 |
| 6 | [AppGameFlowHost](Bootstrap/AppGameFlowHost.cs): `TravelAsync`, `WaitForMissionReadyAsync` | Unity/기존 App 전환과 순수 C# 흐름을 연결하는 adapter |
| 7 | [InitialStageCompositionRoot](Bootstrap/SceneComposition/InitialStageCompositionRoot.cs): `Compose`, `ComposeAuthority`, `ReleaseOwnedBindings` | 실제 Match·Player·공간·지역·환경 조립. main의 Feature 계약과 읽기 Port를 소비 |
| 8 | [MatchSession](Application/MatchSession.cs), [MatchCoordinator](Application/MatchCoordinator.cs) | Match 원본/모드 규칙과 실제 Feature 사건에 따른 완료 조율 |
| 9 | [MatchTimeService](Application/Time/MatchTimeService.cs), [MatchPresenceService](Application/Time/MatchPresenceService.cs) | 게임 시계·예약·참가자 위치와 필드 정지 정책 |
| 10 | [MatchCheckpointScope](Application/Persistence/MatchCheckpointScope.cs), [CheckpointService](Application/Persistence/CheckpointService.cs) | 명시 저장·복원·필수 writer·등록/해제 |
| 11 | Contracts와 해당 Infrastructure adapter | Feature가 제공/소비할 port, NGO·Unity·binary IO 구현 |

## Unity는 Bootstrap을 누가 호출하는가

`AppBootstrap`을 Scene에 붙이거나 다른 스크립트에서 직접 호출하지 않는다. Unity 엔진이 `RuntimeInitializeOnLoadMethod`가 붙은 정적 메서드를 실행한다.

```mermaid
sequenceDiagram
    participant U as Unity 엔진
    participant B as AppBootstrap
    participant R as AppRoot
    participant S as Scene composition
    participant V as 시작 화면
    U->>B: SubsystemRegistration → ResetForNewRuntimeSession()
    U->>B: BeforeSceneLoad → CreateAppRoot()
    B->>R: AddComponent + Initialize(CreateServices())
    B->>U: DontDestroyOnLoad(AppRoot)
    U->>R: 첫 Scene 로드 → OnSceneLoaded()
    R->>S: ComposeScene → Compose(this)
    S->>R: ConfigureGameFlow(routes)
    S->>V: StartMenuPresenter.Bind(flow)
    V->>R: Start → GameFlow.EnterSpaceshipAsync()
```

`SubsystemRegistration`은 Domain Reload를 끈 새 실행에서도 중복 방지 상태를 초기화하는 진입점이다. `BeforeSceneLoad`는 첫 Scene 객체의 Awake보다 먼저 App을 준비한다. Scene이 바뀌어도 App은 유지되고 Scene 소유 객체는 해당 Scene에서 정리한다.

## 폴더 이름과 의존성

| 폴더 | 의미 / 예 |
| --- | --- |
| Domain | 게임의 개념·불변 값·규칙·자료구조. ID, MatchConfig, Grid, 지역, 저장 DTO. Unity Scene을 찾지 않음 |
| Contracts | 외부에 요구하거나 제공하는 인터페이스/요청·결과 계약. 누가 구현하는지는 조립부가 결정 |
| Application | 규칙과 port를 호출하는 실행 절차. MatchSession, GameFlowController, 시간·예약·체크포인트 조율 |
| Bootstrap | 실제 객체를 만들고 연결하는 Unity 진입/Scene 조립부. Core와 Feature가 만나는 위치 |
| Infrastructure | Unity Scene, NGO, binary 파일 IO 등 외부 기술 adapter. Fakes는 외부 서비스를 대신하는 개발/검사 구현 |
| Content | 디자이너가 제작한 정의/카탈로그의 authoring과 runtime 읽기 |
| Presentation | 입력·화면·카메라·실제 Unity 객체의 표현 adapter. 원본 게임 규칙을 소유하지 않음 |
| Editor | Game Debugger·Scene View·제작 도구. Player 빌드의 필수 runtime과 분리 |
| Tests | Core 계약/실제 adapter 검사. fixture 통과를 제품 전체 인수와 구분 |

```mermaid
flowchart TD
    Boot[Bootstrap / Scene composition] --> App[Application]
    App --> Ports[Contracts]
    App --> Data[Domain]
    Ports --> Data
    Boot --> Infra[Infrastructure]
    Boot --> Views[Presentation / Feature ports]
    Infra --> Ports
    Views --> App
    Views --> Ports
    Tool[Editor] --> Boot
    Tool --> Data
```

실제 asmdef가 컴파일 경계를 결정한다. Domain/Application/Contracts와 Infrastructure.Fakes는 엔진 참조가 없는 구성이다. Feature의 MonoBehaviour·Steam/NGO SDK 타입을 Domain에 넣지 않는다. 공개 서비스는 주입받고 Feature가 전역 AppRoot/Manager를 찾아 상태를 직접 바꾸지 않게 한다.

## 주요 사용 경계

- 게임 흐름: 시작/출격/결과의 호출자는 `app.GameFlow`를 사용한다. `Phase`는 App의 현재 진입 위치이며 `MatchPhase`·네트워크 준비 Phase와 다르다. `ReturnToSpaceshipAsync`는 Match를 종료하고 귀환하며, 임시 우주선 출입과 같지 않다.
- Match: `AppRoot.StartMatch`는 Factory로 생성한 Session을 App에 등록한다. `InitialStageCompositionRoot`는 그 Session에 실제 Scene 원본을 연결한다. `MatchCoordinator`는 CU 사건을 읽어 Session의 모드 규칙으로 완료를 조율한다.
- 시간: 공용 게임 시계의 정지·배속은 NGO 접속 시각/메시지 pump와 분리한다. 2인 모두 우주선에 있어야 필드를 정지하며 실제 출입 사실은 Player 담당자가 제공한다.
- 저장: App은 binary 저장소를 제공하고 Match 조립부가 `CreateCheckpointScope`에 runtime·실제 participant·필수 writer 키 목록을 넘긴다. 필수 writer가 빠지면 `MissingParticipantKeys`와 `RequiredFeatureWritersUnavailable`로 설명한다. 현재 Stage에 전체 Feature writer가 연결된 것은 아니다.
- 환경/Encounter: Core는 확정 공간·환경 snapshot을 전달한다. Enemy/Encounter가 계획/실제 생성을 소유하고 World/Weather가 실제 날씨·낮밤 상태를 소유한다.
- UI: StartMenuPresenter는 기존 버튼을 App 흐름에 전달한다. 공용 메뉴/결과 버튼도 GameFlow를 사용한다. Combat HUD와 Player 입력 원본은 Player/UI 담당 범위다.

`Catalog`는 플레이어 상태가 아닌 콘텐츠 정의 목록이다. App은 여러 Match에서 같은 정의를 읽을 수 있도록 카탈로그를 가진다. 어떤 Match를 시작할지는 이후 요청/설정이 선택한다.

`Snapshot`은 어느 순간의 읽기 자료다. 실제 CU/Player/터렛 HP를 소유하는 Feature와 `MatchState`의 기초 상태를 혼동하지 않는다. 현재 Domain MatchState만 저장해서 전체 전투를 되돌릴 수는 없다.

## 왜 GameManager 하나 대신 App/Match 수명을 구분하는가

App은 플랫폼/프로필·네트워크 연결·Scene 전환·저장소처럼 게임 중 여러 Match가 공유하는 서비스를 관리한다. Match는 해당 판의 시간·규칙·공간·환경·요청 수명을 관리한다. Scene은 실제 Player/적/터렛을 연결하고 자신의 구독·lease를 종료한다.

이렇게 나누면 Scene을 직접 실행해도 필요한 의존성을 명시할 수 있고, 순수 C# 절차는 Unity 없이 검사할 수 있다. Match가 끝났다고 Steam 연결까지 끊거나, UI가 닫혔다고 다른 입력 잠금이 풀리는 문제를 줄인다. 새 서비스를 추가할 때는 이름에 Manager를 붙이는 것보다 소유자·수명·원본·생성/해제 주체를 먼저 정한다.

`IDisposable.Dispose()`는 해당 객체가 소유한 구독·등록·작업 등을 해제하는 C# 관례다. Unity의 GameObject 파괴와 같지 않다. 호출자가 받은 lease는 교체/종료 때 Dispose하고, 다른 소유자의 서비스는 임의로 Dispose하지 않는다. `sealed`는 상속을 막고, `a ?? b`는 a가 null일 때 b를 선택한다.

## 현재 한계와 팀 연결

최신 실제 상태는 [Feature 통합 안내](../../../Docs/architecture/core-feature-integration-guide.md)를 기준으로 한다. Player/터렛/Enemy 시간·입력·저장 원본을 연결했고, Weather 환경/예약은 Core 연결을 마쳤다. 실제 Weather writer·날씨 Random·포털/콘솔은 김진태 담당이다. 전체 Save는 해당 writer 누락 시 비활성이다. 재실행 호출은 `AppRoot.ResumeCheckpointAsync`이며 1인 Story부터 지원한다.


- 터렛 Match 권위/ID/관측: main의 승인된 API로 Core 소비자를 연결했다. 터렛 생성·전력·공격 규칙은 변경하지 않는다.
- Weather/Time: Core 공용 clock/scheduler와 환경 발행·Client 표현은 연결했다. 낮밤 producer는 이번에 Core에서 추가했다. 날씨 선택·고유 저장과 포털은 김진태 담당 범위를 따른다.
- Player/Enemy/터렛/CU의 실제 writer와 재생성을 연결했다. 전체 저장은 Weather writer 누락으로 차단한다. 제품 경제와 2인 재접속은 별도다.
- 시작 우주선의 실제 Player/Camera와 Host 출입문은 Core 이동 경계에 연결했다. 원격 Client 출입 권위·제작 콘솔 버튼은 World/Player 담당자와 후속 연결이 필요하다. Debugger 요청이 실제 콘솔 UX의 완료를 뜻하지 않는다.
- Steam 플랫폼은 설정에 따라 실제 SDK/개발 구현을 선택한다. Backend·실제 프로필 인증은 후속이며 Profile은 개발 Fake다. 계정·성장 원본과 체크포인트를 분리한다.
- 최신 main `1a03e9a91`은 통합했다. 격리 Windows Build/두 프로세스의 공유 Scene·시계/환경 흐름을 확인했으며, 실제 원격 Player 전투와 제품 전체 승패·운영 Weather 저장·팀 통합 Build 인수는 별도다.

과거 작업 공간의 제외/보류 이력은 [HOLD 목록](../../../Docs/planning/2026-11-02-feature-integration-hold.md)에 보존한다. 이 이력을 최신 main의 승인된 계약까지 미구현이라는 뜻으로 해석하지 않는다. 기반 통합 당시 범위와 검증은 [main 통합 기록](../../../Docs/planning/2026-11-05-foundation-main-integration.md)을 따른다.

## 구조를 단순하게 정리한 부분

`MatchSimulation` 중간 객체를 제거하고 Session이 State의 단계/확정 결과에 직접 연결된다. 실제 전투는 Feature/Host 경계에 있으며 일반 `Submit(GameCommand)`는 처리하지 않는다. 큰 Scene root는 진입/해제와 Update를 본 파일에, 네트워크 준비/읽기 복제를 `.Networking.cs`, 공간 투영을 `.Spatial.cs`, 저장 조립을 `.Checkpoints.cs`에 둔다. 별도 Manager나 상태 원본이 늘어난 것은 아니다. [삭제/유지 판단과 전후 구조](../../../Docs/architecture/core-structure-review-2026-11-05.md)를 함께 본다.
