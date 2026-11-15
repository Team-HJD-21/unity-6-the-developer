# Stage 1 보스 제작 안내

상태: 보스 프리팹에 페이즈 패턴·포효 소환·빨간 원 범위 공격·소환수 보호·탈진 피해 배율을 연결했다. 컴파일과 제작 참조 검사는 실제 Host/Client 플레이 검증과 구분한다.

## 프리팹

`Assets/Prefabs/Enemies/Base/MovingEnemy.prefab` → `Assets/Prefabs/Enemies/Base/BossEnemy.prefab` → `Assets/Prefabs/Enemies/Runtime/Boss/Stage1Boss.prefab` 순서로 상속한다.

Stage1Boss에는 기존 MushroomTreant의 스프라이트·크기·체력·이동·그림자·콜라이더 설정을 복사했다. BossEnemy의 컨트롤러는 `BossEnemyController`, Stage1Boss는 `Stage1BossController`를 사용한다. 각 프리팹에서 컨트롤러는 하나만 실행되며, EnemyNetworkController와 EnemyPlayerSensor의 참조도 교체한 컨트롤러로 연결했다. 일반 몬스터 원본은 변경하지 않았다.

## 컨트롤러와 일반 공격

- `BossEnemyController : MovingEnemyController`: 기존 경로 이동을 재사용하고 선택한 능력의 조건 검사·시작·실행 중 이동 잠금을 처리한다. 능력 종료 시 파생 컨트롤러에 알려 다음 패턴으로 진행한다.
- `Stage1BossController : BossEnemyController`: 현재 페이즈의 정해진 행동 순서를 선택하고 패턴 종료 후 탈진 시간을 관리한다.
- `BossCloseRangeAbility : EnemyCloseRangeAbility`: 기존 근접 피해 계약·발동 사거리·쿨타임을 재사용하고 Animation Event에서 정면 반경 3·각도 180도의 대상을 한 번씩 타격한다.
- `BossChargeAbility`: 직선 예고 후 속도 10으로 최대 8만큼 돌진한다. 실행 중에는 `EnemyAbility.ControlsMovement`로 공통 이동 제동을 건너뛰고 능력이 이동을 담당한다. 다른 능력의 기본값은 false다.
- `EnemySummonAbility`·`EnemySummonGenerator`: 기존 소환 모션·Match 생성 경로를 재사용한다. 보스에 `EnemySummonController`는 붙이지 않으므로 시설 변태와 영구 위치 고정은 실행하지 않는다.
- `BossStompAbility`: 각 원의 시작마다 반경 15 안의 가장 가까운 유효 플레이어를 우선하며, 없으면 현재 전투 타깃 위치를 고정해 예고한다. 분대 타깃을 변경하지 않으며 예고 원에는 콜라이더를 두지 않는다.
- `BossSkillNetworkView`: 포효·땅구르기 모션을 서버 RPC로 전달하고, 원의 위치·반경과 돌진 직선의 양 끝을 NetworkVariable로 동기화한다.
- `BossDamageRule`: 자신의 소환수가 살아 있으면 피해를 줄이고 탈진 중에는 피해를 늘린다. `EnemyHealth`가 HP를 차감하기 전에 공통 `EnemyDamageRule.ModifyDamage()`를 호출한다. 기본 구현은 원래 피해를 유지한다.

실행 흐름은 `EnemyNetworkController → Stage1BossController → 현재 단계의 Ability → 모션 이벤트 또는 예고 종료 → 생성/피해 계약`이다. 일반 공격은 사거리 밖에서 이동하고 안에서는 정지한 뒤 쿨타임이 허용할 때 공격한다. 진행 중인 능력이 끝나기 전에는 다른 능력을 시작하지 않는다. 고정 순서의 능력이 쿨타임 중이면 그 단계에서 대기한다.

타격은 서버에서 한 번만 처리한다. 일반 공격 시작 시 방향을 고정하고 타격 순간의 정면 범위 대상을 조회한다. 준비 중 타깃이 바뀌어도 휘두르는 방향을 돌리지 않으며 동일 대상의 여러 콜라이더는 중복 타격하지 않는다. 사망·능력 비활성화 시 대기 중인 공격을 비운다. 필드 정지 중 받은 이벤트는 재개할 때 처리한다.

## 페이즈와 피해 기믹

