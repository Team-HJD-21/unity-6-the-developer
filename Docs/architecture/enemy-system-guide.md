# Enemy 시스템 가이드

수정일: 2026-11-03  
상태: 현재 Enemy 코드 구조 참고 · 런타임 밸런스 및 통합 검증 결과를 확정하지 않음

[문서 목차](../README.md) · [2026-10-02 AI 구조 검토](../planning/2026-10-02-ai-architecture-review.md) · [레거시 몬스터 흐름 분석](../archive/legacy-monster-flow-and-dependencies.md)

## 목적과 범위

현재 Enemy 구현은 타깃 지정, 이동, 애니메이션, 능력 실행을 나눠 조합하는 구조다. 이 문서는 새 Enemy 행동과 프리팹을 추가할 때 참고할 코드 경계를 설명한다. 몬스터 종류·분대 편성·밸런스 수치나 Stage 1 재미 검증 결과를 확정하지 않는다.

## 런타임 흐름

1. `EnemySpawnExecutor`는 정의된 프리팹을 네트워크에 스폰하고 Enemy를 `EnemySquad`에 등록한다.
2. `EnemySquad`가 분대 단위로 목표를 선택·유지하고, 목표가 바뀌면 구성원 `EnemyController.AssignTarget()`에 같은 Transform을 전달한다.
3. `EnemyController`는 공통 타깃·애니메이션·능력 참조와 타깃 변경 진입점을 제공한다. 유형별 행동은 하위 컨트롤러가 구현한다.
4. 네트워크 게임에서는 스폰된 서버 인스턴스의 `EnemyNetworkController.FixedUpdate()`가 `ExecuteBehavior()`를 호출한다. 위치와 행동의 권한은 서버에 있으며 Animator 결과는 네트워크 애니메이터가 전달한다.
5. 오프라인 테스트 씬에서는 `EnemyTestDriver`가 타깃을 지정하고 행동을 실행한다. 같은 Enemy에 이 드라이버와 `EnemyNetworkController`를 동시에 붙여 행동을 중복 호출하지 않는다.

## 컴포넌트 책임

| 컴포넌트 | 책임 |
| --- | --- |
| `EnemyController` | 타깃·애니메이션·능력 공통 참조, `AssignTarget`, 행동 실행 계약 |
| `MovingEnemyController` | 능력 조건에 따라 정지·능력 사용 또는 경로를 따라 이동 |
| `EnemyTargeting` | 타깃 Transform과 그 자식 Collider 목록 보관 |
| `EnemyMovement` | 목적지 경로 설정, 다음 이동 방향 제공, Rigidbody2D 이동·정지 |
| `EnemyAnimation` | 방향·이동 애니메이션과 능력 애니메이션 재생 |
| `EnemyAbility` | 능력 조건·실행 계약과 공통 쿨타임 관리 |
| `EnemyCloseRangeAbility` | 타깃과의 Collider 표면 거리 검사 및 Player/Turret 근접 피해 |
| `EnemyNetworkController` | 서버에서 행동 실행, 체력 사망 및 Despawn 수명 처리 |

## 타깃 및 이동

목표 선택은 `EnemySquad`가 담당하며, 개별 컨트롤러는 배정받은 목표를 평가해 바꾸지 않는다. 목표가 유효하면 분대는 이를 유지하고, 목표가 없거나 공격 불가능할 때 새 후보를 찾는다. `AssignTarget(null)`은 타깃을 해제한다.

`MovingEnemyController`는 `EnemyAbility.CheckCondition()`이 참이면 이동을 멈추고 능력 사용을 시도한다. 조건이 거짓이면 타깃 경로의 다음 방향으로 이동한다. 타깃이 없거나 경로가 없을 때는 정지한다. 따라서 `CheckCondition()`은 쿨타임 여부와 독립적으로 능력을 쓸 위치·상황인지를 표현해야 한다. 조건이 참이어도 쿨타임 중일 수 있으며, 이때 컨트롤러는 정지 상태를 유지한다.

