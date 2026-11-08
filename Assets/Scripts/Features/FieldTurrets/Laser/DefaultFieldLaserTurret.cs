using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "DefaultLaserTurret")]
public abstract class DefaultFieldLaserTurret : FieldTurretBase, IActivateTower
{
    private readonly List<Collider2D> _targetCandidates = new();
    private Transform _target;
    private float _fireTime;

    public override FieldTurretKind Kind => FieldTurretKind.Laser;
    public override int ProjectilesPerShot => 0;

    protected abstract bool IsBeamPlaying { get; }
    protected abstract bool IsBeamRetracting { get; }
    protected abstract bool CanExtendBeam { get; }
    protected abstract void Shoot();
    protected abstract void UpdateBeam();
    protected abstract bool ExtendBeam(float deltaTime);
    protected abstract bool RetractBeam(float deltaTime);
    protected abstract void StopFiring();
    protected abstract void HideBeam();

    private void Awake()
    {
        if (!ConfigureSceneActivation())
        {
            enabled = false;
            return;
        }

        HideBeam();
        if (GunRenderer != null)
            GunRenderer.color = Color.gray;
    }

    protected override void Update()
    {
        base.Update();
        UpdateRangeVisibility();

        if (!IsOperational || IsCoolingDown)
        {
            ClearAttack();
            RetractBeam(Time.deltaTime);
            return;
        }

        if (IsBeamPlaying)
        {
            UpdateHeat(true);
            if (IsBeamPlaying)
                UpdateBeam();
            return;
        }

        UpdateHeat(false);
        if (IsBeamRetracting && !RetractBeam(Time.deltaTime))
            return;

        Vector3 origin = turret.position;
        if (!FieldTurretTargetingUtility.IsInAttackRange(turret, _target, MinimumRange, Range) ||
            !_target.gameObject.activeInHierarchy)
        {
            if (!RetractBeam(Time.deltaTime))
                return;
            FieldTurretTargetingUtility.CollectByDistance(origin, Range, EnemyMask, _targetCandidates, MinimumRange);
            _target = _targetCandidates.Count > 0 ? _targetCandidates[0].transform : null;
            TimeTilFire = 0f;
        }

        if (!FieldTurretTargetingUtility.IsInSight(TurretRotationPoint, origin, _target, TargetingAngle))
        {
            TimeTilFire = 0f;
            if (!RetractBeam(Time.deltaTime))
                return;
        }

        if (_target != null)
            FieldTurretTargetingUtility.RotateTowards(TurretRotationPoint, origin, _target.position, RotationSpeed);

        bool inSight = FieldTurretTargetingUtility.IsInSight(TurretRotationPoint, origin, _target, TargetingAngle);
        if (!inSight || !CanExtendBeam)
        {
            TimeTilFire = 0f;
            return;
        }

        bool beamReady = ExtendBeam(Time.deltaTime);
        TimeTilFire += Time.deltaTime;
        if (beamReady && TimeTilFire >= 1f / FireRate)
        {
            _target = null;
            TimeTilFire = 0f;
            Shoot();
        }
    }

    private void UpdateHeat(bool firing)
    {
        float overHeatTime = Definition.OverHeatTime;
        _fireTime = Mathf.Clamp(_fireTime + (firing ? Time.deltaTime : -Time.deltaTime), 0f, overHeatTime);
        float heatRatio = overHeatTime > 0f ? _fireTime / overHeatTime : 0f;
        SetGunHeatTint(heatRatio);

        if (firing && overHeatTime > 0f && _fireTime >= overHeatTime)
        {
            ClearAttack();
            BeginCooling(Definition.CoolTime);
        }
    }

    protected override void ApplyCoolingHeat(float heatRatio)
        => _fireTime = heatRatio * Definition.OverHeatTime;

    protected override void OnActivationChanged(bool isActivated)
    {
        if (Animator != null)
            Animator.SetBool("isOn", isActivated);
        if (isActivated)
            return;

        ClearAttack();
        float overHeatTime = Definition.OverHeatTime;
        float heatRatio = overHeatTime > 0f ? Mathf.Clamp01(_fireTime / overHeatTime) : 0f;
        BeginCooling(heatRatio * Definition.CoolTime, heatRatio);
    }

    protected override void OnDisable()
    {
        ClearAttack();
        HideBeam();
        if (Animator != null)
            Animator.SetBool("isOn", false);
        base.OnDisable();
    }

    private void ClearAttack()
    {
        StopFiring();
        _target = null;
        TimeTilFire = 0f;
    }

    public void ActivateTurret() => RequestActivation(true);
    public void DeactivateTurret() => RequestActivation(false);
    public string GetName() => DisplayName;
    public int GetLevel() => Level;
    public int GetPower() => Power;
    public int GetRpm() => RPM;
    public int GetDamage() => Damage;
}
