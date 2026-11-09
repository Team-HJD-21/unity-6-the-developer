using System.Collections.Generic;
using UnityEngine;

/// <summary>전달받은 후보에서 가장 가까운 유효 타깃을 찾는다.</summary>
public static class EnemyTargetQuery
{
    /// <summary>
    /// 전달된 후보 중 지정 유형에 맞는 유효 타깃을 XY 거리 기준으로 선택한다.
    /// </summary>
    public static bool TryFindNearest(
        Vector3 origin,
        IReadOnlyList<TargetableComponent> candidates,
        TargetType? requiredType,
        out ITargetable target)
    {
        target = null;
        float nearestDistanceSquared = float.PositiveInfinity;

        foreach (TargetableComponent candidate in candidates)
        {
            if (candidate == null || !candidate.CanBeTargeted ||
                candidate.TargetTransform == null ||
                (requiredType.HasValue && candidate.TargetType != requiredType.Value))
                continue;

            Vector2 offset = (Vector2)(candidate.TargetTransform.position - origin);
            float distanceSquared = offset.sqrMagnitude;
            if (distanceSquared >= nearestDistanceSquared)
                continue;

            nearestDistanceSquared = distanceSquared;
            target = candidate;
        }

        return target != null;
    }
}
