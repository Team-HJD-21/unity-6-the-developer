# API 사용법

최종 갱신: 2026-10-25
상태: 현재 Canon / Missile 공개 API. 명령용 호환 API는 컴포넌트 참조를 사용할 수 있다.

이 문서의 최신 냉각·강화 제한 기준은 main `8d08001a`에 병합된 PR #525다.
지속 수리 API는 PR #547로 main에 반영됐다. 플레이어 입력·실제 Stage 연결은 별도다.

네임스페이스는 `TeamHJD.Game.FieldTurrets`다. 결과 타입과 터렛 종류는 `TeamHJD.Game.FieldTurrets.Contracts`에 있다. 소비자 asmdef에서 사용하는 어셈블리를 참조해야 한다.

## ID와 읽기 전용 조회

Core 지역이 바인딩된 Scene은 `FieldTurretController.Regions`를 읽기 원본으로 사용한다. 기존 UnlockZones/UnlockedZoneCount는 독립 황재동 테스트의 구역 뷰이며 Core 상태를 대신하지 않는다. 바인딩 중 ConfigureUnlockZones는 예외, UnlockThroughZone은 false로 이중 쓰기를 거부한다. 해금은 RegionSceneBinding.TryUnlockThroughZone, 모드/Grid 변경은 확인 후 ResetPolicy로 요청한다. [상세](../core-sprint-region-and-request-api.md)

`FieldTurretInstanceRegistry`는 static 클래스다. 별도 씬 인스턴스를 만들지 않는다.

| API | 용도 |
| --- | --- |
| `GetAllSnapshots()` | 등록된 전체 터렛 복사본 |
| `GetActiveSnapshots()` | 활성 터렛 복사본 |
| `GetOperationalSnapshots()` | 실제 작동 가능한 터렛 복사본 |
| `TryGetSnapshot(int, out FieldTurretSnapshot)` | ID 하나의 최신 값 |
| `TryGetInstanceId(Transform, out int)` | 자식 Transform에서 등록된 부모 터렛 ID 조회 |
| `SnapshotChanged` | 값 변경 또는 등록 해제 후 다시 조회할 ID 통지 |

```csharp
if (FieldTurretInstanceRegistry.TryGetInstanceId(hit.transform, out int turretId)
    && FieldTurretInstanceRegistry.TryGetSnapshot(turretId, out var snapshot))
{
    // snapshot.CurrentHealth와 snapshot.Position 등을 사용한다.
}
```

ID는 로컬 런타임 식별자다. Network ID나 저장 데이터 ID가 아니다. 현재 프리팹 교체 방식의 승급과 강등에서는 기존 ID를 유지한다. 씬 제거 후 보관한 ID는 유효하지 않을 수 있다.

## 컨트롤러 명령

| API | 결과와 의미 |
| --- | --- |
| `RequestActivation(int instanceId, bool shouldActivate)` | `FieldTurretActivationResult`. 해당 컨트롤러 소속 터렛에 요청 |
| `GetSnapshot()` | 컨트롤러 전력과 터렛 집계 복사본 |
| `TrySetMaximumPower(int)` | `bool`. 사용 중인 예약을 보존하며 용량 변경 |

없는 ID 또는 다른 전력 공급자를 사용하는 터렛의 활성화 요청은 `PowerSourceUnavailable`이다. `GetSnapshot()`은 전역 집계가 아니라 해당 컨트롤러를 사용하는 등록 터렛의 집계다.

## 피해와 복구 및 업그레이드

Enemy처럼 일반 터렛과 CU를 같은 방식으로 공격하는 소비자는 `TeamHJD.Game.FieldTurrets.Contracts.IDamageableTarget`을 사용한다.

| IDamageableTarget API | 의미 |
| --- | --- |
| `CurrentHealth` / `MaxHealth` | 현재 체력과 Definition 기준 최대 체력 |
| `IsDestroyed` | 체력이 0인 상태 |
| `ApplyDamage(int)` | 유효한 피해가 적용됐는지 반환 |

`FieldTurretBase`와 `ControlUnitHealth`가 이 계약을 구현한다. CU는 이 계약만 공유하며 Registry, 활성화, 전력 명령에는 포함되지 않는다.

아래는 현재 `FieldTurretBase`의 명령 API다. **읽기 전용 조회와 달리 컴포넌트를 조회해서 호출하는 호환 경로**다. 모든 명령이 ID 기반 전용 인터페이스로 분리된 상태는 아니다.

```csharp
if (FieldTurretInstanceRegistry.TryGet(targetTurretId, out var turret))
{
    bool applied = turret.ApplyDamage(damage);
}
```

