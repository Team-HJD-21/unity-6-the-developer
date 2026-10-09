using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A* 경로 계산 결과를 따라 Enemy를 이동시키는 테스트용 드라이버.
/// </summary>
[DisallowMultipleComponent]
public sealed class EnemyManualTestDriver : MonoBehaviour
{
    [Header("경로 탐색 참조")]
    [SerializeField] private EnemyNavigationMap map;
    [SerializeField] private Transform target;

    [Header("테스트 이동 설정")]
    [SerializeField, Min(0.01f)] private float moveSpeed = 1.5f;
    [SerializeField, Min(0.001f)] private float arrivalDistance = 0.02f;

    private List<Vector3Int> path;
    private int nextPathIndex;

    private void Start()
    {
        if (map == null)
        {
            Debug.LogError("EnemyNavigationMap을 Inspector에서 지정해 주세요.", this);
            return;
        }

        if (target == null)
        {
            Debug.LogError("통행 가능한 셀에 둔 목표 Transform을 지정해 주세요.", this);
            return;
        }

        // Enemy와 목표의 월드 위치를 Tilemap 셀 좌표로 바꾼다.
        Vector3Int startCell = map.WorldToCell(transform.position);
        Vector3Int goalCell = map.WorldToCell(target.position);

        // A* 경로를 계산한다.
        path = AStarPathfinder.FindPath(map, startCell, goalCell);

        if (path == null || path.Count == 0)
        {
            Debug.LogWarning(
                $"경로가 없습니다. 시작 셀: {startCell}, 목표 셀: {goalCell}",
                this);
            return;
        }

        // 경로의 0번 셀은 Enemy가 이미 있는 시작 셀이므로 다음 셀부터 이동한다.
        nextPathIndex = 1;

        Debug.Log($"A* 경로 발견: {path.Count}개 셀", this);
    }

    private void Update()
    {
        FollowPath();
    }

    /// <summary>
    /// 현재 경로의 다음 셀 중심을 향해 이동한다.
    /// 도착하면 다음 셀을 이동 목표로 지정한다.
    /// </summary>
    private void FollowPath()
    {
        if (map == null || path == null || nextPathIndex >= path.Count)
            return;

        Vector3 nextPosition = map.CellToWorld(path[nextPathIndex]);

        // 2D 게임 오브젝트의 현재 Z 위치는 그대로 유지한다.
        nextPosition.z = transform.position.z;

        transform.position = Vector3.MoveTowards(
            transform.position,
            nextPosition,
            moveSpeed * Time.deltaTime);

        float distanceSquared =
            ((Vector2)transform.position - (Vector2)nextPosition).sqrMagnitude;

        if (distanceSquared <= arrivalDistance * arrivalDistance)
        {
            transform.position = nextPosition;
            nextPathIndex++;
        }
    }

    /// <summary>
    /// Enemy가 선택된 동안 계산된 경로를 Scene 뷰에 그린다.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (map == null || path == null || path.Count == 0)
            return;

        Gizmos.color = Color.yellow;

        for (int i = 0; i < path.Count; i++)
        {
            Vector3 cellCenter = map.CellToWorld(path[i]);
            cellCenter.z = transform.position.z;

            Gizmos.DrawSphere(cellCenter, 0.08f);

            if (i > 0)
            {
                Vector3 previousCenter = map.CellToWorld(path[i - 1]);
                previousCenter.z = transform.position.z;

                Gizmos.DrawLine(previousCenter, cellCenter);
            }
        }
    }
}