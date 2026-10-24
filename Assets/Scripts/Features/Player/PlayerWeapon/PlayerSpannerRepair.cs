using TeamHJD.Game.Turrets;
using UnityEngine;

/// <summary>전방에서 수리 가능한 가장 가까운 일반 터렛 하나를 선택해 API로 회복합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerSpannerRepair : MonoBehaviour
{
    [SerializeField, Min(1)] private int _repairAmount = 10;
    [SerializeField, Min(.1f)] private float _repairRange = 3f;
    [SerializeField, Range(1f, 180f)] private float _repairArc = 100f;

    public bool TryRepair(Transform owner, Vector2 direction)
        => TryRepair(owner, direction, out _);

    public bool CanRepair(Transform owner, Vector2 direction) => FindTarget(owner, direction) != null;

    public bool TryRepair(Transform owner, Vector2 direction, out Vector3 position)
    {
        position = default;
        TurretBase turret = FindTarget(owner, direction);
        if (turret == null || !turret.Repair(_repairAmount)) return false;
        position = turret.transform.position;
        return true;
    }

    private TurretBase FindTarget(Transform owner, Vector2 direction)
    {
        if (!isActiveAndEnabled || owner == null || _repairAmount <= 0 ||
            !PlayerAimUtility.IsValidDirection(direction)) return null;
        TurretBase nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (TurretBase turret in TurretInstanceRegistry.RegisteredInstances.Values)
        {
            if (turret == null || !turret.IsInitialized || !turret.isActiveAndEnabled || turret.IsDestroyed ||
                turret.IsLocked || turret.IsManualOperationBlocked || turret.CurrentHealth <= 0 ||
                turret.CurrentHealth >= turret.MaxHealth) continue;
            Vector2 position = turret.transform.position;
            float distance = (position - (Vector2)owner.position).sqrMagnitude;
            if (distance >= nearestDistance || !PlayerSpannerTargeting.CanReach(owner, turret.transform,
                position, direction, _repairRange, _repairArc)) continue;
            nearest = turret;
            nearestDistance = distance;
        }
        return nearest;
    }
}
