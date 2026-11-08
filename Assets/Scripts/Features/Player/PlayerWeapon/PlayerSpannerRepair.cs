// 렌치가 Host의 같은 Match 안에서 도달 가능한 실제 터렛에 수리를 적용합니다.
// 수리 수치·타격 시점은 기존 Player 무기 원본을 사용하고 HUD 관리 명령과 구분합니다.
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation.Scene;
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
        FieldTurretBase turret = FindTarget(owner, direction);
        if (turret == null || !turret.Repair(_repairAmount)) return false;
        position = turret.transform.position;
        return true;
    }

    private FieldTurretBase FindTarget(Transform owner, Vector2 direction)
    {
        if (!isActiveAndEnabled || owner == null || _repairAmount <= 0 ||
            !PlayerAimUtility.IsValidDirection(direction)) return null;
        var presence = owner.GetComponentInParent<PlayerMatchPresence>();
        if (presence != null && (!presence.CanReport || !presence.CanSimulateField)) return null;
        FieldTurretBase nearest = null;
        float nearestDistance = float.PositiveInfinity;
        foreach (FieldTurretBase turret in FieldTurretInstanceRegistry.RegisteredInstances.Values)
        {
            if (turret == null || !turret.IsInitialized || !turret.isActiveAndEnabled || turret.IsDestroyed ||
                turret.IsLocked || turret.IsManualOperationBlocked || turret.CurrentHealth <= 0 ||
                turret.CurrentHealth >= turret.MaxHealth) continue;
            if (turret.gameObject.scene != owner.gameObject.scene || !turret.HasStateAuthority) continue;
            var targetTime = turret.GetComponent<FieldSimulationActor>()?.TimeSource;
            if (!ReferenceEquals(targetTime, presence?.TimeSource)) continue;
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
