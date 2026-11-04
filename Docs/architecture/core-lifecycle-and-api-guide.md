# Core 실제 호출·수명·API 읽기 지도

갱신: 2026-11-04. 현재 Core 전용 작업 브랜치 기준이다. [구현·검증·보류 결과](../planning/2026-11-04-core-resume-results.md)와 함께 읽는다. 실제 Feature 연결을 완료한 목표 그림으로 오해하지 않는다.

## 1. 같은 이름으로 묶지 않을 상태

| 상태 | 소유자 | 의미 |
| --- | --- | --- |
| GameFlowPhase | GameFlowController | 시작 화면/우주선/행성 입장이라는 App의 위치 |
| NetworkMatchPhase | NetworkMatchCoordinator | 연결된 참가자의 준비·시작·최종 결과 전달 수명 |
| MatchPhase | MatchSession | 이번 판의 규칙 실행·완료 상태 |
| RegionGridSnapshot | Match의 지역 원본/Client replica | 정적/동적 모드 모두 최종 점유 셀이 원본. 삼각형은 동적 점유 계산의 입력 |
| Feature 상태 | Player/FieldTurret/Enemy/World 담당 | 실제 HP·무기·AI·전력·날씨 등의 원본 |

App의 위치가 Mission이라고 NGO/Feature 전체 동기화가 끝난 것은 아니다. GameFlow는 실제 Match 조립을 확인하고 실패를 Faulted로 남긴다. MatchState의 기초 HP 필드가 실제 모든 Feature HP를 대신하지 않는다.

## 2. 생성과 종료의 한 쌍

```mermaid
flowchart TD
    Engine[Unity RuntimeInitialize] --> Bootstrap[AppBootstrap.CreateAppRoot]
    Bootstrap --> App[AppRoot.Initialize / AppServices]
    App --> Shared[Profile·Platform·Content·네트워크·Scene 전환·binary 저장소]
    App --> Startup[StartupSceneRoot.Compose]
    Startup --> Flow[GameFlowController / 기존 Start 버튼]
    Flow --> Hub[우주선 Scene 도착]
    Hub --> Launch[LaunchMissionAsync]
    Launch --> Scene[AppRoot.ComposeScene]
    Scene --> Init[InitialStageCompositionRoot.Compose]
    Init --> Gate{Feature 계약 준비?}
    Gate -->|현재 HOLD 가능| Failure[조립 실패 기록 / GameFlow Faulted]
    Gate -->|계약 복구 후| Session[AppRoot.StartMatch → MatchSession]
    Session --> Owned[시간·지역·공간·환경·명령/읽기 scope]
    Owned --> Ports[실제 Feature 등록·구독]
    Ports --> End[완료/명시 귀환/Scene 해제]
    End --> Cleanup[ReleaseOwnedBindings → lease·구독·scope Dispose]
    Cleanup --> Hub
    App --> Quit[OnApplicationQuit / OnDestroy → AppRoot.Dispose]
    Quit --> SharedEnd[Match 종료 후 App 서비스 Dispose]
```

| 생성자 / 연결 지점 | 소유자 | 종료 지점 |
| --- | --- | --- |
| AppBootstrap.CreateServices | AppRoot | AppRoot.Dispose → AppServices.Dispose |
| ConfigureGameFlow | AppRoot | GameFlowController.Dispose; Scene 전환 전에 취소 요청 |
| StartMenuPresenter.Bind | StartupSceneRoot/시작 Scene | OnDisable/OnDestroy → Unbind. App 흐름 자체는 Dispose하지 않음 |
| AppRoot.StartMatch | AppRoot | EndCurrentMatch / TryEndMatch / Dispose |
| InitialStageCompositionRoot의 각 scope | 해당 Scene composition | ReleaseOwnedBindings / 초기화 실패 rollback |
| MatchTimeService, MatchCoordinator | 권위 Match composition | Scene 종료·Match 종료. NGO pump와 별개 |
| AppServices.CreateCheckpointScope | 호출한 Match composition | 호출자가 scope.Dispose. App 저장소 수명은 유지 |
| Register(participant)의 반환 lease | 실제 participant를 연결한 호출자 | 교체/despawn/종료 때 Dispose |

소유권을 전달받지 않은 다른 Feature의 GameObject를 Core가 저장/정리 편의를 위해 파괴하지 않는다. 등록 lease와 객체 파괴 책임을 구분한다.

## 3. 시작 버튼은 어떤 함수를 호출하는가

