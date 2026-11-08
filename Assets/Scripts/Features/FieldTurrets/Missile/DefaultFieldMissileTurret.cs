// 기존 공격·과열 규칙을 유지하며 진행과 생성 자식을 공용 필드 시간에 연결합니다.
using System.Collections;
using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "DefaultMissileTurret")]

public abstract partial class DefaultFieldMissileTurret : FieldTurretBase
{   
    public override FieldTurretKind Kind => FieldTurretKind.Missile;
    [SerializeField] protected GameObject missilePrefab;
    
    
    protected Transform[] Targets;
    protected int OverHeatMissileCount => Definition.OverHeatMissileCount;
    protected float CoolTime => Definition.CoolTime;

    private readonly List<Collider2D> _targetCandidates = new();
    private MissileTargetReservation[] _targetReservations;
    private float _currentMissileCount;   //과열시 중지 위한 변수
    protected override bool UsesMinimumRange => true;

    private float CurMissileCount
    {
        get => _currentMissileCount;
        set => _currentMissileCount = value;
    }

    protected void RecordMissileLaunch()
    {
        _currentMissileCount += 1f;
    }
    
   
    
    //Override Methods---------------------------
    protected abstract void Shoot();
    //--------------------------------------------
    private void Awake()
    {
        if (!ConfigureSceneActivation())
        {
            Debug.LogError($"Failed to initialize turret dependencies on {name}.", this);
            enabled = false;
            return;
        }

        ShowRange = false;
    }
    protected override void Update()
    {
        base.Update();
        if (!CanSimulateField) return;
        UpdateRangeVisibility();
        TowerIsActivatedNow();//사용자에 의해 타워가 가동 됐다면 역할 수행
    }
    private void TowerIsActivatedNow()//사용자에 의해 타워가 가동 됐다면 역할 수행(Update에서 수행)
    {
        if (IsOperational)
        {
            if (TryBeginOverheatCooling()) return;
            NoTargetInRange();//적이 타워 범위에 없을 때 탐색(raycast 사용)
            RotateTowardsTarget();//적 발견시 적을 향해 타워 돌리기
            FireRateController();//총알 객체화 후 발사 동작 수행
            OverHeatAnimationController();//설정시간 도달 시 과열
        }
    }

    private void NoTargetInRange()//적이 타워 범위에 없을 때 탐색(TowerIsActivatedNow에서 수행)
    {
        if (Targets == null || Targets.Length == 0)
            return;

        bool needsNewTargets = Targets[0] == null;
        foreach (Transform target in Targets)
        {
            if (target != null &&
                (!target.gameObject.activeInHierarchy ||
                 !target.TryGetComponent(out EnemyController enemy) || !enemy.isActiveAndEnabled ||
                 !FieldTurretTargetingUtility.IsInAttackRange(turret, target, MinimumRange, Range)))
                needsNewTargets = true;
        }

        if (needsNewTargets)
        {
            ReleaseUnlaunchedTargets();
            FindTarget();//(raycast 사용)
            if (Targets[0] == null)
                CurMissileCount = Mathf.Max(0f, CurMissileCount - TeamHJD.Game.Presentation.Scene.FieldSimulationTime.DeltaTime(this)); // 발사 후 타깃 교체는 냉각으로 세지 않는다.
        }
    }
    private void FindTarget()
    {
        _targetReservations ??= new MissileTargetReservation[Targets.Length];
        FieldTurretTargetingUtility.CollectByDistance(
            turret.position,
            Range,
            EnemyMask,
            _targetCandidates,
            MinimumRange);

        int slot = 0;
        foreach (Collider2D candidate in _targetCandidates)
        {
            if (slot >= Targets.Length)
                break;
            if (candidate == null)
                continue;

            EnemyController enemy = candidate.GetComponentInParent<EnemyController>();
            if (enemy == null || !enemy.isActiveAndEnabled ||
                !FieldTurretTargetingUtility.IsInAttackRange(turret, enemy.transform, MinimumRange, Range))
                continue;

            bool alreadySelected = false;
            for (int index = 0; index < slot; index++)
                alreadySelected |= Targets[index] == enemy.transform;
            if (alreadySelected)
                continue;

            if (!MissileTargetReservation.TryAcquire(enemy.transform, out var reservation))
                continue;

            _targetReservations[slot] = reservation;
            Targets[slot++] = enemy.transform;
        }
        if (Targets.Length > 1 && Targets[1] == null && _targetReservations[0] != null)
        {
            Targets[1] = Targets[0];
            _targetReservations[1] = _targetReservations[0].Retain();
        }
    }

    private void ReleaseUnlaunchedTargets()
    {
        if (_targetReservations != null)
        {
            for (int index = 0; index < _targetReservations.Length; index++)
            {
                _targetReservations[index]?.Dispose();
                _targetReservations[index] = null;
            }
        }
        if (Targets != null)
            System.Array.Clear(Targets, 0, Targets.Length);
        TimeTilFire = 0f;
    }

