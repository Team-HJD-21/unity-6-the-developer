using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "LaserTurret")]
public partial class FieldLaserTurret : DefaultFieldLaserTurret
{
    private const float NativeBeamWidth = 0.8f; // 원본 빔의 발광 영역은 5 PPU 기준 4픽셀.
    private const float ThinWidthRatio = 0.15f;

    [Header("Laser Settings")]
    [SerializeField] private SpriteRenderer beamRenderer;
    [SerializeField] private Transform beamMuzzle;
    [SerializeField, Min(0.01f)] private float beamWidth = 0.8f;
    [SerializeField, Min(0.01f)] private float beamDuration = 3f;
    [SerializeField, Min(0.02f)] private float damageInterval = 0.25f;
    [SerializeField, Min(0.01f)] private float beamLengthTransition = 0.2f;
    [SerializeField, Min(0.01f)] private float beamNarrowingTime = 0.1f;
    [SerializeField, Min(0f)] private float beamRestartDelay = 0.3f;

    private readonly List<Collider2D> _hits = new();
    private readonly HashSet<EnemyHealth> _damagedEnemies = new();
    private Vector3 _beamOrigin;
    private Vector2 _beamDirection;
    private float _beamLength;
    private float _beamAngle;
    private double _beamEndsAt;
    private double _nextDamageAt;
    private double _nextBeamExtensionAt;
    private bool _isBeamPlaying;
    private bool _isBeamRetracting;
    private float _visibleLengthRatio;
    private float _visibleWidthRatio = ThinWidthRatio;
    protected override bool IsBeamPlaying => _isBeamPlaying;
    protected override bool IsBeamRetracting => _isBeamRetracting;
    protected override bool CanExtendBeam => FieldSeconds >= _nextBeamExtensionAt;
    private float DamageInterval => Mathf.Max(0.02f, damageInterval);
    private float BeamDuration => Mathf.Max(0.01f, beamDuration);
    private float LengthTransition => Mathf.Max(0.01f, beamLengthTransition);
    private float NarrowingTime => Mathf.Max(0.01f, beamNarrowingTime);
    private float RestartDelay => Mathf.Max(0f, beamRestartDelay);

    public override float TheoreticalDps => Definition != null && beamRenderer != null && beamMuzzle != null
        ? EffectiveDamage * Mathf.CeilToInt(BeamDuration / DamageInterval) /
            (BeamDuration + Mathf.Max(1f / FireRate, LengthTransition) + LengthTransition + RestartDelay)
        : 0f;

    protected override void Shoot()
    {
        if (!TryGetBeamPose(out _beamOrigin, out _beamDirection, out _beamLength))
        {
            HideBeam();
            return;
        }

        _beamAngle = Vector2.SignedAngle(Vector2.up, _beamDirection);
        double now = FieldSeconds;
        _beamEndsAt = now + BeamDuration;
        _nextDamageAt = now + DamageInterval;
        _isBeamPlaying = true;
        _visibleLengthRatio = 1f;
        _visibleWidthRatio = 1f;
        if (Animator != null)
        {
            Animator.enabled = true;
            Animator.SetBool("isShoot", true);
        }
        ApplyBeamDamage();
    }

    protected override void UpdateBeam()
    {
        double now = FieldSeconds;
        if (now >= _beamEndsAt)
        {
            StopFiring();
            return;
        }

        if (now >= _nextDamageAt)
        {
            float interval = DamageInterval;
            _nextDamageAt += (Math.Floor((now - _nextDamageAt) / interval) + 1) * interval;
            ApplyBeamDamage();
        }
    }

    protected override bool ExtendBeam(float deltaTime)
    {
        if (!TryGetBeamPose(out _beamOrigin, out _beamDirection, out _beamLength))
        {
            HideBeam();
            return true;
        }

        _beamAngle = Vector2.SignedAngle(Vector2.up, _beamDirection);
        _visibleWidthRatio = ThinWidthRatio;
        _visibleLengthRatio = Mathf.MoveTowards(_visibleLengthRatio, 1f, deltaTime / LengthTransition);
        return _visibleLengthRatio >= 1f;
    }

    protected override bool RetractBeam(float deltaTime)
    {
        if (_visibleLengthRatio <= 0f)
            return true;

        _isBeamRetracting = true;
        _visibleWidthRatio = Mathf.MoveTowards(
            _visibleWidthRatio, ThinWidthRatio, deltaTime * (1f - ThinWidthRatio) / NarrowingTime);
        _visibleLengthRatio = Mathf.MoveTowards(_visibleLengthRatio, 0f, deltaTime / LengthTransition);
        if (_visibleLengthRatio > 0f)
            return false;

        HideBeam();
        _nextBeamExtensionAt = FieldSeconds + RestartDelay;
        return true;
    }

