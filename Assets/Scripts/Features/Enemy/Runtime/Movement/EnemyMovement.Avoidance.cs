using UnityEngine;
using UnityEngine.AI;

public partial class EnemyMovement
{
    private static readonly float[] AvoidanceAngles = { 45f, -45f, 90f, -90f, 135f, -135f, 180f };
    private Vector2 _avoidanceRequested;
    private Vector2 _avoidanceDirection;
    private float _avoidanceRefreshIn;
    private float _avoidanceSide;
    private float _avoidanceSideRemaining;

    private EnemyTargeting _steeringTargeting;
    private readonly System.Collections.Generic.List<Collider2D> _nearbyFeet = new();
    private Vector3[] _progressPath;
    private int _progressCorner;
    private float _bestPathDistance;
    private float _stalledMovementTime;
    private int _controlApproachCursor = -1;

    private void RefreshStalledMovement()
    {
        if (!HasPath)
            return;

        float distance = Vector2.Distance(transform.position, _path[_currentPath]);
        if (_progressPath != _path || _progressCorner != _currentPath ||
            distance < _bestPathDistance - 0.1f)
        {
            _progressPath = _path;
            _progressCorner = _currentPath;
            _bestPathDistance = distance;
            _stalledMovementTime = 0f;
            return;
        }

        _stalledMovementTime += Time.fixedDeltaTime;
        if (_stalledMovementTime < 1.5f)
            return;

        _stalledMovementTime = 0f;
        if (_steeringTargeting == null || _steeringTargeting.Target == null)
            return;
        if (!TryAlternativeFacilityPath())
            TryUpdatePath(_steeringTargeting.Target, allowSharedRoute: false);
    }

