// XY NavMesh에서 완전한 이동 경로를 구합니다.
// 성공은 반환값과 경로로 전달하고, 실패 원인만 Console에 남깁니다.
using System.Collections.Generic;
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
    private const float FacilityRouteCellSize = 12f;
    private const float MinimumSharedRouteDistance = 16f;
    private const int RouteCacheLimit = 512;
    private static readonly Dictionary<(int Agent, int Scene, int X, int Y, Vector3 Goal, int Variant), (Vector3[] Corners, double Expires)> Routes = new();
    private static readonly Dictionary<(int Agent, int Scene, int X, int Y, Vector3 Goal, int Lane), (Vector3[] Corners, double Expires)> FacilityRoutes = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRouteCaches()
    {
        Routes.Clear();
        FacilityRoutes.Clear();
    }

    /// <summary>
    /// 표현용 Z를 제외한 XY 위치에서 완전한 NavMesh 경로를 구한다.
    /// </summary>
    public static bool TryCalculatePath(
        Vector3 start, Vector3 target, int agentTypeId, out Vector3[] corners, int variant = 0, int scene = 0)
        => TryCalculatePath(start, target, agentTypeId, variant, scene, false, out corners, out _);

    internal static bool TryCalculatePath(Vector3 start, Vector3 target, int agentTypeId, int variant, int scene,
        bool facilityTarget, out Vector3[] corners, out bool sharedFacilityRoute)
    {
        sharedFacilityRoute = false;
        Vector3 navStart = new(start.x, start.y, 0f);
        Vector3 navTarget = new(target.x, target.y, 0f);
        
        var filter = new NavMeshQueryFilter { agentTypeID = agentTypeId, areaMask = NavMesh.AllAreas };
        float extraClearance = Mathf.Max(0f, NavMesh.GetSettingsByID(agentTypeId).agentRadius -
            NavMesh.GetSettingsByID(0).agentRadius);
        float targetProjectionDistance = TargetProjectionDistance + extraClearance;
        float startProjectionDistance = StartProjectionDistance + NavMesh.GetSettingsByID(agentTypeId).agentRadius * 2f;
        bool hasStart = NavMesh.SamplePosition(
            navStart, out NavMeshHit startPoint, startProjectionDistance, filter);
        bool hasGoal = NavMesh.SamplePosition(
            navTarget, out NavMeshHit targetPoint, targetProjectionDistance, filter);

        if (!hasStart|| !hasGoal)
        {
            Debug.LogError($"NavMesh 위치를 찾지 못했습니다. 시작: {hasStart} {navStart} (거리 {startProjectionDistance}), " +
                $"목표: {hasGoal} {navTarget} (거리 {targetProjectionDistance})");
            corners = null;
            return false;
        }

        variant = (int)((uint)variant % 8);
        double now = Time.realtimeSinceStartupAsDouble;
        Vector3 origin = startPoint.position;
        Vector3 goal = targetPoint.position;
        bool useFacilityCache = facilityTarget &&
            (goal - origin).sqrMagnitude >= MinimumSharedRouteDistance * MinimumSharedRouteDistance;
        int lane = variant % 2;
        var facilityKey = (agentTypeId, scene, Mathf.FloorToInt(origin.x / FacilityRouteCellSize),
            Mathf.FloorToInt(origin.y / FacilityRouteCellSize), goal, lane);
        if (useFacilityCache && FacilityRoutes.TryGetValue(facilityKey, out var facilityRoute) &&
            facilityRoute.Expires > now)
        {
            if (TryJoinRoute(origin, facilityRoute.Corners, filter, out int join))
            {
                corners = CopyJoinedRoute(origin, facilityRoute.Corners, join);
                sharedFacilityRoute = true;
                return true;
            }

            // 다른 지형 편에서 접근하면 자신의 기존 경로 규칙으로 돌아간다.
            useFacilityCache = false;
        }

        var key = (agentTypeId, scene, Mathf.FloorToInt(origin.x / 4f), Mathf.FloorToInt(origin.y / 4f), goal, variant);
        if (!useFacilityCache && Routes.TryGetValue(key, out var cached) && cached.Expires > now &&
            TryReuseRoute(origin, cached.Corners, filter, out corners))
            return true;

        var path = new NavMeshPath();
        if (!NavMesh.CalculatePath(startPoint.position, targetPoint.position,
                filter, path) ||
            path.status != NavMeshPathStatus.PathComplete)
        {
            Debug.LogWarning($"완전한 경로가 없습니다. 상태: {path.status}");
            corners = null;
            return false;
        }
        
        if (useFacilityCache)
        {
            corners = CreateVariant(path.corners, filter, lane);
            if (FacilityRoutes.Count >= RouteCacheLimit) FacilityRoutes.Clear();
            FacilityRoutes[facilityKey] = ((Vector3[])corners.Clone(), now + 8d);
            sharedFacilityRoute = true;
        }
        else
        {
            corners = CreateVariant(path.corners, filter, variant);
            if (Routes.Count >= RouteCacheLimit) Routes.Clear();
            Routes[key] = ((Vector3[])corners.Clone(), now + 2d);
        }
        return true;
    }

    private static bool TryReuseRoute(Vector3 start, Vector3[] route, NavMeshQueryFilter filter, out Vector3[] corners)
    {
        corners = null;
        if (!TryJoinRoute(start, route, filter, out int join))
            return false;
        for (int i = join; i < route.Length - 1; i++)
            if (NavMesh.Raycast(route[i], route[i + 1], out _, filter)) return false;

        corners = CopyJoinedRoute(start, route, join);
        return true;
    }

    private static bool TryJoinRoute(Vector3 start, Vector3[] route, NavMeshQueryFilter filter, out int join)
    {
        join = -1;
        if (route.Length < 2)
            return false;

        if (!NavMesh.Raycast(start, route[1], out _, filter))
            join = 1;
        else if (!NavMesh.Raycast(start, route[0], out _, filter))
            join = 0;
        return join >= 0;
    }

    private static Vector3[] CopyJoinedRoute(Vector3 start, Vector3[] route, int join)
    {
        var corners = new Vector3[route.Length - join + 1];
        corners[0] = start;
        System.Array.Copy(route, join, corners, 1, route.Length - join);
        return corners;
    }

    private static Vector3[] CreateVariant(Vector3[] direct, NavMeshQueryFilter filter, int variant)
    {
        float length = PathLength(direct);
        if (variant == 0 || length < 12f) return direct;
        float remaining = length * (0.35f + variant % 3 * 0.15f);
        int segment = 1;
        while (segment < direct.Length - 1 && Vector3.Distance(direct[segment - 1], direct[segment]) < remaining)
        {
            remaining -= Vector3.Distance(direct[segment - 1], direct[segment]);
            segment++;
        }
        Vector3 heading = direct[segment] - direct[segment - 1];
        Vector3 middle = direct[segment - 1] + heading.normalized * remaining;
        float side = variant % 2 == 0 ? 1f : -1f;
        float offset = Mathf.Clamp(length * 0.12f, 3f, 18f) * (0.4f + ((variant + 1) / 2) * 0.15f);
        Vector3 probe = middle + new Vector3(-heading.y, heading.x, 0).normalized * side * offset;
        if (!NavMesh.SamplePosition(probe, out var waypoint, 3f, filter) ||
            Vector2.Distance(waypoint.position, middle) < offset * 0.5f) return direct;

        var first = new NavMeshPath();
        var second = new NavMeshPath();
        if (!NavMesh.CalculatePath(direct[0], waypoint.position, filter, first) || first.status != NavMeshPathStatus.PathComplete ||
            !NavMesh.CalculatePath(waypoint.position, direct[direct.Length - 1], filter, second) || second.status != NavMeshPathStatus.PathComplete)
            return direct;
        Vector3[] before = first.corners, after = second.corners;
        if (before.Length == 0 || after.Length == 0 || PathLength(before) + PathLength(after) > length * 1.35f + 3f)
            return direct;
        var combined = new Vector3[before.Length + after.Length - 1];
        System.Array.Copy(before, combined, before.Length);
        System.Array.Copy(after, 1, combined, before.Length, after.Length - 1);
        return combined;
    }

    private static float PathLength(Vector3[] corners)
    {
        float distance = 0;
        for (int i = 1; i < corners.Length; i++) distance += Vector3.Distance(corners[i - 1], corners[i]);
        return distance;
    }
}
