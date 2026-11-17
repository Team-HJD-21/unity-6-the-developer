using System.Collections.Generic;
using NavMeshPlus.Components;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 계산한 경로를 따라 Rigidbody2D에 이동 힘을 적용한다.
/// </summary>
public partial class EnemyMovement : MonoBehaviour
{
    // Inspector 설정과 제작 참조
    [SerializeField] private float arrivalDistance = 0.4f;
    [SerializeField] private int navMeshAgentTypeId;
    [SerializeField, Min(0f)] private float pathCornerOffset = 0.75f;

    [Header("이동 설정")]
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float moveAcceleration = 10f;
    [SerializeField, Min(0f)] private float moveDeceleration = 20f;
    [SerializeField, Min(1f)] private float pushSpeedLimitMultiplier = 1.1f;

    // 실행 중 참조와 상태
    private readonly List<RaycastHit2D> _movementHits = new();
    private BoxCollider2D _footCollider;
    private Vector3[] _path;
    private int _currentPath = 0;
    private Vector3[] _shortcutPath;
    private int _shortcutCorner;
    private float _shortcutRefreshIn;
    private int _routeVariant = -1;
    private uint _cornerOffsetSeed;
    private static readonly Unity.Profiling.ProfilerMarker DirectionMarker = new("EnemyMovement.PathDirection");
    private static readonly Unity.Profiling.ProfilerMarker MoveMarker = new("EnemyMovement.Move");
    private static readonly Unity.Profiling.ProfilerMarker BrakeMarker = new("EnemyMovement.Brake");

    /// <summary>감속 효과를 적용하기 전의 설정 이동속도다.</summary>
    public float BaseMoveSpeed => Mathf.Max(0f, moveSpeed);

    /// <summary>현재 감속을 반영한 최대 이동속도다.</summary>
    public float MoveSpeed => BaseMoveSpeed * SlowSpeedMultiplier;

    /// <summary>외부 밀림을 포함한 물리 본체의 현재 속력을 반환한다.</summary>
    public float CurrentSpeed => rigidbody2D != null ? rigidbody2D.linearVelocity.magnitude : 0f;

    /// <summary>아직 도착하지 않은 경유점이 남아 있는지 반환한다.</summary>
    public bool HasPath => _path != null && _currentPath < _path.Length;

    /// <summary>외부 충돌로 허용할 최대 속력을 계산한다.</summary>
    private float PushSpeedLimit =>
        MoveSpeed * Mathf.Max(1f, pushSpeedLimitMultiplier);

    // Unity 생명주기

    /// <summary>이동 힘을 적용할 물리 본체의 누락된 참조를 연결한다.</summary>
    private void Awake()
    {
        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
        _footCollider = transform.Find("Foot")?.GetComponent<BoxCollider2D>();
        _steeringTargeting = GetComponent<EnemyTargeting>();
        _avoidanceRefreshIn = Mathf.Abs(GetInstanceID() % 101) * 0.001f;
        _slowHealth = GetComponent<EnemyHealth>();
        _slowNetwork = GetComponent<EnemyNetworkController>();
    }

    internal int GetNavMeshAgentTypeId(Scene scene)
    {
        if (navMeshAgentTypeId != 0)
        {
            foreach (var surface in NavMeshSurface.activeSurfaces)
                if (surface.gameObject.scene == scene && surface.agentTypeID == navMeshAgentTypeId)
                    return navMeshAgentTypeId;
        }
        return 0;
    }

    // 외부 호출과 상태 변경

    /// <summary>
    /// 타깃 변경 시 이전 경로를 비우고 새 경로를 계산한다.
    /// </summary>
    public void SetDestination(Transform target)
    {
        _path = null;
        _currentPath = 0;
        TryUpdatePath(target);
    }

    /// <summary>계산에 성공한 경우에만 기존 경로를 교체한다.</summary>
    public bool TryUpdatePath(Transform target)
    {
        if (target == null)
            return false;

        if (_routeVariant < 0)
        {
            var identity = GetComponent<TeamHJD.Game.Presentation.Scene.SceneEntityIdentity>();
            string id = identity != null ? identity.EntityId.Value : null;
            uint hash = (uint)GetInstanceID();
            if (!string.IsNullOrEmpty(id))
            {
                hash = 2166136261;
                foreach (char character in id) hash = unchecked((hash ^ character) * 16777619);
            }
            _routeVariant = (int)(hash % 8);
            _cornerOffsetSeed = hash;
        }

        if (!EnemyNavMeshPathfinder.TryCalculatePath(
                transform.position, target.position, GetNavMeshAgentTypeId(gameObject.scene), out Vector3[] newPath,
                _routeVariant, gameObject.scene.handle))
        {
            return false;
        }

        _path = AddFacilityApproach(target, SpreadPathCorners(newPath));
        _currentPath = 0;
        return true;
    }

