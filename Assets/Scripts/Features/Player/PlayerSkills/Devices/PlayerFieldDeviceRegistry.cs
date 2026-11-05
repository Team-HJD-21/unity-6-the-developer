// Player가 생성한 필드 장치의 등록·해제·수량을 관리합니다.
using System.Collections.Generic;
using UnityEngine;

/// <summary>활성 장비 검색과 방벽 우선 목표 선택을 담당합니다.</summary>
public static class PlayerFieldDeviceRegistry
{
    private static readonly List<IPlayerFieldTarget> _active = new();
    public static bool HasActiveDevices => _active.Exists(device => device != null && device.IsAlive);
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => _active.Clear();
    public static void Register(IPlayerFieldTarget device)
    {
        if (device == null || _active.Contains(device)) return;
        if (device is TeamHJD.Game.Contracts.IEntityIdentity identity && !identity.EntityId.IsEmpty)
            foreach (var other in _active)
                if (other != null && other.IsAlive && other is TeamHJD.Game.Contracts.IEntityIdentity existing && existing.EntityId == identity.EntityId)
                    throw new System.InvalidOperationException("Two active Player field targets share a logical ID: " + identity.EntityId.Value);
        _active.Add(device);
    }
    public static void Unregister(IPlayerFieldTarget device) => _active.Remove(device);
    public static PlayerFieldDevice Resolve(Vector2 origin, Vector2 originalGoal) => ResolveTarget(origin, originalGoal) as PlayerFieldDevice;
    public static IPlayerFieldTarget ResolveTarget(Vector2 origin, Vector2 originalGoal, Collider2D body = null)
    {
        IPlayerFieldTarget beacon = null;
        float nearest = float.PositiveInfinity;
        foreach (var device in _active)
        {
            if (device == null || !device.IsAlive || device.Kind != PlayerFieldDeviceKind.Beacon) continue;
            float distance = ((Vector2)device.Position - origin).sqrMagnitude;
            if (distance > device.Radius * device.Radius || distance >= nearest) continue;
            beacon = device;
            nearest = distance;
        }
        Vector2 goal = beacon != null ? (Vector2)beacon.Position : originalGoal;
        IPlayerFieldTarget wall = null;
        float closestFraction = float.PositiveInfinity;
        foreach (var device in _active)
        {
            if (device == null || !device.IsAlive || device.Kind == PlayerFieldDeviceKind.Beacon) continue;
            if (!device.Intersects(origin, goal, out float fraction, body) || fraction >= closestFraction) continue;
            wall = device;
            closestFraction = fraction;
        }
        return wall != null ? wall : beacon;
    }
}
