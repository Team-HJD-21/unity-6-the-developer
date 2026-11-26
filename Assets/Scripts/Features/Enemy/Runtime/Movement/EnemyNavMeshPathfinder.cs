// XY NavMesh에서 완전한 이동 경로를 구합니다.
// 성공은 반환값과 경로로 전달하고, 실패 원인만 Console에 남깁니다.
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// XY 평면의 시작점과 목표를 NavMesh에 투영해 완전한 경로를 구한다.
/// </summary>
public static class EnemyNavMeshPathfinder
{
    private const float StartProjectionDistance = 3f;
    // 큰 시설의 중심은 본체 장애물 안에 있습니다. 목표만 주변 보행면을 조금 더 넓게 찾습니다.
    private const float TargetProjectionDistance = 4f;

    /// <summary>
    /// 표현용 Z를 제외한 XY 위치에서 완전한 NavMesh 경로를 구한다.
    /// </summary>
    public static bool TryCalculatePath(
        Vector3 start, Vector3 target, out Vector3[] corners)
    {
        Vector3 navStart = new(start.x, start.y, 0f);
        Vector3 navTarget = new(target.x, target.y, 0f);
        
        bool hasStart = NavMesh.SamplePosition(
            navStart, out NavMeshHit startPoint, StartProjectionDistance, NavMesh.AllAreas);
        bool hasGoal = NavMesh.SamplePosition(
            navTarget, out NavMeshHit targetPoint, TargetProjectionDistance, NavMesh.AllAreas);

        if (!hasStart|| !hasGoal)
        {
            Debug.LogError($"NavMesh 위치를 찾지 못했습니다. 시작: {hasStart} {navStart} (거리 {StartProjectionDistance}), " +
                $"목표: {hasGoal} {navTarget} (거리 {TargetProjectionDistance})");
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
        return true;
    }
}