    private Vector3[] SpreadPathCorners(Vector3[] path)
    {
        int type = GetNavMeshAgentTypeId(gameObject.scene);
        float maximum = Mathf.Min(pathCornerOffset, UnityEngine.AI.NavMesh.GetSettingsByID(type).agentRadius * 0.75f);
        if (path.Length < 3 || maximum <= 0f)
            return path;

        var query = new UnityEngine.AI.NavMeshQueryFilter
        {
            agentTypeID = type,
            areaMask = UnityEngine.AI.NavMesh.AllAreas
        };
        Vector3[] spread = null;
        for (int i = 1; i < path.Length - 1; i++)
        {
            uint hash = _cornerOffsetSeed ^
                unchecked((uint)Mathf.RoundToInt(path[i].x * 100f) * 73856093u) ^
                unchecked((uint)Mathf.RoundToInt(path[i].y * 100f) * 19349663u);
            hash ^= hash << 13;
            hash ^= hash >> 17;
            hash ^= hash << 5;
            float angle = (hash & 65535u) / 65536f * Mathf.PI * 2f;
            float distance = maximum * (0.4f + ((hash >> 16) & 65535u) / 65535f * 0.6f);
            Vector3 previous = spread != null ? spread[i - 1] : path[i - 1];
            for (int attempt = 0; attempt < 4; attempt++)
            {
                float turn = angle + attempt * Mathf.PI * 0.5f;
                Vector3 probe = path[i] + new Vector3(Mathf.Cos(turn), Mathf.Sin(turn), 0f) * distance;
                if (!UnityEngine.AI.NavMesh.SamplePosition(probe, out var point, 0.1f, query) ||
                    Vector2.Distance(point.position, path[i]) < 0.1f ||
                    UnityEngine.AI.NavMesh.Raycast(previous, point.position, out _, query) ||
                    UnityEngine.AI.NavMesh.Raycast(point.position, path[i + 1], out _, query))
                    continue;

                spread ??= (Vector3[])path.Clone();
                spread[i] = point.position;
                break;
            }
        }
        return spread ?? path;
    }
    private Vector3[] AddFacilityApproach(Transform target, Vector3[] path, Vector2? contactPoint = null)
    {
        if (_footCollider == null || path.Length == 0 ||
            !target.TryGetComponent(out TargetableComponent targetable) ||
            targetable.TargetType != TargetType.Turret)
        {
            return path;
        }

        Vector2 start = path[path.Length - 1];
        Vector2 end = contactPoint ?? (Vector2)target.position;
        Vector2 offset = (Vector2)_footCollider.transform.TransformPoint(_footCollider.offset) -
            (Vector2)transform.position;
        Vector3 scale = _footCollider.transform.lossyScale;
        Vector2 size = Vector2.Scale(_footCollider.size, new Vector2(Mathf.Abs(scale.x), Mathf.Abs(scale.y)));
        Vector2 direction = end - start;
        if (direction.sqrMagnitude <= arrivalDistance * arrivalDistance)
            return path;

        var filter = new ContactFilter2D { useTriggers = false };
        int layers = Physics2D.GetLayerCollisionMask(_footCollider.gameObject.layer) | _footCollider.includeLayers;
        filter.SetLayerMask(layers & ~_footCollider.excludeLayers);
        gameObject.scene.GetPhysicsScene2D().BoxCast(start + offset, size, _footCollider.transform.eulerAngles.z,
            direction.normalized, direction.magnitude, filter, _movementHits);
        foreach (RaycastHit2D hit in _movementHits)
        {
            if (hit.rigidbody == rigidbody2D || hit.collider.transform.IsChildOf(target))
                continue;
            if (hit.rigidbody == null || hit.rigidbody.bodyType != RigidbodyType2D.Dynamic)
                return path;
        }

        // 마지막 접근에서는 목적 시설과의 물리 접촉과 기존 공격 사거리 판정에 맡긴다.
        var completedPath = new Vector3[path.Length + 1];
        System.Array.Copy(path, completedPath, path.Length);
        completedPath[path.Length] = new Vector3(end.x, end.y, 0f);
        return completedPath;
    }