```mermaid
sequenceDiagram
    participant V as StartMenuPresenter
    participant F as GameFlowController
    participant H as AppGameFlowHost
    participant A as AppRoot
    participant S as SceneFlowController
    participant U as UnitySceneFlow
    V->>F: EnterSpaceshipAsync()
    F->>H: CanLoad / TravelRestriction 검사
    F->>H: TravelAsync(SpaceshipScene)
    H->>A: ReplaceGameplaySceneAsync()
    A->>A: 최종 결과/권한 확인 → 필요 시 Match 종료
    A->>S: ReplaceAsync(scene, token)
    S->>S: 전환 직렬화 / 입력 잠금 lease
    S->>U: ReplaceAsync()
    U-->>F: 실제 Single Scene 로드 완료
    F->>F: Phase = Spaceship / CurrentSceneKey 갱신
```

Mission 출격은 위 순서 뒤 `WaitForMissionReadyAsync`를 추가한다. 파일만 로드되었거나 Network 준비만 시작한 상태를 입장 성공으로 반환하지 않는다. 조립 예외·해제·App 종료·준비 대기 시간 초과는 실패/취소로 전파된다. 현재 터렛 HOLD는 여기서 확인되는 미연결이다.

기존 공용 메뉴/결과 버튼은 `AppRoot.NavigateFromScreenAsync`를 거쳐 같은 GameFlow로 전달한다. 완전 재시작은 `RestartMissionAsync`, 결과 귀환은 `ReturnToSpaceshipAsync`다. 임시 우주선 방문은 필드를 종료하지 않는 별도 출입 계약이며 이 API를 재사용해 Match를 종료시키지 않는다.

## 4. 시간과 Weather 연결

현재 권위 Scene의 생성 순서는 `MatchTimeService → MatchCoordinator → MatchEnvironmentSceneScope`다. 하지만 그 이전 터렛 바인딩이 HOLD여서 현재 Initial_Stage에서는 이 단계까지 도달하지 못할 수 있다.

1. Timestamp adapter가 NGO 서버 시각 또는 개발용 Unity 단조 시각을 읽는다.
2. MatchTimeService의 Clock은 anchor·배속·정지 정책으로 게임 초를 계산한다. 시간은 복원 시 새 epoch를 갖는다.
3. 같은 서비스의 Scheduler가 기한에 도달한 예약을 실행한다. 종료/epoch 교체/호출자 취소로 과거 예약을 정리한다.
4. MatchPresenceService는 Host 참가자의 위치 보고를 집계한다. 1명 또는 SOS 2명 모두 우주선에 있어야 필드 정지다.
5. 김진태의 Weather/Time 구현이 `IMatchScheduledEnvironmentFeature.BindAuthority`로 예약을 등록하고 반환 lease로 정리한다. 실제 Feature가 없는 상태를 낮밤 완성으로 표시하지 않는다.
6. 환경 Changed는 Match의 읽기 snapshot을 바꾼다. 환경 변경 자체를 추가 Spawn 명령으로 바꾸지 않는다.

게임 시간 정지는 네트워크 연결·메시지 pump·우주선/UI와 다르다. Enemy/터렛의 진행·물리/예약을 실제로 멈추는 부분은 각 Feature consumer가 제공해야 한다. Core가 해당 코드의 deltaTime/공격 루프를 몰래 교체하지 않는다.

## 5. 저장 API를 어디서 조립하는가

AppBootstrap은 `Application.persistentDataPath/Checkpoints`의 BinaryCheckpointStore와 payload codec을 주입한다. 현재는 로컬 개발 사용자 구성이다. Steam 계정/Backend 프로필 권위 및 계정별 저장 분리는 실제 서비스 도입 때 연결한다.

Match 조립부의 호출 형태는 다음과 같다. 아래 인자는 조립부가 실제 계약에서 제공해야 하는 값이며, 새 자동 저장 규칙이나 임의 writer 키가 아니다.

```csharp
// runtime: 현재 Match/게임 시간/권위/취소/복원 전후 정리 callback을 가진 port.
// participants: 실제 원본에서 캡처/복원하는 각 담당자의 writer.
// requiredKeys: 등록된 것에서 자동 추측하지 않고 조립 계약으로 정한 필수 writer 목록.
var scope = app.CreateCheckpointScope(runtime, participants, requiredKeys);
var missing = scope.Service.MissingParticipantKeys;
var saved = await scope.Service.SaveAsync(slot, cancellation);
if (!saved.IsSuccess || !saved.IsCurrentScope)
{
    // Reason을 표시/기록하고 이전 Match의 결과를 현재 게임에 적용하지 않는다.
}
// Feature 교체 시: var lease = scope.Register(writer); ... lease.Dispose();
// 해당 Match 종료 시 scope.Dispose();
```

필수 writer 누락은 직접 API와 CheckpointTrigger가 공유하는 서비스 내부에서 거부한다. scope는 최소 한 개의 필수 키를 요구한다. Source에 현재 등록된 writer만 보고 게임 전체 저장이 준비됐다고 추측하지 않는다.

