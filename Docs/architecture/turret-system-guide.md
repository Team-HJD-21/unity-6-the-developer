# Turret System Guide

작성일: 2026-09-24
최종 갱신: 2026-09-26
상태: Sprint 1 전환기 구현 가이드 / Canon·Missile 기준

[문서 목차](../README.md) · [코드·아키텍처 명명 규칙](naming-and-architecture-conventions.md) · [System Re-architecture Charter](system-rearchitecture-charter.md) · [Sprint 1 Stage 1 PoC](../planning/SPRINT_1_STAGE_1_POC.md)

## 1. 문서 목적과 적용 범위

이 문서는 현재 Canon Turret과 Missile Turret의 데이터 구조, 실행 흐름, Prefab 설정법과 확장 규칙을 설명한다. 터렛 코드를 수정하거나 AI·전력·업그레이드 시스템에서 터렛 정보를 사용할 때 먼저 확인한다.

이 문서는 현재 구현을 설명하는 **전환기 가이드**다. 제품 규칙이나 New Core의 최종 계약을 새로 확정하지 않는다. 게임 범위와 장기 구조가 충돌하면 [System Re-architecture Charter](system-rearchitecture-charter.md)와 [Docs Source of Truth](../README.md#source-of-truth)가 우선한다.

현재 적용 범위는 다음과 같다.

- Canon Turret LV1~LV3, Stage 1~3
- Missile Turret LV1~LV3, Stage 1~3
- `TurretDefinition`, `TurretRuntimeState`, `TurretBase`
- 로컬 런타임 등록을 위한 `TurretInstanceRegistry`
- 터렛 체력·파괴·플레이어 복구 흐름
- 인스턴스별 Damage/Power 보정과 Stage 1 프리팹 교체 방식의 레벨 승급
- Enemy PoC의 `PoCTargetable` 연결
- `TowerBullet`, `TowerMissile`로 전달되는 공격력

다음 항목은 이 문서의 현재 적용 범위가 아니다.

- Laser Turret 이식
- 파괴 연출과 복구 비용·시간의 최종 게임 규칙
- 승급 비용·시간과 Spaceship Research 연동
- NGO를 통한 `InstanceId`와 상태 동기화
- AI 평가값·위협도·전선 정보를 집계하는 정식 Turret Manager

## 2. 핵심 원칙

터렛 데이터는 다음 세 종류로 나눈다.

| 종류 | 의미 | 저장 위치 | 예시 |
| --- | --- | --- | --- |
| Definition | 실행 중 원본을 바꾸지 않는 콘텐츠·밸런스 값 | `TurretDefinition` ScriptableObject | 사거리, 기본 공격력, 최대 체력, 전력, 발사 속도 |
| Runtime State | 터렛 인스턴스마다 실행 중 바뀌는 값 | `TurretRuntimeState` | 자동 발급 ID, 활성 상태, 현재 체력, 파괴 여부, 업그레이드 보정값 |
| Behavior-local State | 공격 동작 내부에서만 필요한 짧은 상태 | Canon/Missile 구현체 | 현재 Target, 발사 타이머, 현재 미사일 과열 수치 |

```text
TurretDefinition ───────────────┐
  고정 수치                     │
                                v
Prefab → Canon/Missile → TurretBase → Projectile
             │              │          └─ 최종 공격력 전달
             │              └─ 공통 참조·Definition 조회
             v
      TurretRuntimeState
        인스턴스별 상태
             │
             v
    TurretInstanceRegistry
      로컬 ID 등록·조회
```

Definition 값을 실행 중 상태 저장소처럼 직접 수정하지 않는다. 반대로 Target, 활성 상태, 과열 진행도처럼 매번 달라지는 값은 Definition에 넣지 않는다.

## 3. 주요 타입과 책임

### 3.1 `TurretDefinition`

파일: [`TurretDefinition.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretDefinition.cs)

Stage와 Level별 터렛 Prefab이 참조하는 ScriptableObject다. 현재 18개 Canon/Missile Definition이 존재한다.

| 필드 | 의미 | 작성 규칙 |
| --- | --- | --- |
| `Id` | Definition을 식별하는 고유 ID | 소문자 `snake_case`, 중복 금지 |
| `DisplayName` | UI에 표시할 이름 | 사람이 읽을 수 있는 이름 |
| `Level` | 터렛 레벨 | 1 이상 |
| `Damage` | 발사체에 전달할 기본 공격력 | 0 이상 |
| `Range` | 적 탐색과 Gizmo에 쓰는 실제 사거리 | 0 이상 |
| `RotationSpeed` | 포신 회전 속도 | 0 이상 |
| `TargetingAngle` | 발사를 허용하는 조준 오차 | Canon 10°, Missile 360° |
| `FireRate` | 초당 발사 횟수 | 0보다 커야 함 |
| `MaxHealth` | 터렛의 최대 체력 | 1 이상 |
| `Power` | 활성화할 때 필요한 전력 | 0 이상 |
| `OverHeatTime` | Canon 연속 사격 과열 기준 | 초 단위 |
| `OverHeatMissileCount` | Missile 과열 기준 발사 횟수 | Canon에서는 0 |
| `CoolTime` | Canon 냉각 시간 | 초 단위 |

예시 ID는 `canon_stage1_lv1`, `missile_stage3_lv2`다. `Id`는 저장·조회용이고 `DisplayName`은 UI용이므로 서로 대신 사용하지 않는다.

Editor에서 Definition을 수정하면 전체 `TurretDefinition`을 검사한다. ID가 비었거나 서로 다른 에셋이 같은 ID를 사용하면 Console에 오류가 표시된다.

### 3.2 `TurretRuntimeState`

파일: [`TurretRuntimeState.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretRuntimeState.cs)

각 터렛 GameObject가 독립적으로 가지는 실행 중 상태다.

| 상태 | 설명 | 변경 방법 |
| --- | --- | --- |
| `InstanceId` | 현재 실행에서 터렛 한 개를 식별 | Registry가 자동 발급 |
| `IsActivated` | 사용자가 켜서 전력을 예약한 상태인지 표시 | `RequestActivation` |
| `IsOperational` | 활성화됐고 과열·파괴 상태가 아닌지 표시 | 활성화·과열·파괴 흐름에서 자동 계산 |
| `CurrentHealth` | 현재 남아 있는 체력 | `ApplyDamage`, `Restore` |
| `IsDestroyed` | 체력 0으로 파괴됐는지 표시 | `ApplyDamage`, `Restore` |
| `DamageBonus` | 수동 보너스와 적용된 업그레이드의 공격력 보정 합 | `SetDamageBonus`, `AddDamageBonus`, `ApplyUpgrade` |
| `PowerBonus` | 적용된 업그레이드의 실행 중 전력 보정 | `ApplyUpgrade` |

Prefab의 `_instanceId` 기본값 `0`은 **미할당**을 뜻한다. Inspector에서 ID를 수동으로 정하지 않는다. 실제 ID는 Play Mode에서 등록될 때 양수로 자동 발급된다.

최종 공격력은 다음 규칙을 사용한다.

```text
Final Damage = max(0, Definition.Damage + RuntimeState.DamageBonus)
Effective Power = max(0, Definition.Power + RuntimeState.PowerBonus)
```

영구 연구 수치를 Definition에 덮어쓰지 않는다. Profile/Research 결과를 Match 시작 시 런타임 보너스로 변환하는 Adapter가 이후 필요하다.

### 3.3 `TurretBase`

파일: [`TurretBase.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretBase.cs)

Canon과 Missile이 공통으로 사용하는 Unity 표현 계층의 작은 base class다. 공통 Scene 참조와 Definition/RuntimeState 접근을 제공한다.

주요 책임은 다음과 같다.

- 포신, 회전 지점, Animator, SpriteRenderer, LayerMask 참조 보관
- Definition의 수치를 읽기 전용 property로 제공
- 활성화·전력 예약의 단일 요청 API 제공
- 체력 감소, 파괴, 복구 API 제공
- 최종 공격력 계산
- `OnEnable`/`OnDisable`에서 Registry 등록·해제

`TurretBase`에 새 밸런스 값을 직접 추가하기 전에 먼저 다음을 판단한다.

1. 모든 인스턴스가 공유하는 고정 값이면 `TurretDefinition`
2. 인스턴스마다 실행 중 바뀌며 여러 시스템이 읽어야 하면 `TurretRuntimeState`
3. 한 공격 방식 내부에서만 잠깐 필요하면 Canon/Missile 구현체의 private field

### 3.4 `TurretInstanceRegistry`

파일: [`TurretInstanceRegistry.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretInstanceRegistry.cs)

씬에 존재하는 터렛에 로컬 `InstanceId`를 발급하고 ID와 상태로 터렛을 조회한다.

```csharp
if (TurretInstanceRegistry.TryGet(instanceId, out TurretBase turret))
{
    turret.AddDamageBonus(5);
}
```

같은 Definition 에셋을 여러 터렛 인스턴스가 사용하는 것은 정상이다. 오류가 되는 경우는 **서로 다른 Definition 에셋이 같은 `Definition.Id`를 사용하는 경우**다.

Registry는 현재 PoC용 로컬 등록부다. AI 조회용 상태 집계, 전력 총합, 구역별 터렛 관리까지 책임지는 정식 Manager가 아니다. `TowerManager`를 다른 이름의 전역 Singleton으로 다시 만드는 방식으로 확장하지 않는다.

현재 제공하는 조회는 `GetAll`, `GetActive`, `GetOperational`이다. 등록·해제,
활성화·작동 상태, 체력 변경, 파괴, 복구, 세부 업그레이드와 레벨 승급은 각각 Registry 이벤트로 알린다. 파괴된
터렛은 Registry에 남아 복구할 수 있지만 Active/Operational 조회에서는 제외된다.

### 3.5 Assembly 경계

Turret의 독립 가능한 계약과 공통 구조는 다음 두 assembly로 분리한다.

| Assembly | 포함 범위 | 허용 의존성 |
| --- | --- | --- |
| `TeamHJD.Game.Turrets.Contracts` | `ITurretActivationRequester`, `ITurretPowerSource`, 활성화 결과 | .NET BCL만 |
| `TeamHJD.Game.Turrets` | Definition, RuntimeState, Base, InstanceRegistry, 활성화·탐색 공통 로직 | Contracts, Unity |

`Contracts`는 `noEngineReferences: true`를 유지한다. `Turrets`는 ScriptableObject와
MonoBehaviour를 포함하므로 Unity Engine을 참조한다. Canon/Missile/Laser concrete 구현은
`AudioManager`, `Monster` 등 legacy 코드 의존성이 남아 있어 현재 `Assembly-CSharp`에
유지한다. 새 assembly에서 legacy `Assembly-CSharp`를 역참조하도록 설정하지 않는다.

Assembly와 namespace는 `TeamHJD.Game.*` 표기를 사용한다. 새 reference를 추가할 때는
편의를 위해 양방향 참조를 만들지 말고 위 표의 단방향을 유지한다.

### 3.6 Canon과 Missile 구현체

| 타입 | 책임 |
| --- | --- |
| `DefaultCanonTurret` | 단일 Target 탐색, 포신 회전, 발사·과열·냉각, 활성화 표현 |
| `CanonTurretLv1~3` | Level별 발사구와 발사체 생성 |
| `DefaultMissileTurret` | 복수 Target 탐색, 포신 회전, 발사 횟수 기반 과열, 활성화 표현 |
| `MissileTurretLV1~3` | 발사구 수에 맞는 Target 배열과 미사일 생성 |
| `TowerBullet` | Canon이 계산한 공격력을 받아 충돌 대상에 적용 |
| `TowerMissile` | Missile이 계산한 공격력을 받아 폭발 범위 대상에 적용 |

Level 스크립트에서 `Damage = 10`처럼 밸런스 수치를 다시 하드코딩하지 않는다. 발사체를 생성한 직후 `Initialize(target, Damage)`를 호출하여 Target과 Definition/RuntimeState에서 계산된 공격력을 한 번에 전달한다.

## 4. 실행 흐름

### 4.1 생성과 등록

```text
Prefab 활성화
→ Canon/Missile Awake에서 Definition과 ControlUnit 참조 확인
→ TurretBase.OnEnable()
→ Registry 등록
→ InstanceId가 0이면 새 ID 자동 발급
→ Definition ID 유효성 검사
→ Level Start에서 발사구 배열과 사거리 표시 크기 초기화
```

### 4.2 활성화와 전력

```text
UI 또는 다른 시스템이 RequestActivation(bool) 호출
→ TurretActivationController가 현재 상태 확인
→ 활성화 요청이면 ITurretPowerSource.TryConsumePower(EffectivePower)
→ 전력 예약 성공 후 RuntimeState.IsActivated 변경
→ 비활성화 요청이면 예약 전력을 ReleasePower(EffectivePower)로 반환
→ TurretActivationResult와 Registry 상태 이벤트 전달
```

호출부는 전력을 먼저 검사한 다음 별도 활성화 메서드를 호출하지 않는다. 전력 확인과 상태
변경은 반드시 `RequestActivation` 한 경로에서 처리한다. 과열은 사용자가 끈 상태가 아니므로
`IsActivated`와 전력 예약을 유지하고 `IsOperational`만 일시적으로 `false`가 된다.
`ControlUnitStatus.ReleasePower`는 반환 전력을 0.1초에 1씩 회복한다. 반면 업그레이드로
전력 사용량이 감소할 때의 차액은 `TryChangeReservation`에서 즉시 반환한다.

### 4.3 체력, 파괴와 복구

```text
ApplyDamage(damage)
→ RuntimeState.CurrentHealth 감소
→ HealthChanged 이벤트
→ 체력이 0이면 RequestActivation(false)로 전력 반환
→ IsDestroyed = true
→ Destroyed 이벤트

플레이어가 Restore() 호출
→ CurrentHealth = Definition.MaxHealth
→ IsDestroyed = false
→ Restored 이벤트
→ 비활성 상태 유지
→ 플레이어가 별도로 RequestActivation(true) 호출
```

파괴된 터렛은 Registry에서 즉시 제거하지 않는다. 플레이어가 같은 인스턴스를 복구할 수
있도록 등록 상태를 유지하되 Active/Operational 조회와 활성화 요청에서는 제외한다. Scene
전환이나 실제 GameObject 제거는 `Unregistered`로 구분한다.

### 4.4 탐색과 공격

Canon은 범위 안에서 가까운 적 하나를 선택하고, `TargetingAngle` 이내로 회전한 뒤 발사한다. Missile은 발사구 수만큼 Target 배열을 만들며 복수의 적을 선택한다. 적 수가 부족하면 첫 Target을 보조 슬롯에서 재사용할 수 있다.

```text
Physics2D 범위 탐색
→ Target 선택
→ 포신 회전
→ FireRate 충족
→ 발사체 생성
→ Definition Damage + Runtime Bonus 전달
→ 발사체 충돌 또는 폭발에서 피해 적용
```

## 5. Prefab과 Definition 설정 방법

### 기존 수치 조정

1. 대상 Prefab이 참조하는 `_definition`을 확인한다.
2. 해당 `TurretDefinition` 에셋에서 수치를 수정한다.
3. 같은 Level이라도 Stage별 Definition이 다르므로 수정 범위를 확인한다.
4. `Id`는 참조 키이므로 밸런스 조정만으로 변경하지 않는다.
5. Prefab의 발사구와 시각적 사거리 표시가 의도대로 동작하는지 `TurretTest` Scene에서 확인한다.

### 새 Canon/Missile 변형 추가

1. 가장 가까운 기존 Prefab과 Definition을 복제한다.
2. 새 Definition에 고유 `Id`, `DisplayName`, Level과 전투 수치를 입력한다.
3. Prefab의 `_definition`을 새 에셋으로 교체한다.
4. 발사구 배열과 방향 Transform을 확인한다.
5. `_runtimeState._instanceId`는 `0`으로 둔다.
6. Gizmo 사거리와 실제 적 탐지 범위를 비교한다.
7. 활성화·비활성화, 전력 부족, 과열·냉각, Target 사망 후 재탐색을 검증한다.
8. 체력 감소, 파괴, 복구 후 수동 재활성화를 검증한다.
9. 새 에셋과 `.meta` 파일을 함께 커밋한다.

### 사거리 표시

실제 탐지와 Scene Gizmo는 `Definition.Range`를 사용한다. 인게임 원형 Sprite는 원본 이미지 크기 때문에 현재 `Range * 2.5` scale을 사용한다. 이 배율은 시각 표현용이며 게임 규칙의 실제 사거리가 아니다.

## 6. 업그레이드와 밸런스 규칙

- 기본 수치 조정: Definition 에셋 수정
- 한 판 동안 적용되는 강화·버프: `TurretUpgradeDefinition`을 선택한 인스턴스의 RuntimeState에 적용
- 계정 영구 성장: Profile/Research에서 보관하고 Match 시작 시 RuntimeState 또는 immutable `MatchConfig`로 변환
- 발사체: 자신이 생성될 때 받은 최종 공격력만 사용

`TurretUpgradeDefinition`은 업그레이드 ID, 표시 이름, 호환 가능한 `TurretDefinition.Id` 목록, Damage/Power 보정값을 가진다. 호환 목록이 비어 있으면 모든 터렛 Definition에 적용할 수 있다.

```csharp
TurretUpgradeResult result = turret.ApplyUpgrade(upgradeDefinition);
```

적용 규칙은 다음과 같다.

- 원본 `TurretDefinition` ScriptableObject는 수정하지 않는다.
- 보정값과 적용 이력은 선택한 `TurretRuntimeState`에만 저장된다.
- 같은 업그레이드 ID는 동일 인스턴스에 한 번만 적용된다.
- 활성 터렛의 Power가 증가하면 추가 전력을 즉시 예약한다. 전력이 부족하면 업그레이드 전체를 적용하지 않는다.
- Power가 감소하면 차액을 즉시 반환한다.
- 외부 조회에는 `EffectiveDamage`, `EffectivePower`를 사용한다.
- 적용 성공 시 `TurretInstanceRegistry.UpgradeApplied`가 발생하므로 AI·전선 Adapter가 값을 다시 읽을 수 있다.

현재 제공하는 샘플은 Canon용 `Low Power`(Damage -3, Power -5)와 `High Firepower`(Damage +6, Power +8)다. 두 업그레이드는 서로 배타적이지 않아 같은 인스턴스에 모두 적용할 수 있다. 새 업그레이드는 에셋을 추가해 확장하며 기존 Canon/Missile 구현체에 조건문을 추가하지 않는다. 사거리와 발사 속도 modifier는 실제 규칙이 확정되기 전까지 추가하지 않는다. 모든 값을 하나의 범용 Dictionary에 넣지 않는다.

### 6.1 레벨 승급

`TurretLevelUpgradeCatalog`은 현재 Definition과 다음 레벨 프리팹을 연결한다. 현재 카탈로그에는 Stage 1 Canon/Missile의 LV1→LV2, LV2→LV3 경로만 등록되어 있다. `TurretTest`에서 각 터렛의 `Level Up` 버튼으로 확인할 수 있다. Stage 2·3의 승급 경로는 카탈로그에 추가해야 한다.

```csharp
if (catalog.TryGetNext(turret.Definition, out TurretBase nextPrefab))
{
    TurretLevelUpgradeResult result =
        turret.RequestLevelUpgrade(nextPrefab, out TurretBase replacement);
}
```

승급은 다음 레벨 프리팹으로 GameObject를 교체한다. 같은 `InstanceId`, 위치, 현재 체력 비율, 세부 업그레이드 보정 및 적용 이력, 활성 상태와 사거리 표시 설정을 이전한다. 체력 비율을 유지하므로 승급만으로 전체 회복되지는 않는다. 활성 터렛은 새 전력 사용량에 맞춰 예약량을 즉시 변경하며, 전력이 부족하면 원래 터렛을 유지한다. 성공하면 `TurretInstanceRegistry.LevelUpgraded(previous, current)`가 발생한다. 이전 컴포넌트를 직접 보관하는 소비자는 이 이벤트를 받아 새 컴포넌트로 참조를 갱신해야 한다. 이미 발사된 총알·미사일은 기존 발사체로 남는다.

현재 승급 비용·시간, 멀티플레이 상태 동기화, 파괴된 터렛의 승급 규칙은 확정되지 않았다. 파괴된 터렛의 승급 요청은 거부한다. 제품 UI에서 승급 버튼을 노출하는 작업은 아직 별도다.

`TurretTest`에서 수동으로 검증할 때는 Canon LV1 하나의 ID와 ControlUnit 전력을 기록하고 활성화·Damage·세부 업그레이드 후 `Level Up`을 누른다. LV2가 같은 ID와 체력 비율·보정값을 유지하는지 확인하고 LV3까지 반복한다. Missile도 같은 순서로 확인한다. 여러 터렛을 켜 남은 전력을 낮춘 뒤 승급을 요청하면 부족한 전력으로 거부되는지도 확인할 수 있다. 이 Play Mode 검증은 코드 컴파일 검사와 별개로 수행해야 한다.

## 7. 멀티플레이 전환 시 주의점

현재 `InstanceId`는 각 프로세스에서 등록 순서대로 발급되는 **로컬 ID**다. Host와 Client에서 같은 터렛이 반드시 같은 숫자를 갖는다는 보장이 없다.

NGO 연동 시 다음 규칙을 적용한다.

- Host가 터렛 생성과 상태 변경의 Authority를 가진다.
- 네트워크 식별자는 NGO `NetworkObjectId` 또는 Host가 발급한 별도 ID를 사용한다.
- 활성화 요청은 Client가 직접 상태를 바꾸지 않고 Command/RPC로 Host에 요청한다.
- Host가 전력과 조건을 검증한 후 확정 상태를 복제한다.
- Definition 자체를 전송하지 않고 안정적인 `Definition.Id`와 필요한 Snapshot을 사용한다.
- 공격력 보너스와 파괴·수리 상태도 Authority가 확정한다.

현재 Registry의 로컬 `InstanceId`를 그대로 네트워크 ID로 보내면 안 된다. 실제 동기화 계약은 Co-op Milestone에서 `HostAuthority`, `MatchState`, NGO Adapter 경계와 함께 정한다.

## 8. AI·전선 시스템 연동 기준

Enemy PoC의 `PoCTargetSelector`는 `PoCTargetable` 컴포넌트를 수집한다. Stage 1 Canon/Missile Prefab에는 `TurretTargetableAdapter`와 `PoCTargetable`을 함께 붙였다. Adapter는 터렛의 현재·최대 체력과 활성·파괴 상태를 전달한다. 비활성·파괴된 터렛의 `PoCTargetable`은 비활성화되어 목표 후보에서 빠진다. 이 연결은 Enemy PoC 쪽에만 있고 `TeamHJD.Game.Turrets`는 Enemy assembly를 참조하지 않는다.

`FirepowerRatio`는 계산 기준이 아직 정해지지 않아 Adapter의 임시값 0.5를 사용한다. 세부 업그레이드와 레벨 승급으로 `EffectiveDamage`가 바뀌어도 이 비율은 아직 바뀌지 않는다. AI 평가 규칙을 정하면 `PoCTargetable.SetFirepower`에 실제 현재/최대 화력을 전달해야 한다.

전선 시스템과 후속 AI 계약에 제공할 후보 데이터는 다음과 같다.

- Instance ID와 Definition ID
- 위치와 소속 구역
- 활성화 여부
- 현재·최대 체력
- 공격력, 사거리, 전력 비용
- 공격 가능 여부와 과열 상태
- AI 목표 평가용 위협도·방어 가치

현재는 `TurretInstanceRegistry.GetActive()`와 터렛의 공개 읽기 속성으로 위치·상태·기본 수치를 조회할 수 있다. 소속 구역, 위협도·방어 가치와 읽기 전용 Snapshot 계약은 미구현이다. `TurretRuntimeState`에 모든 Scene 참조와 AI 계산 결과를 무조건 넣지 않는다.

## 9. 검증 체크리스트

### 코드·에셋

- [ ] Definition ID가 비어 있지 않고 중복되지 않는다.
- [ ] Prefab에 올바른 Definition이 연결되어 있다.
- [ ] Stage 1 승급 카탈로그의 네 경로가 실제 다음 LV TurretBase Prefab을 가리킨다.
- [ ] Stage 1 Canon/Missile Prefab에 PoCTargetable과 Adapter가 함께 있다.
- [ ] `.cs`·`.asset`·Prefab의 `.meta`가 함께 존재한다.
- [ ] `TeamHJD.Game.Turrets`가 Contracts 외의 legacy assembly를 참조하지 않는다.
- [ ] Level 스크립트에 Definition 수치가 중복 하드코딩되지 않았다.
- [ ] Laser Turret을 실수로 Canon/Missile 변경 범위에 포함하지 않았다.

### Play Mode

- [ ] 6개 레벨별 터렛이 `TurretTest` Scene에서 활성화된다.
- [ ] 전력이 부족하면 활성화가 거부된다.
- [ ] 비활성화하면 전력이 반환된다.
- [ ] `Damage`로 현재 체력이 감소한다.
- [ ] 체력 0에서 파괴되고 예약 전력이 반환된다.
- [ ] 파괴된 터렛은 활성화할 수 없다.
- [ ] `Restore` 후 최대 체력·비활성 상태로 돌아오며 다시 활성화할 수 있다.
- [ ] 파괴와 복구 시 Registry 이벤트가 한 번씩 발생한다.
- [ ] Target이 사망하거나 범위를 벗어나면 새 Target을 찾는다.
- [ ] Canon과 Missile의 Gizmo·실제 탐지 범위가 Definition과 일치한다.
- [ ] 발사체 피해량이 Definition Damage와 Runtime Bonus를 반영한다.
- [ ] 과열 후 냉각과 재활성화가 정상 동작한다.
- [ ] 각 활성 터렛의 `InstanceId`가 0이 아니며 서로 다르다.
- [ ] Canon과 Missile을 각각 LV1→LV2→LV3으로 승급하며 발사구·공격 동작이 바뀐다.
- [ ] 승급 전후 `InstanceId`, 체력 비율, 세부 업그레이드와 활성 상태가 유지된다.
- [ ] 활성 중 승급으로 증가한 전력만 추가 예약되고, 부족하면 승급이 거부된다.
- [ ] 꺼지거나 파괴된 터렛은 Enemy PoC의 목표 후보에서 빠진다.

## 10. 현재 기술 부채와 후속 작업

현재 구현에는 다음 전환기 의존성이 남아 있다.

| 항목 | 현재 상태 | 후속 방향 |
| --- | --- | --- |
| Control Unit 탐색 | `GameObject.Find("ControlUnit")` | Scene Composition에서 명시적으로 주입 |
| 등록부 | static 로컬 Registry | Match 수명주기의 조회 서비스로 이전 |
| 전력 변경 | `ITurretPowerSource`를 통해 legacy `ControlUnitStatus` 호출 | Command와 Authority 검증으로 분리 |
| Target 평가 | concrete 코드가 Physics와 `Monster.isTargeted` 직접 사용 | AI/Combat 계약과 평가 모델 분리 |
| Namespace | 일부 concrete 터렛이 전역 namespace | 이식 시 `Presentation` 경계로 정리 |
| 네트워크 | 로컬 상태만 존재 | Host authoritative 상태와 Snapshot 추가 |
| AI 화력 평가 | Stage 1 Adapter에서 `FirepowerRatio = 0.5` 임시 사용 | 조수빈과 계산식·갱신 시점을 확정 |
| 레벨 승급 | Stage 1 카탈로그와 테스트 UI만 연결 | 제품 UI, 다른 Stage 경로, 비용·시간 및 참조 갱신 정책 검증 |
| Laser | 기존 독립 구조 유지 | 별도 Issue에서 행동 확인 후 이식 |

이 항목을 해결할 때 한 번에 전체 시스템을 다시 쓰지 않는다. observable behavior를 먼저 기록하고, 테스트 가능한 작은 Architecture Slice로 교체한다.

## 11. 관련 파일

- [`TurretBase.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretBase.cs)
- [`TurretDefinition.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretDefinition.cs)
- [`TurretRuntimeState.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretRuntimeState.cs)
- [`TurretUpgradeDefinition.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretUpgradeDefinition.cs)
- [`TurretLevelUpgradeCatalog.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretLevelUpgradeCatalog.cs)
- [`TurretLevelUpgrade_Stage1.asset`](../../Assets/Scripts/Tower/TurretDefinitions/TurretLevelUpgrade_Stage1.asset)
- [`TurretTargetableAdapter.cs`](../../Assets/PoC/Enemy/Scripts/TurretTargetableAdapter.cs)
- [`TurretTestController.cs`](../../Assets/Scripts/Tower/Turret/TurretTestController.cs)
- [`TurretInstanceRegistry.cs`](../../Assets/Scripts/Tower/Turret/Core/TurretInstanceRegistry.cs)
- [`TeamHJD.Game.Turrets.Contracts.asmdef`](../../Assets/Scripts/Tower/Turret/Contracts/TeamHJD.Game.Turrets.Contracts.asmdef)
- [`TeamHJD.Game.Turrets.asmdef`](../../Assets/Scripts/Tower/Turret/Core/TeamHJD.Game.Turrets.asmdef)
- [`DefaultCanonTurret.cs`](../../Assets/Scripts/Tower/CanonTurret/DefaultCanonTurret.cs)
- [`DefaultMissileTurret.cs`](../../Assets/Scripts/Tower/MissileTurret/DefaultMissileTurret.cs)
- [`TowerBullet.cs`](../../Assets/Scripts/Tower/TurretWeapons/TowerBullet.cs)
- [`TowerMissile.cs`](../../Assets/Scripts/Tower/TurretWeapons/TowerMissile.cs)
