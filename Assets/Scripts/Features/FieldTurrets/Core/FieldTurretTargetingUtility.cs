using System.Collections.Generic;
using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.FieldTurrets
{
    public static class FieldTurretTargetingUtility
    {
        public const string TerrainObstacleTag = "FieldTurretBulletObstacle";
        public static readonly int TerrainObstacleMask = LayerMask.GetMask("Default", "Forest");
        public static readonly int EnemyHitBoxMask = LayerMask.GetMask("EnemyHurtbox");

        private static readonly int SightBlockerMask = LayerMask.GetMask("CU", "TurretBase", "TurretSightBlocker");
        private static readonly ContactFilter2D SightFilter = new()
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask("Default") | SightBlockerMask,
            useTriggers = false
        };
        private static readonly List<RaycastHit2D> SightHits = new();
        private static readonly List<(Collider2D Collider, float Distance)> TargetDistances = new();

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
                if ((SightBlockerMask & layer) != 0 || obstacle.CompareTag(TerrainObstacleTag))
                    return false;
            }

            return true;
        }

        public static bool CanAttackEnemy(Transform target)
        {
            if (target == null)
                return false;

            IAttackableEnemy enemy = target.GetComponentInParent<IAttackableEnemy>();
            return enemy != null && enemy.CanBeAttackedBy(AttackSourceType.Turret);
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
            if (range <= 0f)
                return;

            var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
            filter.SetLayerMask(targetMask);
            int count = Physics2D.OverlapCircle(origin, range, filter, results);
            float minimum = Mathf.Max(0f, minimumRange);
            float minSquared = minimum * minimum;
            float maxSquared = range * range;
            for (int index = 0; index < count; index++)
            {
                Collider2D candidate = results[index];
                if (candidate == null)
                    continue;

                Vector2 offset = (Vector2)candidate.transform.position - origin;
                float distanceSquared = offset.sqrMagnitude;
                if (distanceSquared >= minSquared && distanceSquared <= maxSquared)
                    TargetDistances.Add((candidate, Mathf.Sqrt(distanceSquared)));
            }

            TargetDistances.Sort(static (left, right) => left.Distance.CompareTo(right.Distance));
            results.Clear();
            foreach (var target in TargetDistances)
                results.Add(target.Collider);
            TargetDistances.Clear();
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