```mermaid
sequenceDiagram
    participant C as 명시 호출자 / 주입 사건
    participant T as CheckpointTrigger
    participant S as CheckpointService
    participant W as 실제 participant
    participant D as App BinaryCheckpointStore
    C->>T: Requests.Publish(request) 또는 Service 직접 호출
    T->>S: SaveAsync / RestoreAsync
    S->>S: Host·Match·epoch·중복·필수 writer 검사
    S->>W: 같은 프레임 정지 → Capture
    S->>D: 불변 자료 SaveAsync
    D-->>C: 저장 결과 / 현재 scope 여부
    C->>S: 명시 Restore
    S->>W: 모든 PrepareRestore → 모든 Apply
    S->>W: ResolveReferences → FinishRestore
    S-->>C: 복원 결과 또는 백업 복구/실패
```

`AppRoot.ReadCheckpointAsync`는 재실행 호출자가 메타데이터를 먼저 읽는 port다. 이 호출만으로 새 Match·Player·터렛·Enemy를 생성하지 않는다. 실제 Resume는 저장 metadata로 새 Match를 준비하고 합의된 writer를 등록한 뒤 Restore해야 한다. 현재 Load 버튼을 비활성화한 이유다.

기본 정책은 당시 World 복원, 경험치 등 Progression과 Currency는 현재 값 유지다. Currency는 요청 정책에 따라 체크포인트 값으로 되돌릴 수 있다. 현재 프로필 자료에 경험치/성장 원본 전체가 없어 Feature 계약을 통해 구현해야 한다.

## 6. 팀원이 제공/소비할 접점

| 담당 | Core가 제공하는 것 | 담당자가 제공/연결할 것 | 현재 상태 |
| --- | --- | --- | --- |
| Player/UI | 실제 권위 actor 등록 lease, 참가자 위치 보고, 공용 입력/화면 경계 | 실제 원격 actor·우주선 출입 사실·HP/무기/설치물 writer | 코드 변경 없이 대기 |
| FieldTurret | Match ID/요청·권한/지역 Grid/읽기 snapshot 계약 | 실제 소유·ID·Match lease·관측 및 전력/actor writer | CORE-FEATURE-HOLD; 표식의 요구 API를 검토해야 함 |
| Enemy/Encounter | 확정 공간/환경·source context와 요청 결과/수명 | 실제 Planner/Executor 소비·객체 추적·진행 정지·writer | 기존 PR 범위 및 담당자 실제 구현과 별도 인수 |
| World/Weather | clock/scheduler, 환경 snapshot, GameFlow/Scene 전환 API | 실제 날씨·낮밤 규칙, 허브 PlayerStart/포털·콘솔 UX | 담당자와 연결 대기 |
| Core | App 저장소·조율/규칙·DI·검증·문서 | 위 계약의 조립, 현재 원본과 문서의 일치 | 독립 범위 구현/검사 수행 |

11/3 회의의 소환형/회복형 분리·암살자 타깃·수리/피해 공통화·터렛 검증 지원 목록은 별도 담당자 작업/요청이다. 이번에 그 알고리즘이나 미정 API를 임의 추가하지 않았다. [최신 main 회의](https://github.com/Team-HJD-21/unity-6-the-developer/blob/bf3db829/Docs/planning/2026-11-03-weekly-progress.md)

## 7. 전체 검토에서 확인한 것과 남은 것

Core C# 437개에는 Editor55개·Tests50개가 포함된다. 단순 파일 수를 모두 게임 관리 runtime으로 보지 않는다. Domain90·Contracts104는 값/자료·외부 경계로 나뉜다. 현재13개 Core asmdef의 참조 순환과 엔진 비참조 계층의 Unity using은 정적 검사에서 발견되지 않았다. 이 검사는 전체 동작/설계의 무결성을 증명하는 검사는 아니다.

AppRoot는 Unity 수명·현재 Match·Scene 조립의 소유자다. GameFlow 절차는 Application으로, Unity 준비 확인은 AppGameFlowHost로, 시작 버튼은 Presenter로 분리했다. 체크포인트의 저장소/Match scope/실행/관찰도 각 수명으로 분리했다.

여전히 Bootstrap의 터렛 구체 타입 의존과 HOLD가 있다. 이를 계층 수만 늘려 숨기거나 동일 동작의 임시 Manager로 대체하지 않았다. 다음 계약 합의 때 Feature가 공개한 최소 port로 축소해야 한다. 기존 AppScreenPresenter의 개발용 기본 폰트/화면은 신규 Startup의 복제 시각 요소와 구분하며 최종 공용 테마 인수도 남아 있다.

PL은 README의 읽는 순서→이 문서의 실제 함수 흐름→각 API 문서→보류표 순으로 확인한다. 이해가 어려운 함수명/소유권은 PR 이전 검토에서 보완한다.