새 이동 유형은 필요에 따라 `EnemyController`를 상속해 `ExecuteBehavior()`와 `OnTargetChanged()`를 구현한다. 경로 이동이 필요한 경우 `EnemyMovement`를 재사용할 수 있다.

## 능력 추가

`EnemyAbility` 하위 클래스에서 다음 두 메서드를 구현한다.

- `CheckCondition()`은 타깃·거리·상태 등 발동 조건만 검사한다. 쿨타임을 직접 검사하지 않는다.
- `Activate()`는 효과를 실행하고 성공 여부를 반환한다. 반환값이 참일 때만 공통 쿨타임이 시작된다.

컨트롤러는 `TryUse()`가 성공하면 `EnemyAnimation.PlayAbilityAnimation()`을 호출한다. 이동형 컨트롤러에서는 능력 조건이 참인 동안 정지한 뒤 이 실행을 시도한다. 투사체형·지원형처럼 근접 공격과 다른 행동은 별도 능력 구현과 필요 시 별도 컨트롤러로 구성한다. 이 구조 자체가 해당 능력의 게임플레이 규칙이나 네트워크 효과를 자동 제공하지는 않는다.

현재 `EnemyCloseRangeAbility`는 비트리거 몸체 Collider와 활성 타깃 비트리거 Collider 사이의 표면 거리를 `attackRange`와 비교한다. 타깃은 `TargetableComponent`가 유효해야 한다. Turret에는 `TryApplyDamage()`를, Player에는 부모의 `PlayerInfo.TakeDamage()`를 호출한다. 피해량이 양수여야 하며, 실제 피해 반영은 각 타깃 구현의 책임이다.

## 프리팹 구성

| 에셋 | 사용 |
| --- | --- |
| `Assets/Prefabs/Enemies/Base/EnemyRoot.prefab` | 공통 루트. 렌더링·물리·체력·타깃·애니메이션·네트워크 수명 컴포넌트를 제공한다. |
| `Assets/Prefabs/Enemies/Base/MeleeEnemy.prefab` | EnemyRoot를 바탕으로 `MovingEnemyController`, `EnemyMovement`, `EnemyCloseRangeAbility`를 더한 근접형 기반 프리팹이다. |
| `Assets/Prefabs/Enemies/Runtime/Melee/Slime.prefab` | 현재 Slime 정의가 참조하는 근접형 런타임 프리팹이다. 이동·공격 설정과 시각 에셋은 여기서 조정한다. |
| `Assets/Prefabs/Enemies/Testing/SlimeTest.prefab` | 테스트 씬에서 사용하는 Slime 프리팹 변형이다. 런타임 카탈로그 정의와 구분한다. |

현재 `Assets/Data/Enemies/Spawning/Definitions/Slime.asset`은 `Runtime/Melee/Slime.prefab`을 참조한다. `Assets/Prefabs/Enemies/Runtime/Slime.prefab`은 별도 기존 프리팹이므로, 해당 파일을 수정했다고 카탈로그의 Slime 런타임 설정이 바뀌는 것은 아니다.

새 런타임 Enemy를 추가할 때는 기반 프리팹을 선택해 필수 참조를 연결하고, Enemy 정의의 프리팹 참조가 의도한 런타임 에셋을 가리키는지 확인한다. 스폰 정의, 네트워크 프리팹 등록 등 프로젝트의 기존 등록 절차도 함께 확인한다.

## 검증 범위

`Assets/Scripts/Features/Enemy/Editor/EnemyAbilityTests.cs`에는 능력 조건·쿨타임·근접 피해와 `MeleeEnemy` 및 Slime 프리팹의 설정·참조를 검사하는 테스트가 있다. 테스트 파일의 존재만으로 전체 Enemy 네트워크 플레이, 밸런스, 각 Scene 통합 또는 새 능력 구현이 검증되었다고 볼 수 없다. 해당 범위는 별도로 확인한다.