    protected override void StopFiring()
    {
        if (_isBeamPlaying && Animator != null)
            Animator.SetBool("isShoot", false);
        _isBeamPlaying = false;
        _isBeamRetracting = _visibleLengthRatio > 0f;
    }

    protected override void OnSimulationTimeRebased(double previous, double current)
    {
        if (_isBeamPlaying)
        {
            _beamEndsAt = current + (_beamEndsAt - previous);
            _nextDamageAt = current + (_nextDamageAt - previous);
        }

        _nextBeamExtensionAt = current + Math.Max(0d, _nextBeamExtensionAt - previous);
    }

    protected override void HideBeam()
    {
        _nextBeamExtensionAt = 0f;
        _isBeamRetracting = false;
        _visibleLengthRatio = 0f;
        _visibleWidthRatio = ThinWidthRatio;
        if (beamRenderer != null && beamRenderer.gameObject.activeSelf)
            beamRenderer.gameObject.SetActive(false);
    }

    protected override void LateUpdate()
    {
        base.LateUpdate();
        if (_visibleLengthRatio > 0f && beamRenderer != null)
        {
            float opacity = Mathf.Lerp(0.25f, 1f, Mathf.InverseLerp(ThinWidthRatio, 1f, _visibleWidthRatio));
            DrawBeam(_beamOrigin, _beamAngle, _beamLength * _visibleLengthRatio, beamWidth * _visibleWidthRatio, opacity);
        }
    }

    private bool TryGetBeamPose(out Vector3 origin, out Vector2 direction, out float length)
    {
        origin = beamMuzzle != null ? beamMuzzle.position : Vector3.zero;
        direction = TurretRotationPoint != null ? ((Vector2)TurretRotationPoint.up).normalized : Vector2.zero;
        length = 0f;
        if (beamRenderer == null || beamMuzzle == null || direction == Vector2.zero)
            return false;

        Vector2 offset = (Vector2)(origin - turret.position);
        float forwardOffset = Vector2.Dot(offset, direction);
        float lateralDistanceSquared = (offset - direction * forwardOffset).sqrMagnitude;
        float reachSquared = Range * Range - lateralDistanceSquared; // 포구부터 사거리 원 경계까지.
        if (reachSquared > 0f)
            length = Mathf.Sqrt(reachSquared) - forwardOffset;
        return length > 0f;
    }

    private void DrawBeam(Vector3 origin, float angle, float length, float width, float opacity)
    {
        Transform beam = beamRenderer.transform;
        beam.SetPositionAndRotation(origin, Quaternion.Euler(0f, 0f, angle));
        Transform parent = beam.parent;
        float scaleX = parent != null ? parent.TransformVector(beam.localRotation * Vector3.right).magnitude : 1f;
        float scaleY = parent != null ? parent.TransformVector(beam.localRotation * Vector3.up).magnitude : 1f;
        if (beamRenderer.sprite != null && scaleX > 0f && scaleY > 0f)
            beam.localScale = new Vector3(width / (NativeBeamWidth * scaleX), length / (beamRenderer.sprite.bounds.size.y * scaleY), 1f);
        Color color = beamRenderer.color;
        color.a = opacity;
        beamRenderer.color = color;
        if (!beam.gameObject.activeSelf)
            beam.gameObject.SetActive(true);
    }

    private void ApplyBeamDamage()
    {
        if (!IsOperational || !_isBeamPlaying || Damage <= 0)
            return;

        var filter = new ContactFilter2D { useTriggers = Physics2D.queriesHitTriggers };
        filter.SetLayerMask(EnemyMask);
        Vector2 center = (Vector2)_beamOrigin + _beamDirection * (_beamLength * 0.5f);
        Physics2D.OverlapBox(center, new Vector2(beamWidth, _beamLength), _beamAngle, filter, _hits);
        _damagedEnemies.Clear();
        int damage = Damage;
        foreach (Collider2D hit in _hits)
        {
            if (!IsOperational || !_isBeamPlaying)
                break;
            EnemyHealth health = hit != null ? hit.GetComponentInParent<EnemyHealth>() : null;
            if (health != null && _damagedEnemies.Add(health))
                health.TakeDamage(damage);
        }
        _hits.Clear();
        _damagedEnemies.Clear();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (Definition == null)
            return;

        Handles.color = Color.magenta;
        Handles.DrawWireDisc(transform.position, transform.forward, Range);
    }
#endif
}
