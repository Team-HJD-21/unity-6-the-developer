# Core API 호출과 소유권 규약

기준일: 2026-10-15 · 상태: 현재 작업 브랜치 기준 · 관련 Issue: #445, #442, #495, #496, #526–#528

팀원이 Player·Turret·Enemy를 Core에 연결할 때 사용할 계약과 종료 책임을 정리한다. 명명 규칙은 [Convention](naming-and-architecture-conventions.md), 타입·실행 흐름은 [Core README](../../Assets/Scripts/Core/README.md)를 따른다. 아래의 공개 계약이 모두 출시용 게임 규칙을 구현했다는 뜻은 아니다.

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

## 비동기와 실패 처리

`IProfileService.LoadAsync(playerId, cancellationToken)`과 `SaveAsync(profile, cancellationToken)`은 취소 토큰을 명시적으로 받는다. 호출자가 자신의 Scene·화면·App 수명에 맞는 토큰을 소유하고 종료 때 취소한다. 취소 후 완료된 응답을 다른 사용자나 새 Match에 적용하지 않는다. `OperationCanceledException`은 취소 흐름으로, 다른 예외는 실패로 처리한다. 프로필이 없을 때 Load 결과의 null과 통신 실패를 동일시하지 않는다.

현재 Bootstrap의 `FakeProfileService`, `FakePlatformService`, `FakeSceneFlow`는 개발용이며 실 Backend·저장·로그인을 보장하지 않는다. ContentCatalog 에셋이 없으면 Fake fallback을 쓴다. 전체 서비스 구현은 #444에서 선정한다.

## 현재 Network 경계

#442는 아직 미완료다. `LocalAuthority`는 Core 명령의 Match 상태·참가자만 확인하는 로컬 구현이지 NGO Server 또는 Steam 인증 구현이 아니다. Enemy의 `EnemySpawnExecutor`는 NGO Server에서 실제 NetworkObject를 생성하지만, 이것이 Player·Turret·Enemy 전체의 권한/복제/2인 종료 계약을 완성하지는 않는다.

`CommandId`, 진단 `RequestId`, `NetworkObjectId`는 서로 다른 용도다. RequestId는 요청과 생성 이벤트의 상관 ID이며 PlanId/재시도 중복 방지 토큰이 아니다. Turret 공간 입력의 instance 기반 ID도 저장·네트워크 ID가 아니다. Host/Client 공통 명령 전송·ID 매핑·상태 복제·소유권·중복 방지 규칙은 #442 및 E의 #459에서 합의할 미결 계약이다. 이 가이드는 네트워크 완성을 선언하지 않는다.

## PoC 요청과 Editor 경계

`EnemyNetworkTestLauncher`는 현재 PoC adapter다. 자동 `MatchStart`, 게임 `RuntimeUI`, `SceneCollision`, 도구 `DebugWindow` 요청은 현재 Match snapshot을 사용하는 공통 Encounter 경로를 거친다.

`IEncounterDebugCommandTarget.TryRequestCurrentEncounter(preset, max, source, out result)`의 성공 여부와 결과를 확인한다. 수동/반복 Debugger 요청은 테스트 명령이며 출시용 Director·스테이지 진행 규칙이 아니다. `IGameDiagnosticsSource`는 조회/사실 이벤트 계약이다. 구독자는 `DiagnosticEmitted`에서 해제하고, bounded 기록의 손실·N/A를 실제 0으로 바꾸지 않는다.

`AppRoot.BattlefieldSnapshotChanged` 및 `CurrentBattlefieldSnapshot`은 `UNITY_EDITOR` 진단 경로다. 게임 Feature는 조립부가 주입한 session/provider를 사용한다. [Game Debugger 사용법](../tooling/game-debugger-guide.md)은 이 게임 계약과 도구 UI를 구분해 설명한다.
