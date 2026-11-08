// 적의 실제 경로와 이동·정지를 처리하며 체크포인트에서 waypoint 진행을 복구합니다.
using UnityEngine;

public partial class EnemyMovement : MonoBehaviour
{
    [SerializeField] private float arrivalDistance = 0.4f;
    private Vector3[] _path;
    private int _currentPath = 0;

    [Header("이동 설정")]
    [SerializeField] private Rigidbody2D rigidbody2D;
    [SerializeField] private float moveSpeed = 1.5f;
    private bool _isStopped = true;
    
    public float MoveSpeed => moveSpeed;

    private void Awake()
    {
        if(rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
    }

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

    public void Move(Vector2 moveDirection)
    {
        
        // 계산한 방향과 속도로 몬스터 위치를 이동시킨다.
        _isStopped = false;
        rigidbody2D.linearVelocity = moveDirection * moveSpeed;
    }

    public void Stop()
    {
        rigidbody2D.linearVelocity = Vector2.zero;
        _isStopped = true;
    }
    
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
