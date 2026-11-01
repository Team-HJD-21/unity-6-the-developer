# Match 필드 정지와 참가자 출입

갱신: 2026-11-02. 권위 Scene의 위치 등록·시계·필드 Feature 연결 구현. 실제 원격 Player 생성·Client의 출입 요청/시각 복제와 허브 왕복 인수는 별도 검증한다.

## 규칙과 소유권

싱글은 참가자가 우주선에 들어가면 필드를 정지한다. 2인 Match는 등록된 참가자 모두 내부에 있을 때만 정지한다. 한 명이라도 필드에 있거나 실제 actor가 아직 등록되지 않았으면 계속 진행한다. actor 등록 해제는 참가자를 roster에서 삭제하지 않으며, 미확인 참가자 때문에 필드 전체가 잘못 멈추는 것을 막는다. 현재 Match roster는 생성 시 확정된다. 실제 SOS 도중 참가·Host Migration을 구현했다고 해석하지 않는다.

Host의 `MatchPresenceService`가 위치 원본과 판정을 소유한다. `PlayerMatchPresence`는 실제 Host actor에 임대한 보고권이다. Client가 `insideShip=true`를 전송하는 API가 아니며, 외부에서 받은 좌표/상태를 그대로 기록하지 않는다. 미래 Client 출입 명령도 서버가 actor·입구 접근·이동 성공을 검증한 뒤 같은 등록을 사용해야 한다.

`Time.timeScale=0`으로 허브까지 멈추지 않는다. `MatchTimeService.SetPaused`가 게임 시계를 동결하고, 같은 시계를 받은 Enemy·FieldTurret·Player의 필드 생성물만 진행을 중지한다. 우주선의 Player 이동·UI와 NGO 서버 시각·통신은 계속 실행할 수 있다. Game Debugger의 **Editor Pause**는 Editor 전체 실행 정지이며 이 정책과 다른 기능이다.

## 실제 호출 흐름

```mermaid
sequenceDiagram
    participant Root as InitialStageCompositionRoot
    participant Scope as MatchFieldSceneScope
    participant Portal as SpaceshipEnter/Exit
    participant Actor as PlayerMatchPresence
    participant Presence as MatchPresenceService
    participant Clock as MatchTimeService
    participant Field as 필드 Feature/FieldSimulationActor
    Root->>Scope: 공용 시계·gameplay 취소·Feature·실제 actor 전달
    Scope->>Presence: MatchConfig 참가자로 생성
    Scope->>Actor: 참가자 위치 등록 임대
    Scope->>Field: BindFieldSimulation(time, gameplay)
    Portal->>Portal: Host actor 확인·목적지 이동 성공
    Portal->>Actor: Report(Spaceship 또는 Field)
    Actor->>Presence: 등록 세대·권위 확인 후 위치 기록
    Presence->>Clock: 전원 우주선 여부가 달라지면 SetPaused
    Field->>Clock: 같은 게임 시간·정지 상태 읽기
    Note over Field: AI·공격·쿨다운·물리·애니메이션 정지/재개
```

`InitialStageCompositionRoot`가 시계를 프레임 앞에서 갱신한다. Match 시작 후 `MatchFieldSceneScope`가 Feature 바인딩과 위치 등록을 소유한다. `MatchPresenceService.Locations`는 메인 스레드 진단용 읽기 뷰이며 네트워크 패킷이나 영속 snapshot이 아니다.

| 호출자 | API | 시점·결과·수명 |
| --- | --- | --- |
| Core Scene 조립부 | `new MatchFieldSceneScope(...)` | 현재 Host Match가 시작한 뒤 한 번 생성. Feature lease와 위치 등록을 Match/Scene 해제 시 정리 |
| Player의 Host 생성자 | `InitialStageNetworkBinding.RegisterAuthorityPlayerActor(matchId, playerId, actor)` | 실제 원격 actor가 준비된 뒤 호출. 터렛 상호작용과 위치 보고 등록을 함께 임대. 교체/Despawn 전에 Dispose |
| Host 출입 어댑터 | `PlayerMatchPresence.Report(location)` | 실제 이동 성공 후 호출. 종료된 Match·해제된 등록은 거부 |
| Scene의 필드 Feature | `IFieldSimulationConsumer.BindFieldSimulation(time, gameplay)` | 공용 시간 읽기와 종료 취소를 받음. 반환 lease를 Feature의 원본 소유자가 해제 |
| 적/탄환/설치물 생성자 | `FieldSimulationActor.Attach(actor, time, gameplay, destroyOnCompletion)` | 생성 직후 연결. 생성자에서 자식 생성물로 시계와 취소를 명시적으로 전파. 전역 Scene/Singleton 조회 없음 |
| Player 필드 생성자 | `PlayerFieldSimulationUtility.BindSpawned(spawned, owner)` | Player 또는 기존 필드 개체를 명시적으로 전달해 그 소유자의 시계/취소만 상속 |