체력 70% 이하에서 2페이즈, 35% 이하에서 3페이즈로 넘어간다. 진행 중인 능력 모션이나 이미 시작한 후딜이 끝난 뒤 다음 능력을 선택할 때 반영하며, 전체 사이클 종료까지 기다리지 않는다. 2·3페이즈 진입 시에는 해당 순서의 Roar에서 먼저 시작해 포효·소환으로 전환을 알린다. 이후 사이클부터는 배열의 처음부터 반복하며 회복해도 이전 페이즈로 돌아가지 않는다. 각 페이즈에 근접 압박·원 공격·소환 연계의 여러 행동 묶음을 배치했다.

| 페이즈 | 반복 순서 | 패턴 종료 대기 |
| --- | --- | --- |
| 1 | 돌진·일반 공격 2회 → 짧은 후딜 → 원 1회 → 돌진·일반 공격 1회 | 긴 후딜 2초, 약점 없음 |
| 2 | 돌진·일반 공격 2회 → 짧은 후딜 → 원 2회·일반 공격 1회 → 짧은 후딜 → 포효·2마리 소환 → 돌진·일반 공격 2회·원 2회 | 탈진 2초 |
| 3 | 2연속 돌진·일반 공격 3회 → 짧은 후딜 → 원·가시 8회 → 포효·4마리 소환 → 2연속 돌진·일반 공격 2회·원·가시 8회 | 탈진 3초 |

`phase1Pattern`·`phase2Pattern`·`phase3Pattern` 배열로 Inspector에서 순서를 조절한다. Attack·DoubleAttack·TripleAttack은 각각 일반 공격 1·2·3회이며, 각 공격 모션이 끝난 뒤 기존 쿨타임과 타깃 사거리를 확인해 다음 공격을 시도한다. 대상이 멀어지면 추적하므로 고정 위치에서 연타하지 않는다. Stomp·DoubleStomp는 원을 1·2회 예고한다. 3페이즈의 StompBarrage는 `phase3StompCount`만큼 연속 예고하고 타격마다 가시를 재생하며 기본값은 8회다. 기존 TripleStomp의 직렬화 번호 10을 유지해 프리팹 패턴 순서는 보존했다. Charge·DoubleCharge는 돌진 1·2회이며 이미 근접 거리라면 돌진 단계를 건너뛰고 후속 공격으로 넘어간다.

돌진은 0.7초 동안 시작 방향을 고정해 직선으로 예고하고 진행하며 매 물리 스텝에서 Foot Collider Cast와 NavMesh 경계를 검사한다. 지형에 막히거나 최대 거리·시간에 도달하거나 타깃과의 몸통 간격이 0.8 이하가 되면 멈추고 1초 후딜을 둔다. 두 번째 돌진은 방향을 다시 정하고 0.45초 예고한다. 충돌 마스크는 Default·Water·Forest·TurretBase이며 기존 Move 모션을 사용한다. 돌진 자체의 추가 피해는 없고 기존 접촉 피해와 후속 휘두르기를 사용한다. 공통 EnemyMovement의 이동·감속·SlowEffects 코드는 변경하지 않는다.

Recovery는 행동 묶음 사이의 짧은 후딜로 1·2페이즈 0.6초, 3페이즈 0.4초이며 약점을 열지 않는다. Rest는 큰 사이클의 마지막에만 둘 수 있고, 2·3페이즈의 Rest 동안에만 탈진 약점을 연다. 빈 배열·후딜만 있는 배열·중간 Rest·마지막 Rest 누락은 잘못된 제작 설정으로 실행을 막는다.

각 근접 공격은 시작 대기 상한 `attackWaitTimeout`을 사용한다. 기본 3초 동안 시작하지 못하면 남은 연타를 생략하고 다음 행동 묶음으로 넘어가므로 플레이어를 계속 쫓느라 원 공격·포효가 막히지 않는다. 실행 중인 타격 모션은 중단하지 않고, 연타의 다음 공격을 기다릴 때는 시간을 새로 부여한다. 이전 능력의 완료 알림을 소비한 후 페이즈를 선택해 새 포효가 완료 알림에 의해 건너뛰어지지 않게 한다.

