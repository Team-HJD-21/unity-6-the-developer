using NavMeshPlus.Components;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// NavMeshAgent로 적의 추적 이동을 제어한다.
/// </summary>
public partial class EnemyMovement : MonoBehaviour
{
    // Inspector 설정과 제작 참조
    [SerializeField] private int navMeshAgentTypeId;

    [Header("이동 설정")]
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float moveAcceleration = 10f;

    // 실행 중 참조와 상태
    private NavMeshAgent _agent;
    private bool _isAgentInitialized;
    private int _configuredAgentTypeId;
    private bool _physicsOwnsMovement;
    private static readonly Unity.Profiling.ProfilerMarker BrakeMarker = new("EnemyMovement.Brake");

    /// <summary>감속 효과를 적용하기 전의 설정 이동속도다.</summary>
    public float BaseMoveSpeed => Mathf.Max(0f, moveSpeed);

    /// <summary>현재 감속을 반영한 최대 이동속도다.</summary>
    public float MoveSpeed => BaseMoveSpeed * SlowSpeedMultiplier;

    internal bool UsesNavMeshAgent => GetOrEnsureNavMeshAgent() != null;

    private NavMeshAgent GetOrEnsureNavMeshAgent()
    {
        if (!_isAgentInitialized)
        {
            _isAgentInitialized = true;
            if (TryGetComponent(out _agent))
                _configuredAgentTypeId = _agent.agentTypeID;
        }

        return _agent;
    }

    private bool AgentReady =>
        _agent != null && _agent.isActiveAndEnabled && _agent.isOnNavMesh;

    /// <summary>추적에 사용할 경로가 있거나 Agent가 경로를 계산 중인지 반환한다.</summary>
    public bool HasPath => AgentReady && (_agent.pathPending ||
        (_agent.hasPath && _agent.pathStatus == NavMeshPathStatus.PathComplete));

    // Unity 생명주기

    /// <summary>이동에 필요한 참조를 연결한다.</summary>
    private void Awake()
    {
        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
        _slowHealth = GetComponent<EnemyHealth>();
        _slowNetwork = GetComponent<EnemyNetworkController>();

        if (!UsesNavMeshAgent)
            return;

        _agent.enabled = false;
        if (rigidbody2D != null)
            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        _agent.updatePosition = true;
        _agent.updateRotation = false;
        _agent.updateUpAxis = false;
        _agent.acceleration = moveAcceleration;
        // 공격 정지는 실제 대상 콜라이더까지의 거리로 결정한다.
        _agent.stoppingDistance = 0f;
    }

    internal void EnableAgentMovement()
    {
        if (!isActiveAndEnabled || !UsesNavMeshAgent || _physicsOwnsMovement || _agent.enabled)
            return;

        _agent.agentTypeID = GetNavMeshAgentTypeId(gameObject.scene);
        _agent.enabled = true;
        if (AgentReady)
            _agent.isStopped = true;
    }

    internal void DisableAgentMovement()
    {
        if (_agent != null)
            _agent.enabled = false;
    }

    internal bool CanHandMovementToPhysics(Rigidbody2D body) =>
        UsesNavMeshAgent && body != null && body == rigidbody2D && body.bodyType == RigidbodyType2D.Kinematic;

    internal void BeginPhysicsMovement(Rigidbody2D body)
    {
        if (!CanHandMovementToPhysics(body))
            return;

        _physicsOwnsMovement = true;
        DisableAgentMovement();
        body.bodyType = RigidbodyType2D.Dynamic;
    }

    internal void EndPhysicsMovement()
    {
        if (!_physicsOwnsMovement)
            return;

        _physicsOwnsMovement = false;
        if (rigidbody2D != null)
            rigidbody2D.bodyType = RigidbodyType2D.Kinematic;
        // 권위 행동 루프가 재개될 때 현재 위치에 Agent를 다시 붙인다.
    }

    internal Vector2 FollowAgentPath()
    {
        if (!AgentReady)
            return Vector2.zero;

        _agent.speed = MoveSpeed;
        if (!_agent.pathPending)
        {
            bool shouldStop = !_agent.hasPath ||
                              _agent.pathStatus != NavMeshPathStatus.PathComplete;
            if (_agent.isStopped != shouldStop)
                _agent.isStopped = shouldStop;
        }

        return _agent.isStopped ? Vector2.zero : (Vector2)_agent.velocity;
    }

    internal int GetNavMeshAgentTypeId(Scene scene)
    {
        int requestedType = navMeshAgentTypeId;
        if (requestedType == 0 && UsesNavMeshAgent)
            requestedType = _configuredAgentTypeId;

        if (requestedType != 0)
        {
            foreach (var surface in NavMeshSurface.activeSurfaces)
                if (surface.gameObject.scene == scene && surface.agentTypeID == requestedType && surface.navMeshData != null)
                    return requestedType;
        }
        return 0;
    }

    internal NavMeshQueryFilter GetNavigationFilter(Scene scene) => new()
    {
        agentTypeID = GetNavMeshAgentTypeId(scene),
        areaMask = UsesNavMeshAgent ? _agent.areaMask : NavMesh.AllAreas
    };

    internal static float GetTargetProjectionDistance(int agentTypeId)
    {
        // 시설 중심에서 주변 보행면을 찾는다. 대형 Agent의 보행면은 시설에서 더 멀다.
        const float baseDistance = 4f;
        if (agentTypeId == 0)
            return baseDistance;

        return baseDistance + Mathf.Max(0f, NavMesh.GetSettingsByID(agentTypeId).agentRadius -
            NavMesh.GetSettingsByID(0).agentRadius);
    }

    /// <summary>타깃 변경 시 이전 경로를 비우고 Agent에 새 경로를 요청한다.</summary>
    public void SetDestination(Transform target)
    {
        if (!AgentReady)
            return;

        _agent.ResetPath();
        _agent.isStopped = true;
        TryUpdatePath(target);
    }

    /// <summary>목표를 보행면에 투영해 Agent에 새 경로를 요청한다.</summary>
    public bool TryUpdatePath(Transform target)
    {
        if (target == null || !AgentReady)
            return false;

        Vector3 goal = target.position;
        goal.z = 0f;
        var filter = new NavMeshQueryFilter
        {
            agentTypeID = _agent.agentTypeID,
            areaMask = _agent.areaMask
        };
        return NavMesh.SamplePosition(goal, out NavMeshHit destination,
                   GetTargetProjectionDistance(filter.agentTypeID), filter) &&
               _agent.SetDestination(destination.position);
    }

    /// <summary>추적 이동을 멈춘다.</summary>
    public void Brake()
    {
        using var profile = BrakeMarker.Auto();
        if (AgentReady && !_agent.isStopped)
            _agent.isStopped = true;
    }
}
