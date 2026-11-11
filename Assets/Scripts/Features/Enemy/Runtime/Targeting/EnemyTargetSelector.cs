using System.Collections.Generic;
using UnityEngine;

namespace TeamHJD.Game.Enemies.Targeting
{
    /// <summary>전달받은 후보에서 가장 가까운 유효 타깃을 찾는다.</summary>
    public static class EnemyTargetSelector
    {
        /// <summary>
        /// 전달된 후보 중 유형과 선택적 배정 인원 조건을 만족하는 타깃을 XY 거리로 선택한다.
        /// </summary>
        public static bool TryFindNearest(
            Vector3 origin,
            IReadOnlyList<TargetableComponent> candidates,
            TargetType? requiredType,
            out ITargetable target,
            int attackerCount = 0)
        {
            target = null;
            float nearestDistanceSquared = float.PositiveInfinity;

            foreach (TargetableComponent candidate in candidates)
            {
                if (candidate == null || !candidate.CanBeTargeted ||
                    candidate.TargetTransform == null ||
                    (requiredType.HasValue && candidate.TargetType != requiredType.Value))
                    continue;

                // 분대 전체 인원을 받을 수 없는 후보는 제외한다.
                if (attackerCount > 0 &&
                    attackerCount > candidate.MaxAttackers - candidate.AssignedAttackerCount)
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
}
