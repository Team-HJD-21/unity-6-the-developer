# API 사용법

최종 갱신: 2026-10-13
상태: 현재 Canon / Missile 공개 API. 명령용 호환 API는 컴포넌트 참조를 사용할 수 있다.

네임스페이스는 `TeamHJD.Game.Turrets`다. 결과 타입과 터렛 종류는 `TeamHJD.Game.Turrets.Contracts`에 있다. 소비자 asmdef에서 사용하는 어셈블리를 참조해야 한다.

## ID와 읽기 전용 조회

`TurretInstanceRegistry`는 static 클래스다. 별도 씬 인스턴스를 만들지 않는다.

| API | 용도 |
| --- | --- |
| `GetAllSnapshots()` | 등록된 전체 터렛 복사본 |
| `GetActiveSnapshots()` | 활성 터렛 복사본 |
| `GetOperationalSnapshots()` | 실제 작동 가능한 터렛 복사본 |
| `TryGetSnapshot(int, out TurretSnapshot)` | ID 하나의 최신 값 |
| `TryGetInstanceId(Transform, out int)` | 자식 Transform에서 등록된 부모 터렛 ID 조회 |
| `SnapshotChanged` | 값 변경 또는 등록 해제 후 다시 조회할 ID 통지 |

```csharp
if (TurretInstanceRegistry.TryGetInstanceId(hit.transform, out int turretId)
    && TurretInstanceRegistry.TryGetSnapshot(turretId, out var snapshot))
{
    // snapshot.CurrentHealth와 snapshot.Position 등을 사용한다.
}
```

ID는 로컬 런타임 식별자다. Network ID나 저장 데이터 ID가 아니다. 현재 프리팹 교체 방식의 승급과 강등에서는 기존 ID를 유지한다. 씬 제거 후 보관한 ID는 유효하지 않을 수 있다.

## 컨트롤러 명령

| API | 결과와 의미 |
| --- | --- |
| `RequestActivation(int instanceId, bool shouldActivate)` | `TurretActivationResult`. 해당 컨트롤러 소속 터렛에 요청 |
| `GetSnapshot()` | 컨트롤러 전력과 터렛 집계 복사본 |
| `TrySetMaximumPower(int)` | `bool`. 사용 중인 예약을 보존하며 용량 변경 |

없는 ID 또는 다른 전력 공급자를 사용하는 터렛의 활성화 요청은 `PowerSourceUnavailable`이다. `GetSnapshot()`은 전역 집계가 아니라 해당 컨트롤러를 사용하는 등록 터렛의 집계다.

## 피해와 복구 및 업그레이드

Enemy처럼 일반 터렛과 CU를 같은 방식으로 공격하는 소비자는 `TeamHJD.Game.Turrets.Contracts.IDamageableTarget`을 사용한다.

| IDamageableTarget API | 의미 |
| --- | --- |
| `CurrentHealth` / `MaxHealth` | 현재 체력과 Definition 기준 최대 체력 |
| `IsDestroyed` | 체력이 0인 상태 |
| `ApplyDamage(int)` | 유효한 피해가 적용됐는지 반환 |

`TurretBase`와 `ControlUnitHealth`가 이 계약을 구현한다. CU는 이 계약만 공유하며 Registry, 활성화, 전력 명령에는 포함되지 않는다.

아래는 현재 `TurretBase`의 명령 API다. **읽기 전용 조회와 달리 컴포넌트를 조회해서 호출하는 호환 경로**다. 모든 명령이 ID 기반 전용 인터페이스로 분리된 상태는 아니다.

```csharp
if (TurretInstanceRegistry.TryGet(targetTurretId, out var turret))
{
    bool applied = turret.ApplyDamage(damage);
}
```

| TurretBase API | 반환 타입 |
| --- | --- |
| `RequestActivation(bool)` | `TurretActivationResult` |
| `ApplyDamage(int)` | `bool` |
| `Restore()` | `bool` |
| `SetLocked(bool)` | `bool` |
| `ApplyUpgrade(TurretUpgradeDefinition)` | `TurretUpgradeResult` |
| `DowngradeUpgrade(TurretUpgradeDefinition)` | `TurretUpgradeResult` |

세부 업그레이드·되돌리기와 레벨 승급·강등은 **비활성화 상태이며 냉각이 끝난 터렛**에만 허용한다. 켜져 있으면 `RequiresDeactivation`, 꺼져 있지만 냉각 중이면 `CoolingDown`, 자동 활성화 진행 중이면 `AutoActivationInProgress`를 반환한다. 거부 시 기존 객체·단계·능력치·전력은 변경하지 않는다. 꺼진 터렛의 업그레이드는 요구 전력만 바꾸며, 실제 예약은 다음 활성화에서 수행한다.

UI는 `CanModifyUpgrades`로 승급·강등 및 세부 업그레이드 버튼을 활성화하고, 요청 시 API 결과도 확인한다. `IsOperational == false`는 과열 중 켜진 터렛도 포함하므로 업그레이드 허용 조건으로 사용하지 않는다.

`IsManualOperationBlocked`는 해당 전력 공급자의 자동 활성화가 진행 중인지 나타낸다. 이때 개별 활성화, 세부/레벨 변경, `SetLocked`, `Restore`, 공격력 보너스 변경을 거부한다. 기존 `IsManualActivationBlocked`는 같은 값을 반환하는 호환 속성이다. bool API는 `false`, void 보너스 API는 미적용으로 처리하며 Console에 거부 로그를 남긴다. 적의 `ApplyDamage`, 자동 처리 내부 활성화 및 게임 규칙의 구역 해금 호출은 계속 처리한다. 개발 UI에서 피해 주입·구역 변경 등 시스템 API를 수동으로 호출하는 버튼도 진행 중에는 비활성화한다.

체력이 0이면 파괴되고 비활성화된다. Restore는 최대 체력과 비활성 상태로 복구한다. 복구 이후 활성화는 별도로 요청한다. 잠금과 해금의 제품 조건은 호출자가 결정한다.

레벨 승급과 강등은 `RequestLevelUpgrade`와 `RequestLevelDowngrade`로 프리팹 교체를 요청한다. 매개변수와 상태 승계 규칙은 [전체 가이드](../turret-system-guide.md)의 레벨 승급 항목을 확인한다.

## 변경 이벤트

`SnapshotChanged`에서 받은 ID를 다시 조회한다. 등록 해제된 ID라면 `TryGetSnapshot`은 false이므로 소비자의 캐시에서도 제거한다. 이벤트 하나를 게임 규칙상의 변경 한 번과 동일하게 취급하지 않는다. 같은 요청에서 여러 번 통지될 수 있다.

Unity 메인 스레드에서 조회한다. 구독한 소비자는 종료 시 구독을 해제한다.
