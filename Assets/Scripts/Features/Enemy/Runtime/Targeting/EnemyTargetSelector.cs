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
        /// <param name="origin">거리를 비교할 기준 위치.</param>
        /// <param name="candidates">호출자가 준비한 타깃 후보 목록.</param>
        /// <param name="requiredType">허용할 타깃 유형. null이면 유형을 제한하지 않는다.</param>
        /// <param name="target">조건을 만족하는 최근접 후보. 없으면 null.</param>
        /// <param name="attackerCount">필요한 집계 인원. 0이면 인원 제한을 검사하지 않는다.</param>
        /// <returns>후보를 찾았으면 true. 실제 인원 배정은 호출자가 처리한다.</returns>
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

                // 봄버 전용 분대와 Encounter 판단용 조회는 인원 0으로 이 검사를 생략한다.
                if (attackerCount > 0 &&
                    attackerCount > candidate.MaxAttackers - candidate.AssignedAttackerCount)
                    continue;

                // 렌더링 정렬용 Z는 제외하고 XY 평면에서만 거리를 비교한다.
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
