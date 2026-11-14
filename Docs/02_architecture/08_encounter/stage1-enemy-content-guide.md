# Stage 1 Enemy 콘텐츠와 등록 가이드

수정일: 2026-11-14
상태: #540 통합 브랜치의 현재 에셋·구현·검증 기록. 수치는 밸런스 초안이다.

[문서 목차](../../README.md) · [시스템 구조](enemy-system-guide.md) · [Encounter 편성·예산](encounter-planning-guide.md)

## 이슈 범위와 현재 결과

[#540 Diversify Stage 1 Monsters](https://github.com/Team-HJD-21/unity-6-the-developer/issues/540)은 Slime 한 종류 중심의 전투를 근접·원거리 Variant로 확장하는 작업이다. 연결 Task는 #541 의존성/책임 정리, #542 공통 Root/상속 구성, #543 실제 Variant·발사체·Encounter 적용이다.

| 범위 | 이번 작업에서 정리한 결과 |
| --- | --- |
| 공통 구조 | AIBrain 호출 계층 제거, 컨트롤러·타깃·이동·능력·애니메이션·체력 책임 분리 |
| 제작 계층 | EnemyRoot → MovingEnemy → MeleeEnemy/RangedEnemy, 런타임 Variant와 Animator Override/클립 구성 |
| 전투 다양화 | 근접·원거리 외에 Bomber, 아군 회복, 변태 소환, Player 전용 암살자 구현 |
| 편성·생성 | Definition 비용·분대 규칙, 8개 프리셋, 타깃 주변 상황에 따른 선택과 지역 예산 배분 |
| 연결 | Initial_Stage의 Region Binding, 기존 Core 요청/환경/origin/시간 계약과 충돌 병합 |

지원·소환·암살자와 상황별 편성은 원래 이슈 설명의 근접/원거리보다 확장된 구현 범위다. `EliteEscort`나 BattleTroll의 이름을 별도 보스 시스템·전용 엘리트 패턴 구현 완료로 해석하지 않는다. Stage 2 이후, 전체 난이도 확정, 운영용 소비자 분리는 별도 작업이다.

## 현재 런타임 콘텐츠

실제 등록 원본은 [EnemyCatalog](../../../Assets/Data/Enemies/Spawning/EnemyCatalog.asset), [Definitions](../../../Assets/Data/Enemies/Spawning/Definitions), [런타임 프리팹](../../../Assets/Prefabs/Enemies/Runtime)이다. 현재 19개 Definition을 등록했다.

| Runtime 폴더 | Enemy ID | 구현 방식 |
| --- | --- | --- |
| `Melee/` | Slime, AntennaSlime, GoblinDual, GoblinSpear, GoblinWarrior | MovingEnemyController + EnemyCloseRangeAbility. 체력·피해·거리·쿨타임을 에셋에서 조정 |
| `Melee/` | InfectiousMushroom, Mushroom, MushroomTreant, Treant, GreenGolem, Troll, BattleTroll | 같은 근접 구조 사용. 독·감염 등 이름에서 연상되는 추가 효과 구현을 의미하지 않음 |
| `Ranged/` | CannonSlime | 폭발 피해·명중 FX를 가진 공격 투사체 |
| `Ranged/` | PlantLeafer | 직선 공격 투사체. 애니메이션 이벤트 발사 지원 |
| `Ranged/` | GoblinMage | ArcaneOrbProjectile의 직격 피해·명중 FX·감속 수신 API 호출 |
| `Support/` | PartyMushroom | 자신을 제외한 범위 내 회복 가능한 아군 한 마리에게 구슬 발사 |
| `Support/` | SpawnerMushroom | 타깃까지 이동 → 범위 진입 시 변태 → 위치 고정 → 주기적 Bomber 소환 |
| `Assassin/` | BlackWolf | 개별 PlayerOnly 분대, 준비 시간 후 근접 공격, 탐지 상태 기반 Player 피격 허용 |
| `Special/` | BomberSlime | Collider 표면 거리로 발동, 범위 피해 후 폭발 모션 종료 시 제거 |

`SlimeTest` 및 예전 `Runtime/Slime.prefab`을 새 몬스터의 제작 기준으로 사용하지 않는다. 현재 Slime Definition은 `Runtime/Melee/Slime.prefab`을 가리킨다. 공통 Animator는 [Animations/Enemies/Common](../../../Assets/Art/Animations/Enemies/Common), 유형별 애니메이션은 `Assets/Art/Animations/Enemies/Runtime`과 해당 콘텐츠 에셋을 확인한다.

## 고유 동작과 연결 조건

### 회복

PartyMushroom은 `EnemyHealAbility`에서 자신의 몸통을 기준으로 주변 Collider를 찾고, 자기 자신·비활성/사망·최대 체력인 대상을 제외한다. 표면 거리 검사를 통과한 가장 가까운 아군 한 마리를 선택한다. 이동 타깃은 전투 목표, `FacingTarget`은 치료할 아군으로 구분한다. 치료할 아군이 없더라도 전투 목표의 능력 범위에 닿으면 정지한다.

구슬이 실제 대상에 도착해야 `EnemyHealth.Heal()`이 호출된다. 대상이 먼저 최대 체력이 되거나 무효화되면 구슬은 제거된다. Play 시작 시 Health가 초기화되므로 시험 피해는 실행 후 적용한다.

### 소환

변태는 소환 `TryUse()`와 분리돼 소환 쿨타임을 소비하지 않는다. `OnTransformationAnimationFinished()`에서 Rigidbody 위치를 고정한 뒤 최초 소환부터 시도한다. 이후에는 공통 쿨타임마다 모션을 시작하고, `OnSummonAnimationEvent()`에서 생성하며, `OnSummonAnimationFinished()`에서 실행 잠금을 푼다.

현재 소환 Definition은 BomberSlime을 사용한다. 정면 타깃 방향으로 `spawnDistance`만큼 떨어져 생성하고, 여러 마리는 `spawnSpacing`으로 좌우 분산한다. 소환은 `EnemySummonGenerator`가 처리하며 일반 `EnemySpawnExecutor`나 SquadPreset을 재사용하지 않는다. 생성한 적은 Definition의 분대/타깃 규칙으로 독립적인 분대에 등록해 재검색할 수 있다.

변태 완료 상태에서 체력으로 죽었을 때 `OnDeath()`가 `SummonOnDeath()`를 실행한다. 추가 수량은 `deathSummonCount`이며 코드 기본값 5는 조정 가능한 값이다. 일반 소환은 파괴될 때까지 쿨타임마다 이어진다. 소환된 Bomber는 소환사가 죽어도 같이 제거하지 않는다.

현재 이 소환 경로는 일반 Executor 추적 목록에 등록하지 않으므로 그 목록 기반 비용/개체 수 집계와 구분한다. 공격인원 포화도 제외 데이터·소비 정책과 Match 종료 시 소환 적 전체 수명 연결까지 구현 완료로 보지 않는다.

### 암살자

BlackWolf의 Definition은 `Individual + PlayerOnly`다. 여러 탐지자가 등록돼 있으면 한 명이 해제돼도 탐지 상태를 유지하고, 마지막 탐지자가 빠지면 다시 비탐지 상태가 된다. 서버 판정만 `EnemyNetworkDetection`으로 동기화한다.

`EnemyAssassinDamageRule`은 탐지 중인 경우에만 `AttackSourceType.Player` 피해를 허용한다. 터렛의 실제 후보 검색에서 제외하려면 터렛 측이 `IAttackableEnemy.CanBeAttackedBy()`를 조회해야 한다. 탐지/피격 API 제공과 다른 파트의 실제 소비를 구분한다. 은신 셰이더는 포함하지 않는다.

## 프리셋과 비용

[SquadPresetCatalog](../../../Assets/Resources/Spawning/EnemySquadPresetCatalog.asset)의 현재 등록 편성이다. `Composition.Weight`는 한 세트의 마릿수이고 `SelectionWeight`는 추첨 비중이다. 둘은 다른 값이다. 총 비용은 `Σ(Definition.SpawnCost × Composition.Weight)`로 계산한다.

| Squad Order | 구성 | 마릿수 | 세트 비용 | 기본 선택 가중치 |
| --- | --- | ---: | ---: | ---: |
| AssassinSquad | BlackWolf ×2 | 2 | 6 | 1 |
| BacklineGuard | GreenGolem ×1, GoblinMage ×1, Slime ×3 | 5 | 10 | 2 |
| BalancedFormation | GreenGolem ×1, GoblinSpear ×1, GoblinMage ×1, PartyMushroom ×1, Slime ×2 | 6 | 14 | 6 |
| Normal | GoblinSpear ×1, Slime ×2 | 3 | 4 | 4 |
| EliteEscort | GreenGolem ×1, BattleTroll ×1, GoblinWarrior ×1, CannonSlime ×1, GoblinDual ×2 | 6 | 17 | 1 |
| RushAssault | GoblinDual ×4, Slime ×2 | 6 | 10 | 2 |
| SummonEscort | GreenGolem ×1, GoblinWarrior ×1, SpawnerMushroom ×1, Slime ×3 | 6 | 13 | 2 |
| SustainLine | Troll ×1, PartyMushroom ×1, GoblinMage ×1, Slime ×2 | 5 | 11 | 2 |

표는 2026-11-08 에셋 기준이다. 수치 변경 시 원본을 우선하고 표도 갱신한다. BomberSlime의 현재 Definition 비용 필드는 1이지만 일반 편성에는 넣지 않으며, 소환 시 Encounter 예산을 차감하지 않는다. 이를 비용 0으로 설정했다고 설명하지 않는다.

Definition·카탈로그 등록과 편성 포함은 별개다. 현재 AntennaSlime, InfectiousMushroom, Mushroom, MushroomTreant, PlantLeafer, Treant는 등록돼 있으나 위 8개 편성에 포함되지 않는다. Bomber는 소환형 전용이다. 해당 몬스터도 시험하려면 고유 동작 시험 또는 편성 데이터 구성이 필요하다.

## 새 몬스터 추가 순서

### 방향별 피격 콜라이더 설정

현재 [EnemyRoot](../../../Assets/Prefabs/Enemies/Base/EnemyRoot.prefab)의 기본 피격 구성은 `SpritePhysicsShapeSync`와 `PolygonCollider2D`가 아니다. EnemyRoot에는 루트의 [DirectionalCapsuleCollider2D](../../../Assets/Scripts/Shared/Physics/DirectionalCapsuleCollider2D.cs)가 있고, 이 컴포넌트가 자식 `HitBox`의 `CapsuleCollider2D`를 참조한다. 새 일반 몬스터 Variant는 이 Base 구성을 상속해 사용한다.

1. Variant의 `DirectionalCapsuleCollider2D`에서 Animator와 `Target Collider` 참조가 EnemyRoot의 Animator 및 자식 HitBox `CapsuleCollider2D`를 가리키는지 확인한다. 보통 Base 참조를 그대로 상속한다.
2. 몬스터 크기와 각 방향 스프라이트에 맞춰 `Down Shape`, `Up Shape`, `Left Shape`, `Right Shape`의 `offset`, `size`, `direction`을 Inspector에서 조정한다. 이 값은 HitBox 로컬 좌표 기준이다. 세로형 캡슐은 `Vertical`, 가로로 긴 캡슐은 `Horizontal`을 선택한다.
3. Animator의 이동 방향 파라미터가 `MoveX`·`MoveY`가 아니면 `Direction X Parameter`와 `Direction Y Parameter`를 Animator와 맞춘다. 컴포넌트는 파라미터로 방향을 판단하고, 방향별로 미리 설정한 캡슐 값을 적용한다. 스프라이트 Physics Shape를 읽거나 스프라이트 윤곽을 자동 복사하지 않는다.
4. 변태 후 고정된 피격 모양이 필요한 몬스터만 `Use Fixed Shape`를 켜고, 고정형 Animator State에 `FixedShape` Tag를 지정한 뒤 `Fixed Shape`의 `offset`, `size`, `direction`을 맞춘다. 일반 몬스터는 끈 상태로 둔다.

컴포넌트는 방향·형태가 바뀔 때만 캡슐 속성을 적용하고, 멈춰 Animator 방향 값이 0이 되면 마지막 방향을 유지한다. 따라서 걷기·공격의 매 프레임 윤곽을 따라가는 방식은 아니다. SpawnerMushroom은 `Use Fixed Shape`를 켜고 시설 모양의 `Fixed Shape` 값을 사용한다. 시설 Idle/소환 Animator State에는 `FixedShape` Tag가 있어야 하며, 변태 중에는 이동형 방향 캡슐을 쓰다가 시설 상태에 들어가면 고정형 값으로 전환된다.

`SpritePhysicsShapeSync`는 [PolygonCollider2D의 경로를 대표 Sprite Physics Shape로 바꾸는 별도 스크립트](../../../Assets/Scripts/Shared/Physics/SpritePhysicsShapeSync.cs)다. 현재 EnemyRoot 기반 새 몬스터 제작 절차에는 사용하지 않는다. 프로젝트에서 별도로 Polygon 피격 형태를 선택한 경우에만 해당 스크립트와 PolygonCollider2D를 함께 구성하고, 캡슐 방식과 혼용하지 않는다.

### 제작·등록

1. [Base 프리팹](../../../Assets/Prefabs/Enemies/Base)에서 행동에 맞는 Variant를 만들고 `Runtime/<유형>/`에 둔다. 고유 능력이 필요할 때 Ability를 추가하고, 행동 전이가 다를 때 Controller를 확장한다.
2. SpriteRenderer와 방향별 클립을 연결하고, HitBox 자식의 `CapsuleCollider2D` 및 루트 `DirectionalCapsuleCollider2D`의 방향별 `offset`·`size`·캡슐 방향을 맞춘다. Rigidbody2D와 Animator Controller/Override도 확인한다. `activationRange` 기즈모로 접촉 거리와 효과 반경을 따로 확인한다.
3. 능력·Movement·Animation·Health·네트워크 컴포넌트 참조와 필요한 Animation Event를 연결한다. 런타임 Variant에는 임시 로컬 테스트 플래그를 남기지 않는다.
4. 투사체·FX는 별도 프리팹에 구성한다. 현재 캐넌 투사체 파일명은 `CannonPorjectile.prefab`이므로 참조 확인 시 실제 철자를 따른다. 이 문서 작업에서 에셋 이름을 변경하지 않는다.
5. EnemyDefinition의 고유 ID·Prefab·비용·SquadMode·TargetRule을 지정하고 EnemyCatalog에 등록한다. 체력·공격력·이동속도·거리 등 실제 값은 프리팹의 해당 컴포넌트에서 조정한다.
6. 적·투사체의 NetworkObject와 사용 중인 NetworkManager의 Network Prefab 목록을 등록한다. FX는 구현이 요청하는 프리팹 참조와 종료 이벤트를 확인한다.
7. 자연 등장시키려면 SquadPreset 구성과 SquadPresetCatalog에 등록한다. 일반 편성에 BomberSlime을 추가하지 않는다.
8. 로컬 한 마리, Host 생성·피해, Client 표현, 타깃 무효화·제거까지 확인한다. GUID·Prefab 참조와 `.meta`를 함께 보존한다.

## EnemyTest 조작 창

현재 씬에서 창을 여는 순서와 각 버튼의 의미는 [Enemy 테스트 씬 사용법](enemy-test-scene-guide.md)을 참고한다.

`Assets/Scenes/Tests/EnemyTest.unity`를 열고 `Tools > TeamHJD > The Developer > Enemy Test`를 실행한다. Play 후 창에서 `Start Host`를 누른다. 현재 테스트 씬의 런처는 `Encounter Debug Spawner`를 명시적으로 참조하며 `시작 시 초기 편성 생성`은 꺼져 있다. 기존 `EnemySpawnRoot`도 씬에 남아 있지만 이 창의 요청은 런처에 연결된 실행기로 전달된다.

- **선택한 편성 요청**: 카탈로그의 Squad Order를 선택하고 기존 Core 요청 서비스에 전달한다.
- **현재 상황으로 편성 선택·요청**: 최신 전장·Region Snapshot과 Core 난수로 `EncounterRuntime`에서 예산을 계산하고 Planner가 상황별 편성을 선택한다.
- 두 경로 모두 `EncounterRuntime → SpawnCompositionPlanner → EnemySpawnExecutor`를 거쳐 실제 생성한다. 생성 상한은 요청당 최대 수이며, 정확한 생성 수나 전역 생존 수 제한이 아니다. 예산·완전한 편성·셀 수용량에 따라 적게 나올 수 있다.
- 창에는 실행기, Match, 실행기가 추적하는 생존 적 수, 지정 편성의 구성·비용, 최근 버튼 요청의 계획 수·실제 생성 수 또는 거부 사유를 표시한다. 상세 상황별 선택 기록은 Game Debugger와 Console을 함께 확인한다.
- **시험 씬 몬스터 전체 정리 · 소환물 포함**은 연결된 실행기와 같은 씬·같은 NetworkManager의 생성된 서버 몬스터를 제거한다. 소환사가 죽은 뒤 남은 봄버도 포함하며 사망 효과를 실행하지 않고 Despawn한다. 실제 플레이어·터렛·CU와 네트워크에 생성되지 않은 배치 개체는 대상이 아니다. 런처의 대기 중 생성 요청은 취소하지만 다른 도구의 반복 요청은 별도로 중지해야 한다. 운영용 `DespawnForMatch()`와 `TryClearSpawnedEnemies()`의 추적 대상은 그대로이며 창의 정리만 Editor 전용 경로를 사용한다. 생존 수 표시는 여전히 Executor의 요청 생성 적만 집계한다.
- **테스트 표적**: Hierarchy에서 `EnemyDamageTestTarget`을 선택하거나 창의 표적 필드에 연결하면 체력·피격 횟수·최근/누적 피해를 표시한다. 선택 표적 또는 현재 실행기가 속한 씬의 모든 테스트 표적을 초기화할 수 있다. Host의 Play Mode에서만 허용하며 실제 터렛·CU·플레이어 체력은 초기화하지 않는다.
- **몬스터 관찰**: Hierarchy에서 몬스터나 자식 HitBox를 선택하거나 창의 몬스터 필드에 연결한다. 체력·네트워크 생성 여부·배정 타깃·타깃 가능 여부·배정 인원·분대 대표 여부·남은 경로·속력·실제 연결된 능력의 활성/실행 여부·능력의 별도 방향 대상·탐지 상태를 0.5초마다 읽는다. 상태를 보기 위해 `CheckCondition()`·능력 실행·경로 계산을 호출하지 않는다. Client 표시가 Host와 같은 권위 상태를 보장하는 것은 아니다.
- **선택 몬스터에 피해 적용**: 관찰 몬스터를 선택하고 양수 피해량·공격자 종류를 지정한다. `현재 체력만큼 피해 · 사망 시험`은 현재 체력만큼 같은 피해 API를 요청한다. 두 버튼 모두 Host에서 같은 시험 씬의 생성된 몬스터에만 사용할 수 있고 `EnemyHealth.TryTakeDamage()`의 탐지·피격 허용·필드 정지 규칙을 따른다. 사망 시험도 규칙상 거부될 수 있으며, 허용되면 실제 사망 이벤트와 소환사의 사망 소환을 실행한다. 힐러 시험에서는 치료받을 아군을 선택해 일부 피해를 준 뒤 회복을 관찰한다.
- **개별 몬스터 기능 시험**: 카탈로그에 등록된 19개 정의 중 하나와 시험 구역을 선택한 뒤 `지정 지점에 1마리 생성·관찰`을 누른다. 출현 지점과 초기 타깃은 직접 바꿀 수 있으며, 타깃을 비우면 분대가 자동 선택한다. 생성 성공 시 해당 몬스터를 Hierarchy에서 선택해 관찰 영역에 연결한다.
- 개별 시험은 Editor 전용 `EnemyNetworkTestLauncher.Testing → EnemySpawnExecutor.Testing → Execute` 경로다. 프리셋·예산 계산을 생략하지만 기존 네트워크 생성·Match 시계 연결·Definition의 분대/집계 설정·생성 추적·정리는 그대로 사용한다. 타깃 유형과 포화도, 지점의 활성/Region 제한도 따른다. BlackWolf 같은 PlayerOnly 정의에는 터렛형 시험 표적을 지정할 수 없다. 실제 플레이어의 TargetableComponent를 연결하거나 초기 타깃을 비운다. 전체 Encounter 경로는 기존 두 편성 요청 버튼으로 검사한다.

`EnemyTestAreas` 아래에는 `01_Turret`, `02_Player` 관찰 구역이 있다. 각 구역은 반경 0인 SpawnPoint와 체력 1000의 박스 표적을 16유닛 간격으로 배치한다. Turret 표적은 `TargetType.Turret`·`Turret` 레이어, Player 표적은 `TargetType.Player`·`PlayerFeetBox` 레이어를 사용한다. Player 구역에서는 BlackWolf 같은 PlayerOnly 몬스터도 지정 표적을 공격할 수 있다. `선택 구역을 Scene 뷰에서 보기`로 이동하고 Gizmos를 켜면 구역 경계와 출현 지점→표적 연결을 확인할 수 있다. 벽으로 격리한 공간이 아니므로 다른 몬스터·플레이어의 접근은 기존 감지와 타깃 정책을 따른다. 두 출현 지점도 같은 씬의 일반 Encounter 후보에 포함된다.

박스의 유형은 `EnemyDamageTestTarget`의 `Target Type`에서 설정한다. `TargetableComponent`의 유형이나 GameObject 레이어만 바꾸지 않는다. Awake·체력 초기화·피격 시 테스터에 설정한 유형을 다시 적용하며, Play Mode에서 유형을 바꿨다면 창의 표적 초기화를 눌러 반영한다. Player 박스는 타깃 선택·감지·피해 기록을 시험하는 더미이며, 실제 플레이어의 조작·은신 해제용 감지 영역·네트워크 상태를 대신하지 않는다.

구역 재제작 도구는 EnemyTest Edit Mode의 `Tools > TeamHJD > The Developer > Configure Enemy Test Areas`다. 기존 구역 Root가 있으면 선택만 하며 수동 조정값을 덮어쓰지 않는다. 신규 제작 전에 현재 NavMesh의 출현/표적 위치와 완전한 연결 경로를 검사한다. 씬 저장은 제작자가 수행한다.

이 창은 요청부터 실제 생성까지 확인하고 테스트 표적과 몬스터 상태를 관찰하는 도구다. 생성 성공만으로 피해·재탐색·모든 능력·클라이언트 동작까지 검증됐다고 판단하지 않는다.

2026-11-14 Editor Host 확인: 초기 자동 생성 0마리, 카탈로그 8개 표시, 지정 편성과 상황별 요청에서 각각 계획 6마리·실제 생성 6마리, 요청 생성 적 정리 후 추적 수 0을 확인했다. 창의 버튼 처리 메서드를 실행해 같은 결과 표시도 확인했다. Client와 실제 전투 동작은 이번 확인에 포함하지 않았다.

같은 날 추가 확인: Edit Mode와 Host 시작 전 초기화 버튼 비활성, 선택 표적의 25 피해 표시 후 체력 100/100·피격 기록 0 복원, 현재 씬의 테스트 표적 7개 전체 초기화, 생성된 슬라임의 자식 HitBox 선택 시 본체의 타깃·경로·능력 표시를 확인했다. 표적 피해는 검증 코드에서 직접 적용했으므로 몬스터 공격에 의한 피해 전달 검증과 구분한다.

개별 시험·구역 추가 당시에는 정의 19개와 구역 3개 표시, GoblinSpear 1마리의 지정 지점 생성·초기 표적 배정, 실제 공격으로 시험 표적 체력 1000→475 및 피격 35회, PlayerOnly 정의의 터렛형 표적 거부, 비활성 출현 지점 거부, 정리 후 추적 수·타깃 배정 인원 0을 확인했다. 창의 소환 버튼 처리로 SpawnerMushroom을 생성하고 자동 관찰 선택까지 확인했다. 당시 구역 추가로 시험 표적이 10개가 됐으며, 현재 저장된 EnemyTest 씬의 구역은 `01_Turret`과 `02_Player` 두 개다.

2026-11-15 Host 확인: 피해 버튼으로 GoblinSpear 체력 70→60 및 회복 가능 상태 전환, 비탐지 암살자의 Player 피해 거부, 탐지 중 암살자의 Turret 피해 거부·Player 피해 허용을 확인했다. 소환사의 변태 완료·소환 방출 콜백을 검증 코드에서 호출해 사망 시험을 준비했으며, 사망 피해 버튼으로 부모가 제거되고 봄버가 2→7마리로 늘어나는 것을 확인했다. 전체 정리 버튼 처리 후 같은 씬의 네트워크 몬스터·요청 생성 추적 수·타깃 배정 인원이 모두 0이었다. 이는 정리·피해 경로 확인이며 애니메이션 클립의 이벤트 타이밍 전체 검증은 아니다.

## 검증과 남은 작업

이번 통합 기준 커밋은 `57c364e65`다. 해당 시점의 검증을 모든 이후 에셋 변경에 대한 보장으로 확대하지 않는다.

| 종류 | 확인한 범위와 결과 |
| --- | --- |
| 정적/컴파일 | 병합 충돌 12개 해결, Unity 6000.3.23f1 컴파일 오류 0, Initial_Stage 중복 fileID 없음, Region Binding 유지 |
| 편성/예산 | SquadAutomaticPlanningTests 60, SpawnBudgetPolicyTests 25, EncounterPipelineBudgetTests 10, EncounterSceneBindingTests 5, SpawnPointSpatialPolicyTests 12: 112건 통과 |
| 능력/탐지 | EnemyAbilityTests 10건 중 7건 통과, EnemyDetectionTests 4건 통과. 전체 집계 126건 중 123건 통과 |
| 남은 테스트 불일치 | 기존 EnemyAbilityTests 3건은 삭제된 Runtime/Slime·SlimeTest 경로와 현재 0.35인 Slime 사거리에 옛 1.0 기대값을 사용함. 테스트를 통과시키려고 프리팹 수치를 되돌리지 않았음 |
| 수동 확인 | 작업자가 개발 과정에서 근접/발사체 피해, 폭발 FX, 회복 구슬, 소환, 탐지 상태/Host 동작을 확인했고 병합 후에도 정상 동작한다고 보고함. 자동 통합 테스트 통과나 모든 몬스터·조건의 망라 확인과 구분 |
| 미실행 | 이번 병합 뒤 Windows Development Build 및 두 실행 파일의 전체 멀티 인수는 수행하지 않음 |

후속 작업:

- 실제 운영 소비자 분리, Core의 반복 요청 시점/주기 정책 연결. 현재 테스트런처가 운영 계약도 구현하고 있으며, Core가 게임 중 계속 요청하는 Director를 이번 브랜치에서 완성한 것은 아니다.
- 감지 반경 Player 우선 전환과 지속 경로 갱신의 프리팹 설정 및 Host/Client 검증. 동작과 설정은 [Enemy 시스템 가이드](enemy-system-guide.md)를 따른다. 타깃별 공격인원 포화도와 프레임당 경로 계산 수 제한은 후속 작업이다.
- 은신 셰이더, 감지자/공격자 API의 Player·Turret 소비 인수, Mage 감속 수신자의 적용 범위 확인.
- 소환 적의 Match 수명·집계 경계, 새 능력/회복 구슬/소환 상태 등의 공용 시간·체크포인트 지원 확인.
- 에셋 등록과 실제 편성의 차이 정리, 임시 스탯·비용·가중치 플레이 밸런싱, 오래된 테스트 fixture 갱신과 Windows/멀티 인수.