로컬 Host Player는 조립부가 이미 등록한다. 동일 참가자에 추가 actor를 중복 등록하면 실패한다. 실제 actor 교체는 기존 lease 해제 후 새 actor를 등록한다. 오래된 lease가 새 등록을 해제하지 않도록 등록 세대를 비교한다.

## Feature별 연결

- **Enemy**: Encounter consumer가 실제 `EnemySpawnExecutor`에 시계를 전달한다. 기존 적과 이후 생성 적 모두 바인딩한다. 서버 행동/공격과 분대 타깃 갱신을 정지한다. Player 장치가 AI를 임시 제어하는 `PlayerFieldEnemyAdapter`도 같은 필드 정지를 따른다.
- **FieldTurret**: `FieldTurretController`가 전력 회복·자동 활성화의 진행을 멈춘다. 등록/해제 이벤트를 통해 추가·교체 터렛을 같은 시계에 연결한다. 냉각과 자동 활성화 기한은 Match 게임 초를 사용한다. 과열·활성 상태 자체를 정지 이유로 변경하지 않는다.
- **투사체**: 캐논·미사일·Player 탄환과 보조 포탑 탄환은 생성 때 같은 시계를 받는다. 이동·충돌 실행·수명·폭발 지연이 필드 정지를 따른다. Match 완료 취소 시 임시 생성물을 정리해 폐기된 시계를 계속 읽지 않는다.
- **Player 필드 효과**: 폭탄 도화선·폭발, 배리어·비콘·임시 터렛의 진행과 관련 쿨다운을 정지한다. 이미 존재하는 필드 효과는 참가자 개인 위치보다 공유 필드 시계를 따른다. 내부 Player의 필드 공격은 차단하지만 이동·HUD·스킬 선택 UI를 전역 비활성화하지 않는다.
- **Weather**: 기존에 이관한 scheduler가 동결된 시계를 사용하므로 날씨 예약도 멈춘다. 복귀 시 정지 구간을 밀린 날씨 사건으로 실행하지 않는다.

`FieldSimulationActor`는 Rigidbody2D의 `simulated`와 속도, Animator 속도, 실행 중 ParticleSystem을 보존했다가 복구한다. `enabled`나 `NetworkObject`를 끄지 않으므로 네트워크 생명주기나 AI 제어 lease를 대신하지 않는다. 정지 중 같은 물리/Animator 속성에 별도의 writer를 추가하려면 이 바인딩과 소유권을 맞춰야 한다. 생성 뒤 자식 물리 개체를 추가하는 Feature는 해당 생성물에도 명시적으로 바인딩한다.

`SpaceshipExit`는 다른 참가자가 내부에 있을 수 있어 출입 때 공용 우주선 Scene을 무조건 Unload하지 않는다. 실제 Hub Scene 해제·재접속/이동 명령의 완료 처리는 게임 사이클의 Scene 소유자가 맡는다.

## 확인 경로와 현재 한계

Game Debugger의 **Overview → 필드 시간 · Host 권위**에서 공용 시계의 준비/정지·배속과 참가자별 Field/Spaceship을 읽는다. 실제 출입 이후 필드 상태가 바뀌는지 확인한다. 개인 메뉴와 Editor Pause로 우주선 출입을 대신하지 않는다.

초기 결합 검사 19/19가 통과했다. 생성물 종료 수명·Player 필드 효과 연결 후 확장 검사에서는 91개 중 84개가 통과했고 7개가 실패했다. 원인을 수정한 후 실패 범위와 실제 필드 정지를 포함한 10개 집중 검사가 모두 통과했다. 이를 한 번의 91/91 성공 실행으로 표시하지 않는다. 실제 원격 Player 생성·포털 왕복 검증을 대신하지 않는다.

실패 원인은 Additive Player 소속, EditMode runner에서 일반 `WaitForSeconds`/`WaitForFixedUpdate`를 실제 대기로 가정한 fixture, private 비직렬화 필드의 SerializedObject 조회, 스폰 시작 전 서비스 이력을 0으로 가정한 비교식이었다. 생성 정책이나 제품 밸런스를 테스트 통과 목적으로 변경하지 않았다.

남은 통합: Client의 읽기용 시간 anchor 복제, 실제 원격 Player 생성자/출입 명령 연결, Additive Hub의 다중 참가자 이동·왕복 검증. 위치 보고 API를 직접 호출한 Scene 검사는 포털 연출·실제 두 프로세스 플레이 검증을 대신하지 않는다.
