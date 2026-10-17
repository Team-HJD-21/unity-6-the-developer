using UnityEngine;
using UnityEngine.AI;

public static class EnemyNavMeshPathfinder
{
    public static bool TryCalculatePath(
        Vector3 start, Vector3 target, out Vector3[] corners)
    {
        Vector3 navStart = new(start.x, start.y, 0f);
        Vector3 navTarget = new(target.x, target.y, 0f);
        
        bool hasStart = NavMesh.SamplePosition(
            navStart, out NavMeshHit startPoint, 3f, NavMesh.AllAreas);
        bool hasGoal = NavMesh.SamplePosition(
            navTarget, out NavMeshHit targetPoint, 3f, NavMesh.AllAreas);

        if (!hasStart|| !hasGoal)
        {
            Debug.LogError($"NavMesh 위치를 찾지 못했습니다. 시작: {hasStart}, 목표: {hasGoal}");
            corners = null;
            return false;
        }

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(startPoint.position, targetPoint.position,
                NavMesh.AllAreas, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning($"완전한 경로가 없습니다. 상태: {path.status}");
            corners = null;
            return false;
        }
        
        corners = path.corners;
        Debug.Log($"NavMesh 경로 발견: {corners.Length}개 지점");
        return true;
    }
}
