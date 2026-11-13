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

### 방향별 피격 윤곽 설정

`SpritePhysicsShapeSync`는 Animator의 `MoveX`·`MoveY`를 읽고 `downSprite`, `upSprite`, `leftSprite`, `rightSprite` 중 하나를 대표 윤곽으로 사용한다. 런타임 Variant마다 해당 몬스터의 각 방향 Idle 스프라이트를 연결하고, `targetCollider`에는 자식 HitBox의 PolygonCollider2D를 연결한다. 각 대표 스프라이트에는 Physics Shape가 필요하다. Animator 파라미터 이름이 다르면 `directionXParameter`·`directionYParameter`도 맞춘다.

방향이나 형태의 대표 스프라이트가 바뀔 때만 콜라이더 경로를 갱신한다. 같은 방향의 걷기·공격 프레임별 세부 윤곽은 따라가지 않는다. 방향 값이 모두 0이면 마지막 방향을 유지하며, 최초에는 아래쪽 윤곽을 사용한다.

SpawnerMushroom은 `fixedShapeSprite`에 시설 Idle 스프라이트를 연결하고, Animator의 `FacilityIdle`·`FacilitySpawn` State Tag를 `FixedShape`로 지정한다. 변태 중에는 이동형 윤곽을 유지하며 시설 상태에 진입하면 고정 윤곽으로 바뀐다. 일반 몬스터는 `fixedShapeSprite`를 비워 둔다. 이 설정은 각 인스턴스에 실제 적용된 Animator 상태를 따르며, 별도의 네트워크 상태를 추가하지 않는다.

### 제작·등록

1. [Base 프리팹](../../../Assets/Prefabs/Enemies/Base)에서 행동에 맞는 Variant를 만들고 `Runtime/<유형>/`에 둔다. 고유 능력이 필요할 때 Ability를 추가하고, 행동 전이가 다를 때 Controller를 확장한다.
2. SpriteRenderer, Collider 크기·offset, Rigidbody2D, Animator Controller/Override와 방향별 클립을 맞춘다. `activationRange` 기즈모로 접촉 거리와 효과 반경을 따로 확인한다.
3. 능력·Movement·Animation·Health·네트워크 컴포넌트 참조와 필요한 Animation Event를 연결한다. 런타임 Variant에는 임시 로컬 테스트 플래그를 남기지 않는다.
4. 투사체·FX는 별도 프리팹에 구성한다. 현재 캐넌 투사체 파일명은 `CannonPorjectile.prefab`이므로 참조 확인 시 실제 철자를 따른다. 이 문서 작업에서 에셋 이름을 변경하지 않는다.
5. EnemyDefinition의 고유 ID·Prefab·비용·SquadMode·TargetRule을 지정하고 EnemyCatalog에 등록한다. 체력·공격력·이동속도·거리 등 실제 값은 프리팹의 해당 컴포넌트에서 조정한다.
6. 적·투사체의 NetworkObject와 사용 중인 NetworkManager의 Network Prefab 목록을 등록한다. FX는 구현이 요청하는 프리팹 참조와 종료 이벤트를 확인한다.
7. 자연 등장시키려면 SquadPreset 구성과 SquadPresetCatalog에 등록한다. 일반 편성에 BomberSlime을 추가하지 않는다.
8. 로컬 한 마리, Host 생성·피해, Client 표현, 타깃 무효화·제거까지 확인한다. GUID·Prefab 참조와 `.meta`를 함께 보존한다.

## 검증과 남은 작업

이번 통합 기준 커밋은 `57c364e65`다. 해당 시점의 검증을 모든 이후 에셋 변경에 대한 보장으로 확대하지 않는다.

| 종류 | 확인한 범위와 결과 |
| --- | --- |
| 정적/컴파일 | 병합 충돌 12개 해결, Unity 6000.3.23f1 컴파일 오류 0, Initial_Stage 중복 fileID 없음, Region Binding 유지 |
| 편성/예산 | SquadAutomaticPlanningTests 60, SpawnBudgetPolicyTests 25, EncounterRuntimeBudgetTests 10, EnemyNetworkTestLauncherTests 5, SpawnPointSpatialPolicyTests 12: 112건 통과 |
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
