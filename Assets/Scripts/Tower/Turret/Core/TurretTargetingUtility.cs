using System.Collections.Generic;
using UnityEngine;

namespace TeamHJD.Game.Turrets
{
    public static class TurretTargetingUtility
    {
        public static void CollectByDistance(
            Vector2 origin,
            float range,
            LayerMask targetMask,
            List<Collider2D> results,
            float minimumRange = 0f)
        {
            results.Clear();
            results.AddRange(Physics2D.OverlapCircleAll(origin, range, targetMask));
            results.RemoveAll(candidate => candidate == null ||
                !IsInAttackRange(origin, candidate.transform.position, minimumRange, range));
            results.Sort((left, right) =>
                Vector2.Distance(origin, left.transform.position)
                    .CompareTo(Vector2.Distance(origin, right.transform.position)));
        }

        public static void RotateTowards(
            Transform rotationPoint,
            Vector3 originPosition,
            Vector3 targetPosition,
            float rotationSpeed)
        {
            float angle = Mathf.Atan2(
                targetPosition.y - originPosition.y,
                targetPosition.x - originPosition.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            rotationPoint.rotation = Quaternion.RotateTowards(
                rotationPoint.rotation,
                targetRotation,
                rotationSpeed * Time.deltaTime);
        }

        public static bool IsInRange(Transform origin, Transform target, float range)
        {
            return target != null &&
                   Vector2.Distance(target.position, origin.position) <= range;
        }

        public static bool IsInAttackRange(
            Vector2 origin, Vector2 target, float minimumRange, float maximumRange)
        {
            if (maximumRange <= 0f)
                return false;

            float minimum = Mathf.Max(0f, minimumRange);
            float distanceSquared = (target - origin).sqrMagnitude;
            return distanceSquared >= minimum * minimum &&
                   distanceSquared <= maximumRange * maximumRange;
        }

        public static bool IsInAttackRange(
            Transform origin, Transform target, float minimumRange, float maximumRange)
        {
            return origin != null && target != null &&
                   IsInAttackRange((Vector2)origin.position, (Vector2)target.position,
                       minimumRange, maximumRange);
        }

        public static bool IsInSight(
            Transform rotationPoint,
            Vector3 originPosition,
            Transform target,
            float angleThreshold)
        {
            if (target == null)
            {
                return false;
            }

            float angleToTarget = Mathf.Atan2(
                target.position.y - originPosition.y,
                target.position.x - originPosition.x) * Mathf.Rad2Deg - 90f;
            float angleDifference = Mathf.DeltaAngle(
                rotationPoint.eulerAngles.z,
                angleToTarget);
            return Mathf.Abs(angleDifference) <= angleThreshold;
        }
    }
}