    private bool TryAlternativeFacilityPath()
    {
        if (_footCollider == null || _steeringTargeting == null ||
            _steeringTargeting.Target == null || _steeringTargeting.TargetComponent == null ||
            _steeringTargeting.TargetComponent.TargetType != TargetType.Turret)
            return false;

        Transform target = _steeringTargeting.Target;
        Bounds bounds = default;
        bool found = false;
        float nearest = float.PositiveInfinity;
        foreach (Collider2D collider in _steeringTargeting.TargetColliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
                continue;
            if (!found)
                bounds = collider.bounds;
            else
                bounds.Encapsulate(collider.bounds);
            found = true;
            nearest = Mathf.Min(nearest, Vector2.Distance(transform.position, collider.ClosestPoint(transform.position)));
        }
        if (!found)
            return false;

        int type = GetNavMeshAgentTypeId(gameObject.scene);
        float radius = NavMesh.GetSettingsByID(type).agentRadius;
        if (nearest > MoveSpeed + radius * 2f)
            return false;
        var query = new NavMeshQueryFilter { agentTypeID = type, areaMask = NavMesh.AllAreas };
        if (!NavMesh.SamplePosition(transform.position, out var origin, 3f + radius * 2f, query))
            return false;

        Vector3 scale = _footCollider.transform.lossyScale;
        Vector2 size = Vector2.Scale(_footCollider.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        Vector2 offset = (Vector2)_footCollider.transform.TransformPoint(_footCollider.offset) - (Vector2)transform.position;
        var contactFilter = new ContactFilter2D { useTriggers = false };
        contactFilter.SetLayerMask(1 << _footCollider.gameObject.layer);
        var physics = gameObject.scene.GetPhysicsScene2D();
        bool controlUnit = target.GetComponent<ControlUnitHealth>() != null;
        int perSide = controlUnit ? 8 : 4;
        int slots = perSide * 4;
        if (_controlApproachCursor < 0)
            _controlApproachCursor = Mathf.Abs(GetInstanceID() % slots);

        Vector3[] best = null;
        float bestCost = float.PositiveInfinity;
        for (int candidate = 0; candidate < slots; candidate++)
        {
            int slot = (_controlApproachCursor + candidate) % slots;
            int side = slot / perSide;
            float t = (slot % perSide + 0.5f) / perSide;
            Vector2 point;
            Vector2 outward;
            if (side < 2)
            {
                outward = side == 0 ? Vector2.up : Vector2.down;
                point = new Vector2(Mathf.Lerp(bounds.min.x, bounds.max.x, t), side == 0 ? bounds.max.y : bounds.min.y);
            }
            else
            {
                outward = side == 2 ? Vector2.right : Vector2.left;
                point = new Vector2(side == 2 ? bounds.max.x : bounds.min.x, Mathf.Lerp(bounds.min.y, bounds.max.y, t));
            }

            Vector2 external = point + outward * (radius + 1f);
            Vector2 edge = point;
            float closest = float.PositiveInfinity;
            foreach (Collider2D collider in _steeringTargeting.TargetColliders)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || collider.isTrigger)
                    continue;
                Vector2 surface = collider.ClosestPoint(external);
                float distance = (surface - external).sqrMagnitude;
                if (distance >= closest)
                    continue;
                closest = distance;
                edge = surface;
            }

            float extent = Mathf.Abs(outward.x) * size.x * 0.5f + Mathf.Abs(outward.y) * size.y * 0.5f;
            Vector2 entry = edge + outward * (extent + 0.15f) - offset;
            if (!NavMesh.SamplePosition(entry, out var goal, radius + 1f, query))
                continue;
            var route = new NavMeshPath();
            if (!NavMesh.CalculatePath(origin.position, goal.position, query, route) ||
                route.status != NavMeshPathStatus.PathComplete)
                continue;
            Vector3[] corners = route.corners;
            Vector2 contact = edge - offset - outward * 0.25f;
            Vector3[] approach = AddFacilityApproach(target, corners, contact);
            if (approach.Length == corners.Length)
                continue;

            physics.OverlapBox(entry + offset, size * 1.5f, _footCollider.transform.eulerAngles.z,
                contactFilter, _nearbyFeet);
            int occupied = 0;
            foreach (Collider2D collider in _nearbyFeet)
                if (collider.attachedRigidbody != rigidbody2D)
                    occupied++;

            // 실제 공격 자리와 마지막 접근로를 함께 확인해 뒤에서 같은 줄을 고르지 않게 한다.
            Vector2 lastLeg = entry - (Vector2)goal.position;
            physics.BoxCast((Vector2)goal.position + offset, size, _footCollider.transform.eulerAngles.z,
                lastLeg.normalized, lastLeg.magnitude, contactFilter, _movementHits);
            foreach (RaycastHit2D hit in _movementHits)
                if (hit.rigidbody != rigidbody2D)
                    occupied++;

            float cost = occupied * 30f;
            Vector2 previous = transform.position;
            foreach (Vector3 corner in corners)
            {
                cost += Vector2.Distance(previous, corner);
                previous = corner;
            }
            if (cost >= bestCost)
                continue;
            bestCost = cost;
            best = approach;
        }
        _controlApproachCursor = (_controlApproachCursor + 1) % slots;
        if (best == null)
            return false;
        _path = best;
        _currentPath = 0;
        _facilitySpreadEnd = 0;
        return true;
    }


    private Vector2 AvoidBlockedMovement(Vector2 requested)
    {
        if (_footCollider == null || !_footCollider.enabled || !_footCollider.gameObject.activeInHierarchy)
            return requested;

        RefreshStalledMovement();
        _avoidanceRefreshIn -= Time.fixedDeltaTime;
        _avoidanceSideRemaining -= Time.fixedDeltaTime;
        if (_avoidanceRefreshIn > 0f)
        {
            if (_avoidanceDirection == Vector2.zero)
                return requested;
            float forward = Vector2.Dot(_avoidanceDirection, _avoidanceRequested);
            float side = _avoidanceRequested.x * _avoidanceDirection.y -
                _avoidanceRequested.y * _avoidanceDirection.x;
            return requested * forward + new Vector2(-requested.y, requested.x) * side;
        }

        _avoidanceRefreshIn = 0.1f;
        _avoidanceRequested = requested;

        float speed = MoveSpeed;
        float lookAhead = Mathf.Max(0.25f, speed * 0.15f);
        float contactOffset = Physics2D.defaultContactOffset;
        ContactFilter2D filter = GetFootCastFilter();
        Vector2 center = _footCollider.bounds.center;
        float forwardClearance = GetMovementClearance(requested, lookAhead, filter, center, contactOffset);
        if (forwardClearance >= lookAhead)
        {
            _avoidanceDirection = requested;
            return requested;
        }

        float stepDistance = speed * Time.fixedDeltaTime + contactOffset;
        Vector2 best = requested;
        float bestScore = forwardClearance >= stepDistance
            ? 1f + 2f * forwardClearance / lookAhead
            : float.NegativeInfinity;

        SteeringOrigin steeringOrigin = default;
        foreach (float angle in AvoidanceAngles)
        {
            Vector2 candidate = Quaternion.Euler(0f, 0f, angle) * requested;
            float side = Mathf.Sign(requested.x * candidate.y - requested.y * candidate.x);
            float sidePenalty = _avoidanceSideRemaining > 0f && side != _avoidanceSide ? 2f : 0f;
            float forward = Vector2.Dot(requested, candidate);
            float continuity = 0.4f * Vector2.Dot(_avoidanceDirection, candidate);
            float maximumScore = forward + 2f * lookAhead / lookAhead + continuity - sidePenalty;
            if (maximumScore <= bestScore)
                continue;

            float clearance = GetMovementClearance(candidate, lookAhead, filter, center, contactOffset);
            if (clearance < stepDistance)
                continue;

            float score = forward + 2f * clearance / lookAhead + continuity - sidePenalty;
            if (score <= bestScore)
                continue;

            if (!steeringOrigin.IsInitialized)
                steeringOrigin = GetSteeringOrigin();
            if (!steeringOrigin.CanSteer(candidate, lookAhead))
                continue;

            bestScore = score;
            best = candidate;
        }

        if (_avoidanceSideRemaining <= 0f && best != requested)
        {
            _avoidanceSide = Mathf.Sign(requested.x * best.y - requested.y * best.x);
            _avoidanceSideRemaining = 1.25f;
        }
        _avoidanceDirection = best;
        return best;
    }

    private ContactFilter2D GetFootCastFilter()
    {
        var filter = new ContactFilter2D { useTriggers = false };
        int layers = Physics2D.GetLayerCollisionMask(_footCollider.gameObject.layer) | _footCollider.includeLayers;
        filter.SetLayerMask(layers & ~_footCollider.excludeLayers);
        return filter;
    }

    private float GetMovementClearance(Vector2 direction, float distance, ContactFilter2D filter,
        Vector2 center, float contactOffset)
    {
        _footCollider.Cast(direction, filter, _movementHits, distance);
        float clearance = distance;
        foreach (RaycastHit2D hit in _movementHits)
        {
            if (hit.distance >= clearance || hit.rigidbody == rigidbody2D)
                continue;
            if (hit.distance <= 2f * contactOffset &&
                Vector2.Dot(direction, (Vector2)hit.collider.bounds.center - center) <= 0f)
                continue;

            clearance = hit.distance;
        }
        return clearance;
    }

    private SteeringOrigin GetSteeringOrigin()
    {
        var target = _steeringTargeting;
        if (!HasPath || (_currentPath == _path.Length - 1 && target != null &&
            target.TargetComponent != null && target.TargetComponent.TargetType == TargetType.Turret))
            return new SteeringOrigin(SteeringMode.Unrestricted);

        var filter = new NavMeshQueryFilter
        {
            agentTypeID = GetNavMeshAgentTypeId(gameObject.scene),
            areaMask = NavMesh.AllAreas
        };
        Vector2 position = transform.position;
        if (NavMesh.SamplePosition(position, out NavMeshHit origin, 0.5f, filter))
            return new SteeringOrigin(SteeringMode.OnMesh, position, origin.position, filter);

        float radius = 3f + NavMesh.GetSettingsByID(filter.agentTypeID).agentRadius * 2f;
        if (NavMesh.SamplePosition(position, out NavMeshHit recovery, radius, filter))
            return new SteeringOrigin(SteeringMode.Recovering, position, recovery.position, filter);

        return new SteeringOrigin(SteeringMode.Blocked);
    }

    private enum SteeringMode { NotChecked, Unrestricted, OnMesh, Recovering, Blocked }

    private readonly struct SteeringOrigin
    {
        private readonly SteeringMode _mode;
        private readonly Vector2 _position;
        private readonly Vector3 _origin;
        private readonly NavMeshQueryFilter _filter;

        public SteeringOrigin(SteeringMode mode, Vector2 position = default, Vector3 origin = default,
            NavMeshQueryFilter filter = default)
        {
            _mode = mode;
            _position = position;
            _origin = origin;
            _filter = filter;
        }

        public bool IsInitialized => _mode != SteeringMode.NotChecked;

        public bool CanSteer(Vector2 direction, float distance)
        {
            switch (_mode)
            {
                case SteeringMode.Unrestricted:
                    return true;
                case SteeringMode.Recovering:
                    return Vector2.Dot(direction, ((Vector2)_origin - _position).normalized) >= -0.1f;
                case SteeringMode.OnMesh:
                    return NavMesh.SamplePosition(_position + direction * distance, out var destination, 0.1f, _filter) &&
                        !NavMesh.Raycast(_origin, destination.position, out _, _filter);
                default:
                    return false;
            }
        }
    }
}