- 보스가 직접 소환해 실제 생성에 성공한 몬스터만 보호 기믹에 포함한다. 생성 성공 콜백으로 등록하고 기존 사망 확정·Despawn 이벤트로 해제한다. 전체 몬스터를 매 프레임 검색하지 않는다.
- 한 마리라도 생존하면 받는 피해를 50% 줄인다. 여러 마리가 살아 있어도 중첩하지 않는다. 마지막 소환수 사망 시 페이드가 끝나기 전에 보호가 해제된다.
- 탈진 중 받는 피해는 150%다. 보호와 탈진이 동시에 적용되면 두 배율을 곱해 원래 피해의 75%를 받는다. 양수 공격의 최소 피해는 1이다.
- 소환 주체가 제거되면 이벤트 구독을 정리하고 살아 있는 소환수는 유지한다. 다른 소환사의 소환수와 일반 스폰 몬스터는 보호 집계에 포함하지 않는다.
- 보스의 제작 초기값은 Slime을 2페이즈에 2마리, 3페이즈에 4마리 요청하며 생존 상한은 6마리, 사망 소환은 0마리다. Stage1BossController의 `phase2SummonCount`·`phase3SummonCount`를 패턴 시작 시 기존 소환 능력에 전달한다. 진행 중인 포효의 수량은 변경하지 않는다. 남은 수용량이 적으면 요청 수보다 적게 생성할 수 있으며, 기존 소환사의 생존 상한 기본값 0은 제한 없음이다.
- 체력 구간·탈진 시간·피해 배율·소환 종류/수량/상한은 각 컴포넌트의 Inspector에서 조정한다. 이 값은 플레이 밸런싱 전 초기 설정이다.

### 보스 초기 스탯

플레이어 1명 기준의 첫 밸런싱용 설정이며 실제 전투 시간의 검증 결과는 아니다. 일반 트렌트와 공통 부모 대신 Stage1Boss 프리팹에 값을 지정한다.

| 항목 | 초기값 |
| --- | --- |
| 최대 체력 | 6,000 |
| 이동 속도 / 가속도 | 3 / 15 |
| 일반 공격 피해 / 쿨타임 / 사거리 | 40 / 1.8초 / 0.8 |
| 접촉 피해 | 20 |
| 일반 공격 범위 반경 / 정면 각도 | 3 / 180도 |
| 돌진 속도 / 최대 거리 / 쿨타임 | 10 / 8 / 6초 |
| 빨간 원 피해 / 반경 | 70 / 3 |
| 원 예고 시간 1·2·3페이즈 | 1.2 / 1 / 0.8초 |

체력 4,200 이하에서 2페이즈, 2,100 이하에서 3페이즈 조건을 충족하며 실제 전환은 진행 중인 능력·후딜 이후의 다음 선택에서 이루어진다.

## 빨간 원 범위 공격

별도 분출 에셋 대신 `StompWarning`의 LineRenderer 테두리와 자식 `Fill`의 원판 MeshRenderer로 반경 3의 빨간 원을 표시한다. 안쪽은 빨간색·알파 0.22로 채워 바닥을 볼 수 있게 하며, `BossSkillNetworkView.warningFillColor`에서 조절한다. 예고 시간은 1·2·3페이즈에서 각각 1.2·1·0.8초다. 필드 시각을 사용하므로 필드 정지 중에는 피해 시각이 진행되지 않는다. 예고 시작 후 원은 타깃을 따라가지 않고 고정된다. 연속 원은 이전 타격 후 가까운 유효 플레이어 위치를 다시 읽어 표시한다. 플레이어가 없으면 현재 전투 타깃을 사용하며 전투 타깃을 잃었다면 남은 원은 생략한다.

피해 발생 순간에만 `Physics2D.OverlapCircle`로 지정 레이어를 조회하고 동일한 TargetableComponent는 한 번만 맞힌다. 제작 마스크는 PlayerHurtbox·Turret·CU·TurretBase이며 센서 레이어는 제외한다. 대상의 실제 콜라이더 레이어가 바뀌면 함께 조정해야 한다. 타격과 Stomp 모션이 모두 끝나야 다음 패턴으로 넘어간다. 사망·능력 비활성화·네트워크 제거 시 예고 표시를 정리한다.

