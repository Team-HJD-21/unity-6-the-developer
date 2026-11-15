using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public sealed partial class PlayerPortalSkill
{
    [SerializeField, Range(0, 1)] private float _shieldHealthFraction = .1f;
    [SerializeField, Min(.1f)] private float _shieldDuration = 2f;
    [SerializeField] private LineRenderer _shieldVisual;
    private int _shieldHealth, _shieldMaximum;
    private double _shieldUntil;
    public float ShieldHealthFraction => _shieldHealthFraction * UpgradeMultiplier;
    public float ShieldDuration => _shieldDuration * UpgradeMultiplier;
    public float ShieldRemaining => (float)System.Math.Max(0, _shieldUntil - FieldSimulationTime.Seconds(this));
    public int ShieldHealth => ShieldRemaining > 0 && isActiveAndEnabled && GetComponent<PlayerInfo>() is PlayerInfo health && !health.IsDestroyed ? _shieldHealth : 0;

    private void GrantArrivalShield()
    {
        var health = GetComponent<PlayerInfo>();
        if (health == null || health.IsDestroyed) return;
        // 새 이동은 덧셈이 아니라 현재 레벨 보호량과 시간으로 갱신합니다.
        _shieldHealth = _shieldMaximum = Mathf.CeilToInt(health.MaxHealth * ShieldHealthFraction);
        _shieldUntil = FieldSimulationTime.Seconds(this) + ShieldDuration;
        RefreshShieldVisual();
    }

    /// <summary>PlayerInfo가 권한·생존·필드 시간을 검증한 뒤 피해에서 보호량을 먼저 차감합니다.</summary>
    internal int AbsorbArrivalDamage(int damage)
    {
        int absorbed = Mathf.Min(Mathf.Max(0, damage), ShieldHealth);
        _shieldHealth -= absorbed;
        RefreshShieldVisual();
        return damage - absorbed;
    }

    internal void ClearArrivalShield()
    {
        _shieldHealth = _shieldMaximum = 0; _shieldUntil = 0;
        if (_shieldVisual != null) _shieldVisual.enabled = false;
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (GetComponent<PlayerInfo>() is PlayerInfo health && health.IsDestroyed) ClearArrivalShield();
        else RefreshShieldVisual();
    }
    private void RefreshShieldVisual()
    {
        if (_shieldVisual == null) return;
        bool active = ShieldHealth > 0;
        _shieldVisual.enabled = active;
        if (!active) return;
        // 물리 판정 없이 플레이어 실루엣을 감싸는 청록색 보호 테두리만 표시합니다.
        var body = GetComponent<SpriteRenderer>();
        Bounds bounds = body != null ? body.bounds : new Bounds(transform.position, new Vector3(1.5f, 2f, 0));
        _shieldVisual.useWorldSpace = true;
        const int segments = 40;
        _shieldVisual.positionCount = segments + 1;
        for (int i = 0; i <= segments; i++)
        {
            float angle = i * Mathf.PI * 2 / segments;
            _shieldVisual.SetPosition(i, bounds.center + new Vector3(Mathf.Cos(angle) * (bounds.extents.x + .15f), Mathf.Sin(angle) * (bounds.extents.y + .15f), 0));
        }
        float alpha = Mathf.Clamp01(ShieldRemaining / .25f) * Mathf.Lerp(.35f, .8f, (float)_shieldHealth / Mathf.Max(1, _shieldMaximum));
        _shieldVisual.startColor = _shieldVisual.endColor = new Color(.3f, .9f, 1f, alpha);
    }

    protected override void OnDisable() { base.OnDisable(); ClearArrivalShield(); }
}
