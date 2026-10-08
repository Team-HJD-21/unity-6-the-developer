using UnityEngine;

/// <summary>실제 경로를 차단한 방벽을 유지하거나 해제하는 규칙입니다.</summary>
public sealed class PlayerBarrierEnemyBehaviour
{
    public IPlayerFieldTarget Resolve(IPlayerFieldTarget wall) => wall != null && wall.IsAlive ? wall : null;
}
