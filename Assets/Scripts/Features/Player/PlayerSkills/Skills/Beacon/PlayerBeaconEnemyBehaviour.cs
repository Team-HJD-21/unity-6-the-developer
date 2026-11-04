using UnityEngine;

/// <summary>비콘의 유인 범위를 확인하고 접근 중 가로막는 장비를 우선합니다.</summary>
public sealed class PlayerBeaconEnemyBehaviour
{
    public IPlayerFieldTarget Resolve(Vector2 position, IPlayerFieldTarget beacon, Collider2D body = null)
    {
        if (beacon == null || !beacon.IsAlive || Vector2.Distance(position, beacon.Position) > beacon.Radius) return null;
        return PlayerFieldDeviceRegistry.ResolveTarget(position, beacon.Position, body);
    }
}
