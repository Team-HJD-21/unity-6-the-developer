using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// 테스트 씬에서 NavMesh 경로의 지점을 따라 슬라임을 이동시킨다.
/// </summary>
public sealed class EnemyNavMeshTestDriver : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float arrivalDistance = 0.1f;

    private Vector3[] _corners;
    private int _nextCorner;

    /// <summary>
    /// 시작점과 시험 목표를 NavMesh에 투영하고 완전한 경로일 때만 이동을 준비한다.
    /// </summary>
    private void Start()
    {
        if (target == null)
        {
            Debug.LogError("NavMesh 테스트 목표를 지정해 주세요.", this);
            return;
        }

        // 슬라임과 목표를 가장 가까운 NavMesh 위치로 맞춘다.
        bool hasStart = NavMesh.SamplePosition(
            transform.position, out NavMeshHit start, 3f, NavMesh.AllAreas);
        bool hasGoal = NavMesh.SamplePosition(
            target.position, out NavMeshHit goal, 3f, NavMesh.AllAreas);

        if (!hasStart || !hasGoal)
        {
            Debug.LogError($"NavMesh 위치를 찾지 못했습니다. 시작: {hasStart}, 목표: {hasGoal}", this);
            return;
        }

        // 완전한 경로일 때만 이동한다.
        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(start.position, goal.position, NavMesh.AllAreas, path)
            || path.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning($"완전한 경로가 없습니다. 상태: {path.status}", this);
            return;
        }

        _corners = path.corners;
        _nextCorner = 0;
        Debug.Log($"NavMesh 경로 발견: {_corners.Length}개 지점", this);
    }

    /// <summary>
    /// 시험 경로의 다음 경유점으로 이동하고 도착하면 다음 지점으로 넘어간다.
    /// </summary>
    private void Update()
    {
        if (_corners == null || _nextCorner >= _corners.Length)
            return;

        Vector3 next = _corners[_nextCorner];
        next.z = transform.position.z;

        transform.position = Vector3.MoveTowards(
            transform.position, next, moveSpeed * Time.deltaTime);

        if (Vector2.Distance(transform.position, next) <= arrivalDistance)
            _nextCorner++;
    }

    private void OnDrawGizmosSelected()
    {
        if (_corners == null)
            return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < _corners.Length; i++)
        {
            Vector3 point = _corners[i];
            point.z = transform.position.z;
            Gizmos.DrawSphere(point, 0.15f);

            if (i > 0)
            {
                Vector3 previous = _corners[i - 1];
                previous.z = transform.position.z;
                Gizmos.DrawLine(previous, point);
            }
        }
    }
}