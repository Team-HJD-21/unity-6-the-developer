# Core API 호출과 소유권 규약

기준일: 2026-10-15 · 상태: 현재 작업 브랜치 기준 · 관련 Issue: #445, #442, #495, #496, #526–#528

팀원이 Player·Turret·Enemy를 Core에 연결할 때 사용할 계약과 종료 책임을 정리한다. 명명 규칙은 [Convention](naming-and-architecture-conventions.md), 타입·실행 흐름은 [Core README](../../Assets/Scripts/Core/README.md)를 따른다. 아래의 공개 계약이 모두 출시용 게임 규칙을 구현했다는 뜻은 아니다.

2026-11-02 보강 계획: [이번 주 전체 배치](../planning/2026-11-02-core-sprint-scope.md#9-함수-설명팀-문서-완료-기준)에서 기존 Core 연동 API 전체의 호출자·시점·결과·해제 책임을 점검하고 소스 XML 설명과 이 문서의 파트별 예제를 함께 보강한다. [함수 설명 규약](naming-and-architecture-conventions.md#공개-api와-함수-설명)을 따른다. 시간·안전장치의 예정 API와 현재 존재하는 API를 구분하며, 이 계획 추가가 구현·문서 전수 보강 완료를 의미하지 않는다.

## 호출 방향과 상태 소유권

현재 지역/네트워크/정식 Encounter API는 [스프린트 API 보충](core-sprint-region-and-request-api.md)을 함께 확인한다. 기존 문자열 메서드는 정식 요청으로 변환하는 호환 진입점이며, 별도 생성 경로가 아니다.

이전 `Global` 계열과의 대응은 책임을 이해하기 위한 것이며 상속·호환 계보가 아니다. 신규 구현은 해당 레거시를 호출하지 않는다.

| 이전 구조 | 현재 Core 대응 |
| --- | --- |
| `GlobalBootstrap / Global` | `AppBootstrap / AppRoot`: App 생성과 소유 |
| `GlobalServices` | `AppServices`: composition 내부 typed dependency 묶음 |
| `Game` | `MatchSession`과 Scene composition: 한 판 수명과 Unity 연결 분리 |
| `RunState` | `MatchState`: 한 판의 현재 상태. 계정 진행도는 별도 Profile |

기존 #388의 `IMatchFactory`는 후보 명칭이며 현재 생성 책임은 `MatchSessionFactory`로 구현돼 있다. 새 호출자는 존재하지 않는 후보 인터페이스를 전제로 작성하지 않는다.

| 호출자 | 계약과 동작 | 호출자가 하지 않을 일 |
| --- | --- | --- |
| Scene 조립부 | `IAppMatchHost.StartMatch(...)`로 세션을 만들고 필요한 Feature에 주입 | `AppServices`를 게임 서비스 검색기로 공개하거나 static Instance 사용 |
| Player 입력 | `PlayerSceneAdapter.Bind(session)` → `Submit(GameCommand)` | 입력 코드에서 `MatchState`를 직접 변경하거나 명령 성공을 추정 |
| Turret 입력 | `IBattlefieldTurretInputSource.CaptureTurretLayout()` → `UpdateBattlefieldTurretLayout(...)` | `FieldTurretBase`·Transform을 Core snapshot에 보관 |
| Player·Enemy 위치 | 조립부가 `BattlefieldDynamicSpatialInput` → `UpdateBattlefieldParticipants(...)` | 별도 전역 Grid 또는 두 번째 위치 권위 모델 생성 |
| Encounter 소비자 | `IEncounterMatchConsumer.BindMatch(matchId, snapshotProvider)`로 최신 읽기 전용 공간 결과 수신 | Battlefield 분석 재구현, 과거 Match의 provider를 재사용 |
| HUD·관측자 | `session.Events.Subscribe(...)`, `session.CreateSnapshot(sequence)` | 구독 인터페이스로 이벤트 발행 또는 상태 쓰기 |

`MatchState`는 Core의 Match 상태이며 Battlefield는 파생 분석이다. 현재 Unity actor의 실제 위치·체력과 Core 전투 상태를 모두 일원화한 것은 아니다. Scene adapter는 이 전환 경계에서 값만 투영한다. 전선 기하·셀 점유와 Spawn 정책은 분리하며, 후자는 Enemy/Encounter가 소유한다.

## Command와 Query와 Event

- Command는 수행 의도다. `GameCommand`는 비어 있지 않은 `CommandId`, `IssuerId`를 가진다. `MatchSession.Submit`은 권한 확인 뒤 `CommandSubmissionResult`를 반환한다. `Accepted / Rejected / Unauthorized / NotHandled`를 구분한다. 현재 `MatchSimulation.Execute`는 실제 전투 명령을 처리하지 않으므로 `NotHandled`를 성공으로 표시하지 않는다.
- Query는 조회다. `MatchSession.Battlefield`의 immutable 결과를 현재 평가 시점에 읽는다. `MatchId`와 `Revision`, Grid 설정을 함께 해석한다. 새 snapshot이 발행돼도 과거 snapshot이 자동 변경되지는 않는다.
- Event는 이미 발생한 사실이다. `IMatchEventStream.Subscribe`가 돌려준 `IDisposable`을 구독자가 정리한다. 생성 요청 수와 실제 생성·사망·제거 수를 구분한다.

API에는 역할에 맞는 `Start / Bind / Unbind / Update / Reconfigure / Try / Create` 동사를 사용한다. 실패가 정상적인 분기인 API는 `Try...`의 bool과 사유를 확인한다. `Can...`의 true가 이후 실행 성공을 보장하지는 않는다.

## Player와 Turret와 Enemy 연결 예

아래의 `session`, `playerAdapter`, `turretSource`, `encounterConsumer`는 조립부에서 전달받은 실제 객체다. 새 전역 탐색 경로를 만들기 위한 예가 아니다.

```csharp
// Player: 입력 adapter가 Match의 명령 경계를 사용한다.
playerAdapter.Bind(session);
CommandSubmissionResult result = playerAdapter.Submit(command);
// result.Status를 확인한다. NotHandled는 아직 규칙이 없는 명령이다.

// Turret: 엔진 객체 대신 ID/XY 값으로 topology 입력을 갱신한다.
session.UpdateBattlefieldTurretLayout(turretSource.CaptureTurretLayout());

// Enemy: 각 평가 시 최신 immutable snapshot을 조회한다.
encounterConsumer.BindMatch(session.Config.MatchId, () => session.Battlefield);

// 구독은 자신이 소유한다. Scene 종료 전에 해제한다.
IDisposable subscription = session.Events.Subscribe(OnMatchEvent);
subscription.Dispose();
encounterConsumer.UnbindMatch(session.Config.MatchId);
playerAdapter.Unbind();
```

실제 조립부는 provider에서 자신의 Match가 아직 현재 Match인지 확인한다. `InitialStageCompositionRoot`의 소유권 검사와 정리 순서를 따른다. Player·Enemy 위치 수집은 기본 0.1초 간격이며 셀 점유가 바뀔 때만 Grid를 갱신한다. 터렛 topology는 배치 입력이 바뀔 때 갱신하며 과열은 배치 점을 제거하지 않는다.

잘못된 예는 `GeneralManager.Instance`로 새 Core를 찾기, Feature가 `EndCurrentMatch()`로 다른 Scene의 Match까지 끝내기, cached snapshot을 영구 최신 결과로 간주하기, `EnemySpawnExecutor.Execute`를 직접 호출해 공통 정책·관측을 우회하기다.

## Scene과 Match 종료

`IAppMatchHost.CurrentMatchId`와 자신이 생성한 ID를 대조한다. Scene 소유자는 `TryEndMatch(ownedMatchId)`를 사용하며, false는 현재 Match가 달라 종료하지 않았다는 뜻이다. `EndCurrentMatch()`는 App의 무조건 종료 경로다.

정리 순서는 현재 composition의 Encounter unbind·Player actor 정리·소유 Match 종료를 따른다. 한 소비자의 Unbind 실패가 나머지 정리를 중단하지 않도록 한다. `MatchSession.Dispose`는 이벤트 버스와 공간 Runtime을 정리하고 재호출을 허용한다. 종료 뒤 변경·명령 메서드 호출은 `ObjectDisposedException`이다. `HudPresenter.Dispose()`는 자신이 가진 구독을 해제한다. 오래된 session/provider를 새 Match에 연결하지 않는다.

Player adapter와 Scene은 해제 콜백 전에 이전 소유/입력 참조를 닫고, 여러 기능·actor·지역·FieldTurret 자원 정리를 끝까지 시도한 뒤 오류를 합쳐 전달한다. `Dispose`/`TryEndMatch`가 정리 오류를 던졌다고 이전 입력이 여전히 유효한 것으로 해석하지 않는다. 설정 실패도 rollback하며 설정 오류와 해제 오류를 함께 보존한다. 해제 중 새 Match를 재바인딩하지 않는다. Unity actor/NGO 객체의 실제 파괴는 프레임 끝에 확정되므로 참조 해제와 GameObject 제거 시점을 구분한다.

## 비동기와 실패 처리

`IProfileService.LoadAsync(playerId, cancellationToken)`과 `SaveAsync(profile, cancellationToken)`은 취소 토큰을 명시적으로 받는다. 호출자가 자신의 Scene·화면·App 수명에 맞는 토큰을 소유하고 종료 때 취소한다. 취소 후 완료된 응답을 다른 사용자나 새 Match에 적용하지 않는다. `OperationCanceledException`은 취소 흐름으로, 다른 예외는 실패로 처리한다. 프로필이 없을 때 Load 결과의 null과 통신 실패를 동일시하지 않는다.

현재 Bootstrap의 `FakeProfileService`, `FakePlatformService`는 개발용이며 실 Backend·저장·로그인을 보장하지 않는다. ContentCatalog 에셋이 없으면 Fake fallback을 쓴다. Scene은 실제 UnitySceneFlow와 연결 시 NgoSceneFlow를 사용하며 [Scene API](scene-flow-api.md)의 수명/취소·Host 범위를 따른다. FakeSceneFlow는 검사 전용으로 남긴다. 전체 서비스 구현은 #444에서 선정한다.

## 현재 Network 경계

로컬 운영 화면은 [ScreenManager API](screen-management-api.md)를 따른다. 실제 결과/로딩/메뉴와 Button 경로를 연결했고 기존 전투 HUD는 유지한다. 개인 메뉴는 로컬 입력만 차단하며 게임 시간이나 Host pause를 바꾸지 않는다. 화면 close로 Player 사망·다른 화면·전환 잠금을 해제하지 않는다.

2026-10-26: Match 진행은 `MatchCoordinator`가 실제 `IControlUnitHealthSource`를 구독하고, 같은 Session의 `IMatchProgressRules`에 `MatchProgressSnapshot`을 전달한다. CU HP writer는 Feature에 유지한다. 조립부가 `Start()`를 한 번 호출하고 게임 delta를 `Tick()`에 전달한다. 비동기 gameplay 작업은 `GameplayCancellation`을 사용하고, 네트워크 pump/진단의 unscaled 시계와 혼동하지 않는다. 완료/폐기는 토큰을 취소한다. 동일 outcome의 `Session.Complete` 재호출은 기존 불변 결과를 반환하며 결과/보상 제안을 다시 만들지 않는다. 구독/취소 관측 예외가 나더라도 결과와 다른 관측자의 처리는 보존한다.

#442는 아직 미완료다. `LocalAuthority`는 Core 명령의 Match 상태·참가자만 확인하는 로컬 구현이지 NGO Server 또는 Steam 인증 구현이 아니다. Enemy의 `EnemySpawnExecutor`는 NGO Server에서 실제 NetworkObject를 생성하지만, 이것이 Player·Turret·Enemy 전체의 권한/복제/2인 종료 계약을 완성하지는 않는다.

`CommandId`, 진단 `RequestId`, `NetworkObjectId`는 서로 다른 용도다. 기존 진단의 요청 번호를 정식 `MatchRequestContext.RequestId` GUID와 혼동하지 않는다. 정식 요청 GUID는 발신자·의도와 함께 중복 실행을 막으며 재전송 때 유지한다. 실제 터렛 배치의 `SceneEntityIdentity` 제작 키와 `TurretMatchBinding` 대응표를 공용 명령/공간 입력에서 사용한다. 로컬 InstanceId·SDK NetworkObjectId는 이 논리 ID를 대신하지 않는다.

터렛의 정식 요청/결과·실제 UTP 전송·Host 검증과 지역 전체 상태 복제는 [Core 스프린트 API](core-sprint-region-and-request-api.md)를 따른다. `TurretCommandGateway`는 Client 의도를 Host로 전달하고, 실제 연결 발신자를 검증한 서비스만 FieldTurret 상태를 변경한다. 정식 Scene/HUD 입력·표시 연결과 실제 두 프로세스 검증은 수행했다. Player/UI의 실제 권위 원격 actor 생성/이동 상태 연결과 FieldTurret의 실제 터렛 표현·전투 복제는 여전히 별도이므로 전체 협동 완성을 선언하지 않는다. 개발 actor fixture를 사용한 Core API 증거와 이 연계를 구분하며 Client 위치를 payload에 넣어 거리 검증을 우회하지 않는다.

## PoC 요청과 Editor 경계

`EnemyNetworkTestLauncher`는 현재 PoC adapter다. 자동 `MatchStart`, 게임 `RuntimeUI`, `SceneCollision`, 도구 `DebugWindow` 요청은 현재 Match snapshot을 사용하는 공통 Encounter 경로를 거친다.

`IEncounterDebugCommandTarget.TryRequestCurrentEncounter(preset, max, source, out result)`의 성공 여부와 결과를 확인한다. 수동/반복 Debugger 요청은 테스트 명령이며 출시용 Director·스테이지 진행 규칙이 아니다. `IGameDiagnosticsSource`는 조회/사실 이벤트 계약이다. 구독자는 `DiagnosticEmitted`에서 해제하고, bounded 기록의 손실·N/A를 실제 0으로 바꾸지 않는다.

`AppRoot.BattlefieldSnapshotChanged` 및 `CurrentBattlefieldSnapshot`은 `UNITY_EDITOR` 진단 경로다. 게임 Feature는 조립부가 주입한 session/provider를 사용한다. [Game Debugger 사용법](../tooling/game-debugger-guide.md)은 이 게임 계약과 도구 UI를 구분해 설명한다.

## 환경 조회와 Encounter

[Match 환경 API](match-environment-api.md)를 따른다. `AppRoot.CurrentEnvironment`는 불변 현재 값이다. World 쓰기/타이머는 Host Match lease에, Client는 replica에만 연결한다. 공통 Encounter 요청이 최신 Battlefield와 환경을 권위 측에서 조회하므로 호출자가 Client 날씨/좌표를 권위 상태로 제출하지 않는다. Debugger의 현재 환경과 각 요청의 사용 환경 revision을 구분한다.

## 추가 생성과 source

[origin/source API](encounter-spawn-origin-api.md)의 `IEncounterOriginRequestTarget` 또는 `IEncounterRequestService`를 조립부에서 주입받는다. `AtPoint`, `AtWorld`, `FromSource`를 구분하고 source lease의 등록 generation을 보존한다. 같은 요청 재전송은 GUID를 유지한다. 지연 등록은 실제 생성 성공이 아니며 현재 Match clock과 취소 수명을 따른다. 현재 지면/공중 실제 전이는 명시 미지원이다.

## 2026-10-27 조작 요청 검증 방향

Player 등 호출 파트 → Core/Match의 권한·매치 진행 상태 검증 → FieldTurret 명령 API를 우선 설계 방향으로 삼는다. 이는 회의 당시의 방향 기록이다. 이 통합 브랜치에는 명시적인 Host 요청 gateway와 Feature 실행 경계가 연결돼 있으며 실제 Player/Feature 전체 동기화와 구분한다. 조회 API 이동, 상태의 최종 권한, 실패 응답·동기화 계약은 후속 합의다. Enemy는 실행 로직과 네트워크 전달 계층을 분리하는 중이며 양현석의 기반 권한 작업이 개별 객체 동기화를 모두 대신하지 않는다. [주간 보고](../planning/2026-10-27-weekly-progress.md)
