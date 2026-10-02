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
        target = null;
        float nearestDistanceSqr = float.PositiveInfinity;

        // 이번 선택 시점에 존재하는 타깃만 조사한다.
        TargetableComponent[] candidates =
            Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None);

        foreach (TargetableComponent candidate in candidates)
        {
            if (candidate == null || !candidate.CanBeTargeted ||
                (candidate.TargetType != TargetType.Player &&
                 candidate.TargetType != TargetType.Turret) ||
                candidate.TargetTransform == null)
                continue;

            // 2D 거리의 제곱을 비교해 가장 가까운 후보만 남긴다.
            Vector2 offset = (Vector2)(candidate.TargetTransform.position - origin);
            float distanceSqr = offset.sqrMagnitude;
            if (distanceSqr >= nearestDistanceSqr)
                continue;

            nearestDistanceSqr = distanceSqr;
            target = candidate;
        }

        return target != null;
    }
}