    /// <summary>
    /// 도착한 경유점을 건너뛰고 다음 경유점으로 향하는 정규화된 방향을 반환한다.
    /// </summary>
    public bool TryGetNextPathDirection(out Vector2 direction)
    {
        using var profile = DirectionMarker.Auto();
        direction = Vector2.zero;

        if (_path == null)
            return false;

        Vector2 position = transform.position;
        while (_currentPath < _path.Length && arrivalDistance >= 0f)
        {
            float tolerance = _currentPath < _path.Length - 1
                ? Mathf.Min(arrivalDistance, 0.1f)
                : arrivalDistance;
            if (((Vector2)_path[_currentPath] - position).sqrMagnitude > tolerance * tolerance &&
                !CanSkipPathCorner(position))
                break;

            _currentPath++;
        }

        if (_currentPath >= _path.Length)
            return false;

        direction = ((Vector2)_path[_currentPath] - position).normalized;
        return true;
    }

    private bool CanSkipPathCorner(Vector2 position)
    {
        if (_currentPath >= _path.Length - 1 || _footCollider == null)
            return false;

        Vector2 offset = position - (Vector2)_path[_currentPath];
        if (_currentPath == 0 && offset.sqrMagnitude > 9f)
            return false;


        if (_shortcutPath != _path || _shortcutCorner != _currentPath)
        {
            _shortcutPath = _path;
            _shortcutCorner = _currentPath;
            _shortcutRefreshIn = 0f;
        }
        _shortcutRefreshIn -= Time.fixedDeltaTime;
        if (_shortcutRefreshIn > 0f)
            return false;
        _shortcutRefreshIn = 0.2f;

        Vector2 next = (Vector2)_path[_currentPath + 1] - position;
        var filter = new ContactFilter2D { useTriggers = false };
        int layers = Physics2D.GetLayerCollisionMask(_footCollider.gameObject.layer) | _footCollider.includeLayers;
        filter.SetLayerMask(layers & ~_footCollider.excludeLayers);
        _footCollider.Cast(next.normalized, filter, _movementHits, next.magnitude);
        foreach (RaycastHit2D hit in _movementHits)
        {
            if (hit.rigidbody == rigidbody2D ||
                (hit.rigidbody != null && hit.rigidbody.bodyType == RigidbodyType2D.Dynamic))
                continue;
            return false;
        }

        foreach (var surface in NavMeshSurface.activeSurfaces)
        {
            if (surface.gameObject.scene != gameObject.scene)
                continue;
            var query = new UnityEngine.AI.NavMeshQueryFilter
            {
                agentTypeID = GetNavMeshAgentTypeId(gameObject.scene),
                areaMask = UnityEngine.AI.NavMesh.AllAreas
            };
            return UnityEngine.AI.NavMesh.SamplePosition(position, out var origin, 3f, query) &&
                !UnityEngine.AI.NavMesh.Raycast(origin.position, _path[_currentPath + 1], out _, query);
        }

        return true;
    }

    /// <summary>
    /// 목표 속도를 향해 가속하고, 방향 전환과 초과 속도를 보정한다.
    /// </summary>
    /// <param name="moveDirection">정규화된 이동 방향.</param>
    public void Move(Vector2 moveDirection)
    {
        using var profile = MoveMarker.Auto();
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            Brake();
            return;
        }

        moveDirection = AvoidBlockedMovement(moveDirection);
        Vector2 currentVelocity = LimitPushSpeed();
        float deltaTime = Time.fixedDeltaTime;

        // 새 이동 방향의 속도와 옆으로 남은 속도를 분리한다.
        float forwardSpeed = Vector2.Dot(currentVelocity, moveDirection);
        Vector2 sidewaysVelocity =
            currentVelocity - moveDirection * forwardSpeed;

