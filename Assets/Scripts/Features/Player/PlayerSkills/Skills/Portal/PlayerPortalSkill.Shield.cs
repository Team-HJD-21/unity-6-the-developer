using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed partial class PlayerPortalSkill
{
    [SerializeField, Min(1)] private float _shieldHealth = 20f;
    [SerializeField, Min(.1f)] private float _shieldDuration = 2f;
    [SerializeField, Min(.1f)] private float _shieldCooldown = 15f;
    private int _arrivalShield, _shieldMaximum;
    private double _shieldUntil, _shieldReadyAt;
    public float ShieldCapacity => _shieldHealth * UpgradeMultiplier;
    public float ShieldDuration => _shieldDuration * UpgradeMultiplier;
    public float ShieldCooldown => _shieldCooldown;
    public float ShieldCooldownRemaining => (float)System.Math.Max(0, _shieldReadyAt - FieldSimulationTime.Seconds(this));
    public float ShieldRemaining => (float)System.Math.Max(0, _shieldUntil - FieldSimulationTime.Seconds(this));
    public int ShieldHealth => ShieldRemaining > 0 && isActiveAndEnabled && GetComponent<PlayerInfo>() is PlayerInfo health && !health.IsDestroyed ? _arrivalShield : 0;

    private void GrantArrivalShield()
    {
        var health = GetComponent<PlayerInfo>();
        if (health == null || health.IsDestroyed || ShieldCooldownRemaining > 0) return;
        // 이동과 별개로 관리합니다. 재이동은 보호량/시간을 갱신하거나 중첩하지 않습니다.
        _arrivalShield = _shieldMaximum = Mathf.CeilToInt(ShieldCapacity);
        double now = FieldSimulationTime.Seconds(this);
        _shieldUntil = now + ShieldDuration;
        _shieldReadyAt = now + ShieldCooldown;
    }

    /// <summary>PlayerInfo가 권한/생존/필드 시간을 검증한 뒤 피해에서 보호량을 먼저 차감합니다.</summary>
    internal int AbsorbArrivalDamage(int damage)
    {
        int absorbed = Mathf.Min(Mathf.Max(0, damage), ShieldHealth);
        _arrivalShield -= absorbed;
        return damage - absorbed;
    }

    internal void ClearArrivalShield() { _arrivalShield = _shieldMaximum = 0; _shieldUntil = 0; }
    internal void ResetArrivalShield() { ClearArrivalShield(); _shieldReadyAt = 0; }
    // 비활성화/사망으로 보호막이 사라져도 생성 쿨타임은 유지합니다.
    protected override void OnDisable() { base.OnDisable(); ClearArrivalShield(); }
}
