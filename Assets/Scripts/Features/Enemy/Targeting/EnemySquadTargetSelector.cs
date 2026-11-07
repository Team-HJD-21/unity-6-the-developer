using UnityEngine;

/// <summary>
/// 분대 중심에서 가장 가까운 공격 가능한 Player 또는 Turret을 찾는다.
/// 현재 PoC는 두 유형에 우선순위를 주지 않고 거리만 비교한다.
/// 개별 적의 점수 기반 선택기는 사용하지 않는다.
/// </summary>
public static class EnemySquadTargetSelector
{
    /// <summary>
    /// 현재 씬의 활성 TargetableComponent를 조회해 가장 가까운 유효 타깃을 반환한다.
    /// </summary>
    /// <param name="origin">분대 구성원 위치의 평균값.</param>
    /// <param name="target">선택한 Player 또는 Turret. 후보가 없으면 null.</param>
    /// <returns>공격 가능한 후보를 찾았다면 true.</returns>
    public static bool TrySelectNearestTarget(Vector3 origin, out ITargetable target)
    {
        return TrySelectNearestTarget(origin, null, out target);
    }

    /// <summary>공격 가능한 플레이어만 후보로 삼아 가장 가까운 대상을 선택한다.</summary>
    public static bool TrySelectNearestPlayer(Vector3 origin, out ITargetable target)
    {
        return TrySelectNearestTarget(origin, TargetType.Player, out target);
    }

    private static bool TrySelectNearestTarget(
        Vector3 origin, TargetType? requiredType, out ITargetable target)
    {
        TargetableComponent[] candidates =
            Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None);

        return EnemyTargetQuery.TryFindNearest(origin, candidates, requiredType, out target);
    }
}