예고 종료 시 빨간 원을 숨기고 같은 위치에 `Treant_GroundRupture_SpikesOnly_24Frames.png`의 가시를 재생한다. `BossStompAbility → BossSkillNetworkView.PlayGroundRupture() → 서버 RPC → BossGroundRuptureVisual` 경로로 Host·Client에 표시하며, 가시에는 별도 피해나 Collider를 두지 않는다. 최초 투명 2프레임을 제외한 02~23번을 초당 24프레임으로 약 0.92초 재생한 뒤 숨긴다. 기존 24개 Sprite 슬라이스와 GUID는 유지한다.

보스 프리팹에 비활성 SpriteRenderer 3개를 연결해 연속 원의 가시를 순환 재사용한다. 표시 중 Instantiate·Destroy나 개별 NetworkObject 생성은 없다. 가시의 월드 위치와 크기는 부모 보스의 이동·배율과 분리하며, 최대 가시 폭 1.73을 기준으로 피해 지름에 맞춘다. `framesPerSecond`와 `spriteDiameter`로 재생 속도와 외형 배율을 조절한다. 재생 중에는 추가 피해가 없으며 늦게 접속한 Client에 이미 끝난 타격 연출을 재생하지 않는다. 이펙트 종료·네트워크 제거 시 렌더러를 숨긴다.

예고 원 머터리얼은 `Assets/Art/Materials/Enemy/Stage1BossWarning.mat`, 채움 원판은 `Assets/Art/Meshes/Enemy/Stage1BossWarningDisk.asset`다. 원의 월드 반경은 보스 프리팹의 확대 스케일과 분리하고 선은 상태 변경 때만 다시 계산한다. 채움 원도 동일한 반경을 사용하며, 표시 중에는 LateUpdate에서 부모 확대와 이동을 상쇄해 보스가 밀려도 원의 위치가 유지된다. 메시와 머터리얼을 개체마다 새로 만들지 않으며, 채움 색상은 MaterialPropertyBlock으로 적용한다.

## 전용 Animator와 모션

프리팹의 Animator는 `Assets/Art/Animations/Enemies/Runtime/Boss/Stage1Boss/Stage1Boss.controller`를 사용한다. 일반 트렌트의 AnimatorOverrideController와 분리되어 있다.

| 상태 | 원본 모션 | 재생 요청 | 방향 |
| --- | --- | --- | --- |
| Idle | MushroomTreant Idle | 기본 상태 | Down / Up / Left / Right |
| Move | MushroomTreant Walk | IsMoving = true | Down / Up / Left / Right |
| Attack | Mushroom_Treant_Attack_2 | Attack Trigger | Down / Up / Left / Right |
| Stomp | Mushroom_Treant_Attack_1 | Stomp Trigger | Down / Up / Left / Right |
| Roar | Mushroom_Treant_Ability | Roar Trigger | Down / Up / Left / Right |

- 각 상태는 MoveX·MoveY로 방향을 선택하는 BlendTree를 사용한다.
- Move는 기존 MoveAnimSpeed 파라미터로 재생 속도를 조절한다.
- 클립 20개는 보스 전용 폴더에 저장했다. 스프라이트 텍스처는 기존 에셋을 참조한다.
- 공격·땅구르기·포효 클립은 반복하지 않으며, 재생이 끝나면 Idle로 복귀한다. IsMoving이 true이면 이후 Move로 전환한다.
- 일반 공격 4방향 클립에는 `OnBossMeleeHit`를 2/12초 지점에, `OnBossMeleeAttackFinished`를 클립 종료 직전에 연결했다. 실제 재생 시간은 Animator 상태 속도의 영향을 받는다.
- Roar 4방향 클립에는 현재 재생 속도에서 시작 약 0.5초가 되는 지점의 `OnSummonAnimationEvent`와 종료 직전의 `OnSummonAnimationFinished`를 연결했다.
- Stomp 4방향 클립에는 종료 직전의 `OnStompAnimationFinished`를 연결했다. 범위 피해 시점은 클립 이벤트가 아닌 필드 시각의 예고 종료 시점이다.
- 특수 모션이 너무 짧게 보이지 않도록 Animator의 Roar 상태는 약 1.5초, Stomp 상태는 약 1.2초에 재생되게 속도를 낮췄다. 배속 1 기준이며, 포효 소환 이벤트는 시작 약 0.5초에 실행되고 종료 이벤트는 각 모션 끝 직전에 유지된다. Animator의 각 상태 Speed에서 길이를 조정한다.