| FieldTurretBase API | 반환 타입 |
| --- | --- |
| `RequestActivation(bool)` | `FieldTurretActivationResult` |
| `ApplyDamage(int)` | `bool` |
| `Repair(float healthPerSecond, float deltaTime)` | `FieldTurretRepairResult` |
| `Restore()` | `bool` |
| `Repair(int amount)` | `bool`. 살아 있는 터렛을 부분 회복. 최대 체력 제한, 실제 회복 시 체력·Snapshot 변경 통지 |
| `SetLocked(bool)` | `bool` |
| `ApplyUpgrade(FieldTurretUpgradeDefinition)` | `FieldTurretUpgradeResult` |
| `DowngradeUpgrade(FieldTurretUpgradeDefinition)` | `FieldTurretUpgradeResult` |

세부 업그레이드·되돌리기와 레벨 승급·강등은 **비활성화 상태이며 냉각이 끝난 터렛**에만 허용한다. 켜져 있으면 `RequiresDeactivation`, 꺼져 있지만 냉각 중이면 `CoolingDown`, 자동 활성화 진행 중이면 `AutoActivationInProgress`를 반환한다. 거부 시 기존 객체·단계·능력치·전력은 변경하지 않는다. 꺼진 터렛의 업그레이드는 요구 전력만 바꾸며, 실제 예약은 다음 활성화에서 수행한다.

UI는 `CanModifyUpgrades`로 승급·강등 및 세부 업그레이드 버튼을 활성화하고, 요청 시 API 결과도 확인한다. `IsOperational == false`는 과열 중 켜진 터렛도 포함하므로 업그레이드 허용 조건으로 사용하지 않는다.

`IsManualOperationBlocked`는 해당 전력 공급자의 자동 활성화가 진행 중인지 나타낸다. 이때 개별 활성화, 세부/레벨 변경, `SetLocked`, `Repair`, `Restore`, 공격력 보너스 변경을 거부한다. 기존 `IsManualActivationBlocked`는 같은 값을 반환하는 호환 속성이다. bool API는 `false`, void 보너스 API는 미적용으로 처리하며 Console에 거부 로그를 남긴다. `Repair`는 매 프레임 호출할 수 있으므로 거부 로그 없이 결과값으로 알린다. 적의 `ApplyDamage`, 자동 처리 내부 활성화 및 게임 규칙의 구역 해금 호출은 계속 처리한다. 개발 UI에서 피해 주입·구역 변경 등 시스템 API를 수동으로 호출하는 버튼도 진행 중에는 비활성화한다.

체력이 0이면 파괴되고 비활성화된다. Restore는 최대 체력과 비활성 상태로 복구한다. 복구 이후 활성화는 별도로 요청한다. Initial의 플레이어 UI에서 잠금과 해금을 직접 변경하지 않는다. 구역 해금은 Core의 게임 규칙이 관리한다.

스패너가 사용하는 `Repair(int amount)`는 양수 회복량만 받으며 초기화 전·비활성 컴포넌트·잠금·파괴·자동 활성화 진행 중이면 false다. 정수 HP를 즉시 더하고 실제 체력이 증가했을 때만 true를 반환한다. 아래의 `Repair(float healthPerSecond, float deltaTime)`와 같은 체력 처리 경로를 사용하며, 어느 API로든 최대 체력에 도달하면 남은 소수점 회복량을 비운다. 한 수리 동작에서 두 API를 중복 호출하지 않는다.
살아 있는 터렛의 체력만 회복하고 활성화, 전력 예약, 냉각 상태는 유지한다. 만피에서는 false이며 파괴 복구용 Restore와 구분한다.

레벨 승급과 강등은 `RequestLevelUpgrade`와 `RequestLevelDowngrade`로 프리팹 교체를 요청한다. 매개변수와 상태 승계 규칙은 [전체 가이드](../turret-system-guide.md)의 레벨 승급 항목을 확인한다.

### 플레이어의 지속 수리

플레이어가 상호작용 가능한 무기·도구를 들었는지, 입력을 유지하는지, 대상과 거리가 유효한지는 **플레이어 측에서 검사**한다. 조건이 유지되는 동안 매 갱신에서 다음 API를 한 번 호출한다. 아래는 플레이어 연동 예시이며 기존 입력 코드에 연결된 상태는 아니다.

```csharp
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;

// 플레이어 측 무기·입력·거리 조건을 통과한 Update 경로에서 호출한다.
if (FieldTurretInstanceRegistry.TryGet(targetTurretId, out var turret))
{
    FieldTurretRepairResult result = turret.Repair(
        healthPerSecond: 10f,
        deltaTime: Time.deltaTime);
    // Repaired / Accumulating은 유효한 수리 진행이다.
}
```

