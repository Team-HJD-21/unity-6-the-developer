using UnityEngine;

/// <summary>
/// 계산한 경로를 따라 Rigidbody2D에 이동 힘을 적용한다.
/// </summary>
public partial class EnemyMovement : MonoBehaviour
{
    // Inspector 설정과 제작 참조
    [SerializeField] private float arrivalDistance = 0.4f;

    [Header("이동 설정")]
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float moveAcceleration = 10f;
    [SerializeField, Min(0f)] private float moveDeceleration = 20f;
    [SerializeField, Min(1f)] private float pushSpeedLimitMultiplier = 1.1f;

    // 실행 중 참조와 상태
    private Vector3[] _path;
    private int _currentPath = 0;

    /// <summary>스스로 이동할 때 목표로 하는 최대 속도를 반환한다.</summary>
    public float MoveSpeed => moveSpeed;

    /// <summary>외부 밀림을 포함한 물리 본체의 현재 속력을 반환한다.</summary>
    public float CurrentSpeed => rigidbody2D != null ? rigidbody2D.linearVelocity.magnitude : 0f;

    /// <summary>아직 도착하지 않은 경유점이 남아 있는지 반환한다.</summary>
    public bool HasPath => _path != null && _currentPath < _path.Length;

    /// <summary>외부 충돌로 허용할 최대 속력을 계산한다.</summary>
    private float PushSpeedLimit =>
        Mathf.Max(0f, moveSpeed) * Mathf.Max(1f, pushSpeedLimitMultiplier);

    // Unity 생명주기

    /// <summary>이동 힘을 적용할 물리 본체의 누락된 참조를 연결한다.</summary>
    private void Awake()
    {
        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
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

        if (!EnemyNavMeshPathfinder.TryCalculatePath(
                transform.position, target.position, out Vector3[] newPath))
        {
            return false;
        }

        _path = newPath;
        _currentPath = 0;
            return true;
    }

    /// <summary>
    /// 도착한 경유점을 건너뛰고 다음 경유점으로 향하는 정규화된 방향을 반환한다.
    /// </summary>
    public bool TryGetNextPathDirection(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (_path == null)
            return false;

        Vector2 position = transform.position;
        float arrivalDistanceSquared = arrivalDistance * arrivalDistance;

        // 이미 도착한 지점은 건너뛴다.
        while (_currentPath < _path.Length &&
               arrivalDistance >= 0f &&
               ((Vector2)_path[_currentPath] - position).sqrMagnitude
               <= arrivalDistanceSquared)
        {
            _currentPath++;
        }

        if (_currentPath >= _path.Length)
            return false;

        direction = ((Vector2)_path[_currentPath] - position).normalized;
        return true;
    }

    /// <summary>
    /// 목표 속도를 향해 가속하고, 방향 전환과 초과 속도를 보정한다.
    /// </summary>
    /// <param name="moveDirection">정규화된 이동 방향.</param>
    public void Move(Vector2 moveDirection)
    {
        if (moveDirection.sqrMagnitude <= 0.0001f)
        {
            Brake();
            return;
        }

        Vector2 currentVelocity = LimitPushSpeed();
        float deltaTime = Time.fixedDeltaTime;

        // 새 이동 방향의 속도와 옆으로 남은 속도를 분리한다.
        float forwardSpeed = Vector2.Dot(currentVelocity, moveDirection);
        Vector2 sidewaysVelocity =
            currentVelocity - moveDirection * forwardSpeed;

        // 역방향 속도는 먼저 제동하고, 이후 목표 속도까지 가속한다.
        float targetForwardSpeed = forwardSpeed < 0f ? 0f : moveSpeed;
        float forwardAcceleration = forwardSpeed < 0f || forwardSpeed > moveSpeed
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
