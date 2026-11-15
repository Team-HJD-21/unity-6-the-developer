using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

public partial class PlayerInfo
{
    public const double HitInvulnerabilityDuration = 3;
    private const float HitBlinkInterval = .12f;
    private readonly List<HitCollisionExclusion> _hitCollisionExclusions = new();
    private double _hitProtectedUntil;
    private bool _hitProtectionActive, _hitProtectionBound;
    private int _hitProtectionScene;
    private Guid _hitProtectionEpoch;
    private MatchId _hitProtectionMatch;
    private SpriteRenderer _hitBody;
    private Color _hitOriginalColor, _hitAppliedColor;

    /// <summary>실제 HP 피격 후 남은 무적 시간입니다. 필드 정지 중에는 공용 시계와 함께 멈춥니다.</summary>
    public double HitInvulnerabilityRemaining => _hitProtectionActive && HasHitProtectionScope()
        ? Math.Max(0, _hitProtectedUntil - FieldSimulationTime.Seconds(this)) : 0;
    public bool IsHitInvulnerable => HitInvulnerabilityRemaining > 0;

    private IMatchTime HitProtectionClock => GetComponentInParent<PlayerMatchPresence>()?.TimeSource ??
        GetComponentInParent<FieldSimulationActor>()?.TimeSource;

    private bool HasHitProtectionScope()
    {
        if (!isActiveAndEnabled || !CanWriteHealth || IsDestroyed || gameObject.scene.handle != _hitProtectionScene ||
            GetComponent<PlayerControlState>()?.IsInSpaceship == true) return false;
        var presence = GetComponentInParent<PlayerMatchPresence>();
        var actor = GetComponentInParent<FieldSimulationActor>();
        if ((presence != null && presence.GameplayCancellation.IsCancellationRequested) ||
            (actor != null && actor.GameplayCancellation.IsCancellationRequested)) return false;
        var clock = HitProtectionClock;
        if (!_hitProtectionBound) return clock == null;
        if (clock == null || !clock.IsAvailable) return false;
        var time = clock.Snapshot;
        return time.MatchId == _hitProtectionMatch && time.Epoch == _hitProtectionEpoch;
    }

    private void StartHitProtection(double remaining = HitInvulnerabilityDuration)
    {
        ClearHitProtection();
        if (remaining <= 0 || !double.IsFinite(remaining) || !isActiveAndEnabled || IsDestroyed ||
            GetComponent<PlayerControlState>()?.IsInSpaceship == true) return;
        var clock = HitProtectionClock;
        _hitProtectionBound = clock != null;
        if (clock != null)
        {
            if (!clock.IsAvailable) return;
            _hitProtectionEpoch = clock.Snapshot.Epoch;
            _hitProtectionMatch = clock.Snapshot.MatchId;
        }
        _hitProtectionScene = gameObject.scene.handle;
        _hitProtectedUntil = FieldSimulationTime.Seconds(this) + Math.Min(HitInvulnerabilityDuration, remaining);
        _hitProtectionActive = true;

        // Collider별 제외이므로 다른 Player와 전역 레이어 행렬에는 영향을 주지 않습니다.
        // 비활성 방향의 몸도 포함해 좌우 전환 뒤에도 유지합니다. 센서/피격 Trigger는 건드리지 않습니다.
        int enemies = LayerMask.GetMask("Enemy", "EnemyFeetBox");
        foreach (var body in GetComponentsInChildren<Collider2D>(true))
        {
            if (body.isTrigger || body.GetComponentInParent<PlayerInfo>() != this) continue;
            int added = enemies & ~body.excludeLayers.value;
            if (added == 0) continue;
            _hitCollisionExclusions.Add(new HitCollisionExclusion(body, added));
            body.excludeLayers = body.excludeLayers.value | added;
        }
        _hitBody = GetComponent<SpriteRenderer>();
        if (_hitBody != null) _hitOriginalColor = _hitAppliedColor = _hitBody.color;
        RenderHitProtection();
    }

    private void AdvanceHitProtection()
    {
        if (!_hitProtectionActive) return;
        if (!IsHitInvulnerable) { ClearHitProtection(); return; }
        RenderHitProtection();
    }

    private void RenderHitProtection()
    {
        if (_hitBody == null) return;
        // 외부에서 새 tint를 지정하면 그 값을 바탕으로 곱합니다. 매 프레임 누적 곱하지 않습니다.
        if (_hitBody.color != _hitAppliedColor) _hitOriginalColor = _hitBody.color;
        double elapsed = HitInvulnerabilityDuration - HitInvulnerabilityRemaining;
        float alpha = ((int)(elapsed / HitBlinkInterval) & 1) == 0 ? 1f : .18f;
        _hitAppliedColor = _hitOriginalColor * new Color(.55f, .55f, .55f, alpha);
        _hitBody.color = _hitAppliedColor;
    }

    private void ClearHitProtection()
    {
        _hitProtectionActive = false;
        _hitProtectedUntil = 0;
        foreach (var exclusion in _hitCollisionExclusions)
            if (exclusion.Body != null) exclusion.Body.excludeLayers = exclusion.Body.excludeLayers.value & ~exclusion.Added;
        _hitCollisionExclusions.Clear();
        if (_hitBody != null && _hitBody.color == _hitAppliedColor) _hitBody.color = _hitOriginalColor;
        _hitBody = null;
    }

    private void LateUpdate() => AdvanceHitProtection();
    private void OnDisable() => ClearHitProtection();

    private readonly struct HitCollisionExclusion
    {
        public readonly Collider2D Body;
        public readonly int Added;
        public HitCollisionExclusion(Collider2D body, int added) { Body = body; Added = added; }
    }
}
