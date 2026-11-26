using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "DefaultLaserTurret")]
public abstract class DefaultFieldLaserTurret : FieldTurretBase, IActivateTower
{
    private readonly List<Collider2D> _targetCandidates = new();
    protected Transform Target { get; set; }
    protected float FireTime { get; set; }

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
        if (!CanSimulateField) return;

        float deltaTime = FieldSimulationTime.DeltaTime(this);
        UpdateRangeVisibility();

        if (!IsOperational || IsCoolingDown)
        {
            ClearAttack();
            RetractBeam(deltaTime);
            return;
        }

        if (IsBeamPlaying)
        {
            UpdateHeat(true, deltaTime);
            if (IsBeamPlaying)
                UpdateBeam();
            return;
        }

        UpdateHeat(false, deltaTime);
        if (IsBeamRetracting && !RetractBeam(deltaTime))
            return;

        Vector3 origin = turret.position;
        if (!CanSeeTarget(Target))
        {
            Target = null;
            TimeTilFire = 0f;
            if (!RetractBeam(deltaTime))
                return;
            Target = FindNearestVisibleTarget(_targetCandidates);
        }

        if (!FieldTurretTargetingUtility.IsInSight(TurretRotationPoint, origin, Target, TargetingAngle))
        {
            TimeTilFire = 0f;
            if (!RetractBeam(deltaTime))
                return;
        }

        if (Target != null)
            FieldTurretTargetingUtility.RotateTowards(TurretRotationPoint, origin, Target.position, RotationSpeed, deltaTime);

        bool inSight = FieldTurretTargetingUtility.IsInSight(TurretRotationPoint, origin, Target, TargetingAngle);
        if (!inSight || !CanExtendBeam)
        {
            TimeTilFire = 0f;
            return;
        }

        bool beamReady = ExtendBeam(deltaTime);
        TimeTilFire += deltaTime;
        if (beamReady && TimeTilFire >= 1f / FireRate)
        {
            Target = null;
            TimeTilFire = 0f;
            Shoot();
        }
    }

    private void UpdateHeat(bool firing, float deltaTime)
    {
        float overHeatTime = Definition.OverHeatTime;
        FireTime = Mathf.Clamp(FireTime + (firing ? deltaTime : -deltaTime), 0f, overHeatTime);
        float heatRatio = overHeatTime > 0f ? FireTime / overHeatTime : 0f;
        SetGunHeatTint(heatRatio);

        if (firing && overHeatTime > 0f && FireTime >= overHeatTime)
        {
            ClearAttack();
            BeginCooling(Definition.CoolTime);
        }
    }

    protected override void ApplyCoolingHeat(float heatRatio)
        => FireTime = heatRatio * Definition.OverHeatTime;

    protected override void OnActivationChanged(bool isActivated)
    {
        if (Animator != null)
            Animator.SetBool("isOn", isActivated);
        if (isActivated)
            return;

        ClearAttack();
        float overHeatTime = Definition.OverHeatTime;
        float heatRatio = overHeatTime > 0f ? Mathf.Clamp01(FireTime / overHeatTime) : 0f;
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
        Target = null;
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
