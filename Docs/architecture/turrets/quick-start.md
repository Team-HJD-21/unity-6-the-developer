# 빠른 시작

최종 갱신: 2026-10-14
상태: Canon / Missile 로컬 런타임 통합 안내.

최신 조작 조건은 main `8d08001a`에 병합된 PR #525를 기준으로 한다.
전체 계약은 [API 사용법](api-reference.md), Initial의 버튼 연결은
[Player UI API](../player-ui-api.md)를 참고한다.

UI는 ON 전에 잠금·파괴·냉각·자동 활성화 여부를 확인한다. 활성 과열 중에도 OFF는
가능하지만 자동 활성화 진행 중에는 수동 OFF도 막힌다. 일반 OFF는 즉시 전력 반환을 시작하고 남은 과열을 동시에 냉각한다. `IsCoolingDown`과
`CoolingDownRemaining`으로 남은 냉각 시간을 표시한다. 레벨과 세부 강화 버튼은
`CanModifyUpgrades`가 true인 비활성·냉각 완료 상태에서 사용한다. 전력 예약과
강화 값은 직접 변경하지 않고 API 요청 결과를 처리한다.

## 씬 준비

1. 기존 Canon 또는 Missile Prefab을 배치한다. Definition과 발사 지점 및 투사체 참조를 유지한다.
2. 씬에 `FieldTurretController`를 배치하고 최대 전력을 설정한다.
3. 터렛 Inspector의 컨트롤러 참조에 해당 컨트롤러를 지정한다. Additive 씬에서 같은 예산을 쓰려면 같은 컨트롤러를 지정한다.
4. 활성화 요청 결과와 등록된 스냅샷을 확인한다.

컨트롤러 참조가 없으면 터렛 자신의 씬에서 활성 컨트롤러를 찾는다. 없으면 기본 전력 100인 컨트롤러를 생성한다. 자동 생성은 테스트 편의 기능이다. 제품 씬에서는 명시적으로 배치하는 것을 권장한다.

Canon과 Missile의 전력 공급에는 CU가 필수는 아니다. 기존 CU와 UI를 함께 쓰면 CU도 같은 컨트롤러를 참조해야 한다. 기존 CU의 `Initialize Power From Legacy Data`가 켜져 있으면 Start에서 기존 데이터의 전력 용량이 적용된다.

## 조회 후 활성화

```csharp
using TeamHJD.Game.FieldTurrets;

// turretController는 Inspector 등에서 주입한 씬 컨트롤러다.
foreach (var snapshot in FieldTurretInstanceRegistry.GetAllSnapshots())
{
    if (snapshot.IsDestroyed || snapshot.IsLocked)
        continue;

    var result = turretController.RequestActivation(snapshot.InstanceId, true);
    // result를 확인한다. 다른 컨트롤러 소속이면 요청이 거부된다.
}
```

위 예제는 API 연결 방법이다. 모든 터렛을 자동으로 켜는 제품 정책은 아니다. 전력 부족과 잠금 및 파괴 상태에 따른 요청 실패를 처리해야 한다.

## 적과의 연동

추적 대상은 `EnemyController`다. 피해 대상은 `EnemyHealth`이며 `TakeDamage(int)`를 호출한다. 현재 Enemy PoC의 피해 처리는 네트워크 스폰과 서버 실행 조건을 확인하므로 터렛만 배치한 오프라인 씬에서 적 체력 감소까지 보장되지 않는다.

구체적인 프리팹 설정과 공격 흐름은 [전체 가이드](../turret-system-guide.md)를 확인한다.