## 현재 확인 방법과 한계

Animation 창에서 Stage1Boss의 각 방향 클립을 선택해 미리보기로 외형과 모션을 확인한다. Animator에는 Attack·Stomp·Roar Trigger가 준비되어 있다.

보스도 기존 EnemySpawnExecutor가 소유한 개체여야 소환을 실행할 수 있다. 단순히 Scene에 프리팹만 배치하거나 NetworkObject만 Spawn하면 소환 부모의 Match 생성기를 찾지 못한다.

`Assets/Data/Enemies/Spawning/Definitions/Stage1Boss.asset`를 기존 `EnemyCatalog.asset`에 등록했다. Enemy Test 창에서 연결·카탈로그를 새로고침한 뒤 개별 몬스터 종류 `Stage1Boss`를 선택하고 초기 타깃을 지정해 1마리 생성한다. 이 경로는 기존 생성기 소유와 Match 연결을 유지하므로 포효 소환도 같은 생성 경로를 사용한다. 보스는 독립 분대이며 자동 검색은 시설을 대상으로 하고 플레이어 감지 전환을 허용한다. 비용 2는 기존 MushroomTreant를 따른 초기값이고 개별 기능 시험은 예산을 적용하지 않는다.

일반 스쿼드 프리셋에는 보스를 추가하지 않았다. 실제 최종 Scene의 보스 진입 연결은 별도로 확인해야 한다.

수동 검증 시에는 다음을 확인한다.

- Host에서 일반 공격 타격 시점·정면 범위 이탈·공격 종료 후 재개를 확인한다.
- 돌진 예고 중 옆으로 회피할 수 있는지, 벽·NavMesh 경계에서 멈추는지, 3페이즈 두 번째 돌진과 원·가시 8회가 이어지는지 확인한다.
- 체력을 구간 아래로 내리고 현재 모션·후딜 종료 후 2·3페이즈 진입 포효가 실행되는지 확인한다. 타깃을 따라가던 중에도 전체 사이클을 기다리지 않고 전환돼야 한다.
- 소환수 생존·전멸·탈진을 조합해 동일한 원본 피해가 50%·100%·75%·150%로 적용되는지 확인한다.
- 생존 소환수가 6마리를 넘지 않는지, 생성 실패는 보호 집계에 남지 않는지 확인한다.
- 예고 중 범위에서 벗어나면 맞지 않고, 여러 콜라이더를 가진 대상도 한 번만 맞는지 확인한다.
- Client에서도 포효·땅구르기 방향과 원 표시·제거를 확인하고 보스 사망 후 추가 피해가 없는지 확인한다.
- 필드 일시 정지·재개 중 예고 대기 시간이 유지되는지 확인한다.

소환 주체 관계는 기존 생성 지점 기록의 `summon/부모 EntityId`를 사용해 복원한다. 모든 적을 생성한 뒤 `ResolveCheckpointReferences()`에서 소환수를 부모의 `EnemySummonGenerator`에 다시 등록하므로 생존 수·피해 감소·소환 상한과 사망/제거 이벤트 구독을 유지한다. 복원 순서와 새 NGO ID에 의존하지 않으며 롤백도 동일한 연결 경로를 사용한다. 부모가 저장에 없는 경우 소환수만 독립적으로 유지하고 다른 소환사에게 배정하지 않는다. 저장 버전과 payload 필드는 변경하지 않는다. `EnemySummonCheckpointTests`는 이 연결과 중복 등록·사망·제거·새 네트워크 ID·상한 차단을 검증하는 EditMode 회귀 테스트다. 실제 저장/로드·Host 플레이 검증과 구분한다.

보스의 페이즈·패턴 단계·탈진 시간·능력별 쿨타임의 전체 체크포인트 저장/복원은 아직 지원하지 않는다. 능력 진행 중 캡처는 기존 보스 검사에서 재시도 예외를 반환하지만, 모션이 끝났다는 이유만으로 전체 보스 상태가 복원된다고 판단하면 안 된다.

소환수 보호·탈진 약점을 플레이어에게 알리는 전용 시각 표시는 후속 작업이다. 빨간 원과 특수 모션의 동기화가 약점 표시를 대신하지는 않는다. Play Mode와 Test Runner는 사용자 검증으로 남겨둔다.
