# 스냅샷

최종 갱신: 2026-10-03
상태: 조회 시점 복사본. 실시간으로 자동 갱신되는 참조가 아니다.

## TurretSnapshot — 터렛 하나

| 필드 | 의미 |
| --- | --- |
| `InstanceId` / `DefinitionId` | 런타임 ID / 정의 ID |
| `Position` | 월드 위치 |
| `IsActivated` / `IsOperational` | 켜진 상태 / 실제 작동 가능 상태 |
| `IsDestroyed` / `IsLocked` | 파괴 / 잠금 상태 |
| `CurrentHealth` / `MaxHealth` | 현재 / 최대 체력 |
| `EffectiveDamage` / `EffectivePower` | 보정 적용 공격력 / 요구 전력 |
| `Range` | 보정 적용 외부 사거리 |
| `SelectedUpgradeId` / `UpgradeLevel` | 선택한 세부 업그레이드 / 단계 |

`IsActivated`가 true여도 과열 중이라면 작동하지 않을 수 있다. `Range`는 외부 사거리이며 최소 사거리 필드를 포함하지 않는다. 현재 스냅샷에는 `TurretKind`도 포함되지 않는다.

## TurretControllerSnapshot — 전력과 집계

```csharp
var snapshot = turretController.GetSnapshot();
int availablePower = snapshot.AvailablePower;
int activeCanonCount = snapshot.GetActivatedCount(TurretKind.Canon);
```

| 필드 | 의미 |
| --- | --- |
| `MaximumPower` | 최대 전력 |
| `AvailablePower` | 지금 예약할 수 있는 전력 |
| `ReservedPower` | 사용 중인 예약 전력 |
| `PendingRecoveryPower` | 반환을 기다리는 전력 |
| `RegisteredTurretCount` | 해당 컨트롤러를 사용하는 등록 터렛 수 |
| `ActivatedTurretCount` | 활성 상태인 비파괴 터렛 수 |
| `OperationalTurretCount` | 작동 가능한 터렛 수 |
| `ActivatedCountByType` | 종류별 활성 수의 읽기 전용 복사본 |
| `GetActivatedCount(TurretKind)` | 해당 종류의 활성 수. 없으면 0 |

전력은 `MaximumPower = AvailablePower + ReservedPower + PendingRecoveryPower`로 구분한다. 끈 터렛의 전력이 즉시 가용 전력으로 전부 돌아오는 것은 아니다.

Legacy Laser는 같은 예산을 소비할 수 있지만 Registry 집계 대상은 아니다. 따라서 예약 전력과 집계된 터렛의 요구 전력 합계가 반드시 같지는 않다. 종류 enum에 Laser와 Tesla가 있다는 것만으로 이식 완료를 뜻하지 않는다.

## 보관과 갱신

스냅샷은 조회 당시의 값이다. 최신 값이 필요할 때 다시 조회한다. 매 프레임 전체 목록을 반복 복사하기보다 소비자의 갱신 주기에 맞게 조회하거나 변경 이벤트로 캐시를 무효화한다. 모든 소비자가 동일한 갱신 주기를 써야 한다는 뜻은 아니다.