        // 역방향 속도는 먼저 제동하고, 이후 목표 속도까지 가속한다.
        float effectiveSpeed = MoveSpeed;
        float targetForwardSpeed = forwardSpeed < 0f ? 0f : effectiveSpeed;
        float forwardAcceleration = forwardSpeed < 0f || forwardSpeed > effectiveSpeed
            ? moveDeceleration
            : moveAcceleration;

        float nextForwardSpeed = Mathf.MoveTowards(
            forwardSpeed,
            targetForwardSpeed,
            forwardAcceleration * deltaTime);

        // 90도 전환도 감속도에 따라 이전 방향의 미끄러짐을 줄인다.
        Vector2 nextSidewaysVelocity = Vector2.MoveTowards(
            sidewaysVelocity,
            Vector2.zero,
            moveDeceleration * deltaTime);

        Vector2 nextVelocity =
            moveDirection * nextForwardSpeed + nextSidewaysVelocity;

        ApplyVelocityChange(currentVelocity, nextVelocity);
    }

    /// <summary>
    /// 정지 상태에서는 현재 속도를 감속도로 줄인다.
    /// 외부 힘에 의한 이동도 즉시 차단하지 않고 점진적으로 제동한다.
    /// </summary>
    public void Brake()
    {
        using var profile = BrakeMarker.Auto();
        _avoidanceRefreshIn = 0f;
        _progressPath = null;
        Vector2 currentVelocity = LimitPushSpeed();

        if (moveDeceleration <= 0f)
            return;

        Vector2 nextVelocity = Vector2.MoveTowards(
            currentVelocity,
            Vector2.zero,
            moveDeceleration * Time.fixedDeltaTime);

        ApplyVelocityChange(currentVelocity, nextVelocity);
    }

    // 내부 처리

    /// <summary>외부 충돌로 과속한 경우 허용 속도까지만 줄이고 밀린 방향은 유지한다.</summary>
    private Vector2 LimitPushSpeed()
    {
        Vector2 velocity = rigidbody2D.linearVelocity;
        float limit = PushSpeedLimit;

        if (velocity.sqrMagnitude <= limit * limit ||
            !rigidbody2D.simulated ||
            rigidbody2D.bodyType != RigidbodyType2D.Dynamic ||
            (rigidbody2D.constraints & RigidbodyConstraints2D.FreezePosition)
            == RigidbodyConstraints2D.FreezePosition)
        {
            return velocity;
        }

        velocity = Vector2.ClampMagnitude(velocity, limit);
        rigidbody2D.linearVelocity = velocity;
        return velocity;
    }

    /// <summary>이번 물리 단계의 목표 속도에 필요한 힘을 적용한다.</summary>
    private void ApplyVelocityChange(
        Vector2 currentVelocity,
        Vector2 nextVelocity)
    {
        float deltaTime = Time.fixedDeltaTime;

        if (deltaTime <= 0f ||
            !rigidbody2D.simulated ||
            rigidbody2D.bodyType != RigidbodyType2D.Dynamic)
        {
            return;
        }

        // 변태를 마친 소환사처럼 위치가 고정된 상태에는 힘을 주지 않는다.
        if ((rigidbody2D.constraints & RigidbodyConstraints2D.FreezePosition)
            == RigidbodyConstraints2D.FreezePosition)
        {
            return;
        }

        // 방향 전환 시 전진·옆 속도를 합친 결과도 허용 속도를 넘지 않게 한다.
        nextVelocity = Vector2.ClampMagnitude(nextVelocity, PushSpeedLimit);

        // 물리 단계에서 적용될 감쇠를 보상해 목표 속도와 가감속을 유지한다.
        float dampingFactor =
            1f + rigidbody2D.linearDamping * deltaTime;

        Vector2 velocityChange =
            nextVelocity * dampingFactor - currentVelocity;

        rigidbody2D.AddForce(
            velocityChange * (rigidbody2D.mass / deltaTime),
            ForceMode2D.Force);
    }

    // 에디터 표시

    /// <summary>
    /// 저장된 경로의 경유점과 연결선을 현재 표현 깊이에서 표시한다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (_path == null)
            return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < _path.Length; i++)
        {
            Vector3 point = _path[i];
            point.z = transform.position.z;
            Gizmos.DrawSphere(point, 0.15f);

            if (i > 0)
            {
                Vector3 previous = _path[i - 1];
                previous.z = transform.position.z;
                Gizmos.DrawLine(previous, point);
            }
        }
    }
}
