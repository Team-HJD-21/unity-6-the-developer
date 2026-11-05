# Scene 전환과 로컬 입력 잠금

기준일: 2026-11-05. 현재 작업 브랜치 기준. [실행 증거](../planning/2026-10-26-core-sprint-implementation.md)와 [관리 책임](match-management-responsibilities.md)을 함께 읽는다.

## 호출 경계

조립부가 `SceneFlowController`와 `IInputBlocker`를 필요한 UI/Feature에 주입한다. AppServices는 내부 composition container다. Unity 조립 진입점은 `AppRoot.SceneTransitions` / `AppRoot.LocalInput`이며 새 전역 Instance나 매 프레임 AppRoot 검색을 만들지 않는다.

| API | 의미 |
| --- | --- |
| `LoadAsync(scenePath, token)` | 임시 Additive 로드. 이미 로드됐다면 다시 소유/제거하지 않음 |
| `ReplaceAsync(scenePath, token)` | Single 교체. 같은 경로면 실제 Scene 재로드 |
| `UnloadAsync(scenePath, token)` | 명시적 Scene 해제. 없으면 무해 |
| `CancelCurrent()` | 취소 가능한 Additive 작업에만 요청. 네이티브 종료/복구까지 기다림 |
| `IsTransitioning`, `TargetScene`, `LastFailure` | 전환 중/목표/실제 실패 조회 |
| `Progress` | Unity 로더는 실제 진행, NGO 전체 완료 대기는 `NaN`(미제공). UI는 부정확한 백분율을 만들지 않음 |
| `SetSceneFlow(adapter)` | composition 전용. 진행 중이면 다음 작업부터 적용 |
| `IInputBlocker.Acquire(owner)` | 소유자별 IDisposable 잠금. 자기 token만 해제 |

Scene 경로는 `Assets/Scenes/.../*.unity`의 Build Settings 등록 경로를 사용한다. 사전 검사/중복 전환 거부 뒤 잠금을 잡고, 성공·실패·취소 정리가 실제로 끝나면 해제한다. `LastFailure`와 취소는 구분한다.

## 취소와 commit

Unity AsyncOperation은 시작 뒤 즉시 취소되지 않는다. Additive 취소는 완료를 기다린 뒤 **이 요청이 새로 로드한 Scene만** 해제한다. 그동안 입력 잠금은 유지한다. 이미 로드된 Scene은 다른 소유자의 자원으로 보고 지우지 않는다.

Single 교체/Unload는 시작 이후 되돌릴 수 있는 작업으로 표시하지 않는다. 시작 전 취소는 거부하고, 시작 후 늦은 취소보다 실제 commit이 우선한다. `CancelCurrent`는 이 단계에서 false다. 이전 게임 상태를 복구했다고 주장하지 않는다. App 종료는 남은 작업을 취소 요청하며 소유 자원을 정리한다.

## Match와 NGO

- SceneFlowController는 승패/보상 판단을 하지 않는다. 게임 흐름이 먼저 불변 결과를 보존하고 `NetworkMatchCoordinator.TryEnd()` 또는 로컬 Match 종료를 명시한 뒤 Replace를 호출한다.
- AppBootstrap은 실제 `UnitySceneFlow`를 사용한다. NGO 조립 뒤에는 `NgoSceneFlow`를 다음 전환 driver로 붙인다. 연결 중 Scene NetworkObject 수명은 NGO의 Scene 로더로 관리한다.
- NGO 전환은 **Host가 결정하는 1~2인 공유 경로**다. 고정 참가자 전원의 Scene 완료와 Feature 준비를 각각 기다린다. Client는 로드/재시작을 독자 결정하지 않는다. 전환 중 접속 집합 변경·timeout은 실패다.
- 단독 개발 진입은 SinglePlayerHost 정책을 유지한다. `GameFlow.LaunchMissionAsync`는 Host의 명시 출격 의도 안에서 공유 Scene 완료 후 현재 참가자1~2인을 준비하고 전원 응답 뒤 새 Match를 시작한다.
- 기존 SpaceshipEnter/Exit의 임시 Additive Hub 왕복은 그대로다. 실제 이동 성공 후 위치 보고는 김진태가 연결한다. 전원 내부일 때만 필드가 정지하며, 한 사람이 내부여도 다른 사람이 필드에 있으면 계속 진행한다.

## 로컬 입력

`InputLockService`는 App 수명의 소유자별 token을 보관한다. PlayerControlState에는 `ILocalInputConsumer`로 주입하며 기존 Feature의 `_uiBlocked`·사망/Match 권한과 **OR** 결합한다. 화면 하나를 닫아 다른 화면/로딩 잠금이나 사망 상태를 풀지 않는다. 게임 TimeScale/Host pause를 변경하지 않는다.

## 제작 Scene 관찰

최신 main Initial_Stage에는 `Encounter Debug Spawner`와 `EnemySpawnRoot` 두 Executor가 함께 제작돼 있다. B8의 `EncounterSceneConfiguration`은 EnemySpawnRoot prefab 인스턴스를 실제 실행 대상으로 명시 연결한다. 재로드 시 persistent consumer에 새 Scene 참조를 전달한다. 다른 제작 Executor는 삭제하지 않았으며 검사는 이전 객체 모두의 폐기·새 Scene 제작 개수/소유 Scene·명시 대상과 실제 생성까지 확인한다. [origin/source API](encounter-spawn-origin-api.md) 참조.


## Additive Scene의 Player 소속 (2026-11-02)

`LocalPlayerActorSpawner.Spawn`은 실제 `PlayerStart`의 Scene에 생성 actor를 귀속시킨다. 활성 Scene이 Hub여도 행성의 PlayerStart에서 생성한 Player는 행성 Scene에 속한다. 부모 Transform을 전달하면 부모도 같은 Scene이어야 하며 불일치는 생성 전에 거부한다. 이는 actor의 생성 소속이며, 진행 중 우주선에 들어갈 때 actor를 다른 Scene으로 이동시키는 정책은 아니다.

실제 빈 Scene에서 Initial_Stage를 Additive 로드하는 탄환 통합 검사에서 기존 HUD의 actor 소속 불일치를 발견해 수정했다. 이후 서버 적 생성과 실제 탄환 피해까지 통과했다. 포털 이동·다중 참가자 Scene 교체 완료 검증과 구분한다.

### 준비 중 취소의 Match 소유권

일반 출격과 슬롯 복원 모두 Scene 파일 로드만으로 성공하지 않는다. `AppGameFlowHost.WaitForMissionReadyAsync`가 새 Match 준비를 기다리고, 시작 직후 취소/준비 실패는 `AppRoot.EndFailedMissionArrival(expectedMatchId)`로 그 입장이 만든 Match만 정리한다. 실패한 화면 상태만 남기고 적/Match를 계속 돌리지 않는다. 결과 전송이 보류된 Match나 다른 ID를 강제로 종료하지 않는다. 실제 일반 출격 취소 재현과 파일 입장4개를 포함한 최종 집중 검사5/5로 확인했다.