    protected void LaunchMissiles(Transform[] spawnPoints)
    {
        if (IsCoolingDown) return;
        bool launched = false;
        try
        {
            for (int index = 0; index < spawnPoints.Length; index++)
            {
                MissileTargetReservation reservation = _targetReservations?[index];
                if (reservation == null || !reservation.IsValid)
                    continue;

                GameObject missileObject = Instantiate(
                    missilePrefab, spawnPoints[index].position, turretRotationPoint.rotation);
                try
                {
                    BindProjectileSimulation(missileObject);
                    missileObject.GetComponent<FieldTurretMissile>().Initialize(reservation, Damage);
                    if (!launched)
                    {
                        launched = true;
                        StartCoroutine(ShootAnimation());
                    }
                }
                catch
                {
                    Destroy(missileObject);
                    throw;
                }
            }
        }
        finally
        {
            ReleaseUnlaunchedTargets(); // 이미 발사체로 이전된 예약은 유지한다.
            if (launched)
            {
                RecordMissileLaunch(); // 미사일 개수와 관계없이 일제 발사 한 번을 누적한다.
                TryBeginOverheatCooling();
            }
        }
    }

    protected override void OnDisable()
    {
        ReleaseUnlaunchedTargets();
        base.OnDisable();
    }
    private void RotateTowardsTarget() //적향해 타워 z축 회전(TowerIsActivatedNow에서 수행)
    {
        if (Targets[0] == null) return;
        if (Targets[1] != null)
        {
            Vector3 targetPosition = (Targets[0].position + Targets[1].position) / 2f;
            FieldTurretTargetingUtility.RotateTowards(
                TurretRotationPoint,
                turret.position,
                targetPosition,
                RotationSpeed);
        }
        else
        {
            FieldTurretTargetingUtility.RotateTowards(
                TurretRotationPoint,
                turret.position,
                Targets[0].position,
                RotationSpeed);
        }
    }
    private void FireRateController()//총알 객체화 후 발사 동작 수행(TowerIsActivatedNow에서 수행)
    {
        if (!CheckTargetIsInRange())//적이 범위에 없음
        {
            TimeTilFire = 0f;
        }
        else//적이 범위에 있음
        {
            
            TimeTilFire += TeamHJD.Game.Presentation.Scene.FieldSimulationTime.DeltaTime(this);
            if (TimeTilFire >= 1f / FireRate)//적이 타워의 시야각에 있고 RPS만큼 발사
            {
                FireSound();
                Shoot();
                TimeTilFire = 0f;
            }
        }
    }
    private void OverHeatAnimationController()//설정시간 도달 시 과열(TowerIsActivatedNow에서 수행)
    {
        if (IsCoolingDown) return;
        float heatRatio = Mathf.Clamp01(CurMissileCount / Mathf.Max(1, OverHeatMissileCount));
        GunRenderer.color = new Color(1f, 1f - heatRatio, 1f - heatRatio);
        TryBeginOverheatCooling();
    }
    private bool TryBeginOverheatCooling()
    {
        if (IsCoolingDown || CurMissileCount < OverHeatMissileCount) return false;
        ReleaseUnlaunchedTargets(); // 냉각 중에는 새 적을 독점 예약하지 않는다.
        StopAllCoroutines();
        BeginCooling(CoolTime);
        return true;
    }
    private bool CheckTargetIsInRange()//적이 사거리에 있는지 확인(FireRateController에서 수행)
    {
        return FieldTurretTargetingUtility.IsInAttackRange(turret, Targets[0], MinimumRange, Range);
    }
    private void FireSound()//코루틴 함수 냉각 역할 수행(OverHeatAnimationController에서 수행)
    {
        // yield return new WaitForSeconds(0.2f);
        Collider2D player = Physics2D.OverlapCircle(turret.position, 70, playerMask);
        if (player != null)
        {
            float distance = Vector2.Distance(turret.position, player.transform.position);
            // AudioManager.Instance.PlaySfx(AudioManager.Sfx.MissileLaunch, distance, 70);
        }
    }
    protected override void ApplyCoolingHeat(float heatRatio)
        => CurMissileCount = heatRatio * Mathf.Max(0, OverHeatMissileCount);

    protected IEnumerator ShootAnimation()
    {
        Animator.SetBool("isShoot",true);
        double end = FieldSeconds + 0.6;
        while (FieldSeconds < end) yield return null;
        Animator.SetBool("isShoot",false);
    }
    protected override void OnActivationChanged(bool isActivated)
    {
        if (isActivated)
        {
            // AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOn);
            return;
        }

        ReleaseUnlaunchedTargets();
        float heatRatio = OverHeatMissileCount > 0
            ? Mathf.Clamp01(CurMissileCount / OverHeatMissileCount)
            : 0f;
        StopAllCoroutines();
        // AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOff);
        BeginCooling(heatRatio * Mathf.Max(0f, CoolTime), heatRatio);
    }

    //Getter
    public string GetName()
    {
        return DisplayName;
    }

    public int GetLevel()
    {
        return Level;
    }

    public int GetPower()
    {
        return Power;
    }
    public int GetRPM()
    {
        return RPM;
    }
    public int GetDamage()
    {
        return Damage;
    }

    //  TODO : 데미지 정보를 만들어야 함.
   
}
