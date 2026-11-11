using UnityEngine;

/// <summary>
/// 계산한 경로를 따라 Rigidbody2D에 이동 힘을 적용한다.
/// </summary>
public partial class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float arrivalDistance = 0.4f;
    private Vector3[] _path;
    private int _currentPath = 0;

    [Header("이동 설정")]
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField, Min(0f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0f)] private float moveAcceleration = 10f;
    
    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        if(rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 기존 경로 진행을 비우고 지정한 타깃의 현재 위치까지 경로를 계산한다.
    /// </summary>
    public void SetDestination(Transform target)
    {
        _path = null;
        _currentPath = 0;
        
        if (target == null)
            return;
        
        Vector3 startLocation = this.transform.position;
        Vector3 arriveLocation = target.position;
        EnemyNavMeshPathfinder.TryCalculatePath(startLocation, arriveLocation, out _path);
    }
    
    /// <summary>
    /// 도착한 경유점을 건너뛰고 다음 경유점으로 향하는 정규화된 방향을 반환한다.
    /// </summary>
    public bool TryGetNextPathDirection(out Vector2 direction)
    {
        direction = Vector2.zero;

        if (_path == null)
            return false;

        // 이미 도착한 지점은 건너뛴다.
        while (_currentPath < _path.Length &&
               Vector2.Distance(transform.position, _path[_currentPath]) <= arrivalDistance)
        {
            _currentPath++;
        }

        if (_currentPath >= _path.Length)
            return false;

        direction = ((Vector2)_path[_currentPath] -
                     (Vector2)transform.position).normalized;
        return true;
    }

    /// <summary>
    /// 이동 방향의 속도가 목표보다 느릴 때 이동 힘을 더한다.
    /// </summary>
    public void Move(Vector2 moveDirection)
    {
        Vector2 direction = moveDirection.normalized;
        float forwardSpeed = Vector2.Dot(rigidbody2D.linearVelocity, direction);
        float acceleration = Mathf.Min(moveAcceleration,
            Mathf.Max(0f, moveSpeed - forwardSpeed) / Time.fixedDeltaTime);

        // 외부에서 밀린 속도는 잘라내지 않고, 스스로 더하는 이동 힘만 제한한다.
        rigidbody2D.AddForce(direction * acceleration * rigidbody2D.mass, ForceMode2D.Force);
    }

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