- `healthPerSecond`는 초당 HP다. **10은 예시**이며 플레이어 무기·설정값을 인자로 전달하면 된다. 10 HP/초이면 50 HP 회복에 총 5초의 유효 호출 시간이 필요하다.
- `deltaTime`은 이번 호출이 담당하는 게임 시간이다. `Update`에서는 `Time.deltaTime`, `FixedUpdate`에서는 `Time.fixedDeltaTime`을 전달한다. 같은 수리 시간을 두 경로에서 중복 전달하지 않는다. 시간 0은 `InvalidDeltaTime`으로 거부한다.
- 호출을 멈추면 회복도 멈춘다. 코루틴이나 별도 시작·중단 호출이 필요하지 않다. 소수점 회복량은 터렛 내부에 남아 다음 유효 호출에 합산하며, 공개 체력은 정수 HP다.
- 활성화·비활성화·냉각 중 모두 수리할 수 있고, 활성 상태·냉각·전력 예약은 바꾸지 않는다. 여기서 비활성화는 터렛 OFF를 뜻하며, GameObject/컴포넌트 자체가 비활성이면 거부한다.
- 최대 체력을 넘지 않으며, 최대 체력 도달·파괴·복구·새 초기화·레벨 교체 시 소수점 잔량을 비운다. 수리 중 피해를 받아도 별도 피격 중단 규칙은 없다.
- 실제 정수 HP가 증가할 때만 기존 `HealthChanged`와 `SnapshotChanged`를 통지한다. 파괴 후 복구 이벤트 `Restored`는 발행하지 않는다.
- `CanRepair`는 현재 터렛 상태의 허용 여부다. 무기·입력·거리·회복 속도 검사를 대신하지 않으며 API도 호출 시 상태를 다시 검사한다.

| `FieldTurretRepairResult` | 의미 |
| --- | --- |
| `Repaired` | 이번 호출로 정수 HP가 증가함 |
| `Accumulating` | 유효한 진행이지만 누적량이 아직 1 HP 미만임 |
| `FullHealth` | 이미 최대 체력임 |
| `Locked` / `Destroyed` | 잠금 또는 파괴 상태. 파괴 상태에는 별도 `Restore()`가 필요함 |
| `AutoActivationInProgress` | 해당 터렛 전력 공급자의 자동 활성화 진행 중 |
| `NotInitialized` / `Inactive` | 초기화 전 또는 GameObject/컴포넌트 비활성 |
| `InvalidRepairRate` / `InvalidDeltaTime` | 해당 입력이 0 이하이거나 NaN/Infinity임 |

이 API는 로컬 회복 명령이며 네트워크 권한·거리·호출 중복을 검증하지 않는다. 멀티플레이 연결 시 플레이어/서버 측에서 요청을 검증하고 회복을 확정해야 한다. 여러 호출자의 회복량은 합산된다. CU의 `ControlUnitHealth`에는 이 API를 추가하지 않았다.

## 변경 이벤트

### UI에 필요한 냉각과 강화 조회

| 속성 | UI 사용 규칙 |
| --- | --- |
| `IsCoolingDown` / `CoolingDownRemaining` | 활성 과열 회복과 OFF 후 냉각을 함께 조회한다. 남은 시간은 게임 시간이며 표시 주기로 재조회한다. |
| `IsManualOperationBlocked` | 자동 활성화 중 모든 수동 조작을 차단한다. |
| `CanModifyUpgrades` | 세부 강화와 프리팹 레벨 변경의 공통 허용 조건이다. |
| `CanRepair` | 현재 터렛 상태에서 지속 수리를 받을 수 있는지 확인한다. |
| `RuntimeState.SelectedUpgradeId` / `UpgradeLevel` | 선택한 강화 분기와 0~5 단계를 읽는다. 외부에서 직접 변경하지 않는다. |

OFF는 활성 과열 중에도 요청할 수 있다. ON은 꺼진 터렛의 냉각·잠금·파괴 또는
자동 활성화 중에는 차단한다. 비활성 버튼의 사유는 별도 상태 텍스트나 툴팁에 표시한다.
냉각 종료와 Transform 이동을 `SnapshotChanged` 이벤트만으로 감지한다고 가정하지 않는다.

강화 0에서는 두 분기를 모두 선택할 수 있다. 1~5에서는 같은 분기만 증가·감소하며
5에서는 증가를 막는다. `HasAppliedUpgrade(asset.Id)`인 분기만 감소할 수 있고,
0으로 되돌리면 분기 선택이 초기화된다. 데이터와 허용 조건은 매 요청 직전에 재검사한다.

### 이벤트 수명

`SnapshotChanged`에서 받은 ID를 다시 조회한다. 등록 해제된 ID라면 `TryGetSnapshot`은 false이므로 소비자의 캐시에서도 제거한다. 이벤트 하나를 게임 규칙상의 변경 한 번과 동일하게 취급하지 않는다. 같은 요청에서 여러 번 통지될 수 있다.

Unity 메인 스레드에서 조회한다. 구독한 소비자는 종료 시 구독을 해제한다.
