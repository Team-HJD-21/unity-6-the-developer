using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.FieldTurrets
{
    public static class FieldTurretTargetingUtility
    {
        public const string TerrainObstacleTag = "FieldTurretBulletObstacle";
        public static readonly int TerrainObstacleMask = LayerMask.GetMask("Default", "Forest");
        public static readonly int EnemyHitBoxMask = LayerMask.GetMask("EnemyHitBox");

        private static readonly int StructureSightObstacleMask = LayerMask.GetMask("CU", "TurretBase");
        private static readonly ContactFilter2D SightFilter = new()
        {
            useLayerMask = true,
            layerMask = TerrainObstacleMask | StructureSightObstacleMask,
            useTriggers = false
        };
        private static readonly List<RaycastHit2D> SightHits = new();

        internal static bool HasClearLineOfSight(Vector2 origin, Vector2 target, Transform observer)
        {
            Scene scene = observer.gameObject.scene;
            int hitCount = scene.GetPhysicsScene2D().Linecast(origin, target, SightFilter, SightHits);
            for (int index = 0; index < hitCount; index++)
            {
                Collider2D obstacle = SightHits[index].collider;
                if (obstacle == null || obstacle.gameObject.scene != scene || obstacle.transform.IsChildOf(observer))
                    continue;

                int layer = 1 << obstacle.gameObject.layer;
                if ((StructureSightObstacleMask & layer) != 0 || obstacle.CompareTag(TerrainObstacleTag))
                    return false;
            }

            return true;
        }

        public static bool IsEnemyHitBox(Collider2D collider)
            => (EnemyHitBoxMask & (1 << collider.gameObject.layer)) != 0;

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
            => RotateTowards(rotationPoint, originPosition, targetPosition, rotationSpeed, Time.deltaTime);

        public static void RotateTowards(
            Transform rotationPoint,
            Vector3 originPosition,
            Vector3 targetPosition,
            float rotationSpeed,
            float deltaTime)
        {
            float angle = Mathf.Atan2(
                targetPosition.y - originPosition.y,
                targetPosition.x - originPosition.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(0f, 0f, angle);
            rotationPoint.rotation = Quaternion.RotateTowards(
                rotationPoint.rotation,
                targetRotation,
                rotationSpeed * deltaTime);
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
