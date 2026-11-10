// 기존 공격·과열 규칙을 유지하며 진행과 생성 자식을 공용 필드 시간에 연결합니다.
using System.Collections.Generic;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: null, sourceAssembly: "Assembly-CSharp", sourceClassName: "DefaultCanonTurret")]

public abstract partial class DefaultFieldCanonTurret : FieldTurretBase
{   
    public override FieldTurretKind Kind => FieldTurretKind.Canon;
    [SerializeField] protected GameObject bulletPrefab;

    protected Transform Target;             //target of bullets
    protected float OverHeatTime => Definition.OverHeatTime;
    protected float CoolTime => Definition.CoolTime;

    private readonly List<Collider2D> _targetCandidates = new();
    private float _fireTime = 0f;       //과열시 중지 위한 변수
    protected override bool UsesMinimumRange => true;

    protected abstract void Shoot();//총알 객체화 후 목표로 발사(FireRateController에서 수행)
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
            NoTargetInRange();//적이 타워 범위에 없을 때 탐색(raycast 사용)
            RotateTowardsTarget();//적 발견시 적을 향해 타워 돌리기
            FireRateController();//총알 객체화 후 발사 동작 수행
            OverHeatAnimationController();//설정시간 도달 시 과열
        }
    }

    private void NoTargetInRange()//적이 타워 범위에 없을 때 탐색(TowerIsActivatedNow에서 수행)
    {
        if (!CheckTargetIsInRange())
        {
            Target = null;
            TimeTilFire = 0f;
            _fireTime -= TeamHJD.Game.Presentation.Scene.FieldSimulationTime.DeltaTime(this);
            if(_fireTime <= 0f) _fireTime = 0f;
            Animator.SetBool("isShoot", false);
            FindTarget();//(Overlap 사용)
        }
    }
    private void RotateTowardsTarget()//적향해 타워 z축 회전(TowerIsActivatedNow에서 수행)
    {
        if (Target != null)
        {
            FieldTurretTargetingUtility.RotateTowards(
                TurretRotationPoint,
                turret.position,
                Target.position,
                RotationSpeed);
        }
    }
    private void FireRateController()//총알 객체화 후 발사 동작 수행(TowerIsActivatedNow에서 수행)
    {
        if (!CheckTargetIsInRange())//적이 범위에 없음
        {
            _fireTime -= TeamHJD.Game.Presentation.Scene.FieldSimulationTime.DeltaTime(this);
            if(_fireTime <= 0f) _fireTime = 0f;
            Animator.SetBool("isShoot", false);
            Target = null;
            TimeTilFire = 0f;
        }
        else//적이 범위에 있음
        {
            TimeTilFire += TeamHJD.Game.Presentation.Scene.FieldSimulationTime.DeltaTime(this);
            if (TimeTilFire >= (1f / FireRate) && IsTargetInSight())//적이 타워의 시야각에 있고 RPS만큼 발사
            {
                FireSound();
                Shoot();
                TimeTilFire = 0f;

            }
        }
    }
    private void OverHeatAnimationController()//설정시간 도달 시 과열(TowerIsActivatedNow에서 수행)
    {
        SetGunHeatTint(OverHeatTime > 0f ? _fireTime / OverHeatTime : 0f);

        if (IsTargetInSight())//적이 사격 시야에 있음
        {
            _fireTime += TeamHJD.Game.Presentation.Scene.FieldSimulationTime.DeltaTime(this);
            if (_fireTime >= OverHeatTime)//터렛 과열
            {
                BeginCooling(CoolTime);
            }
            else
            {
                Animator.SetBool("isShoot", true); 
            }
        }
        else//적이 사격 시야에 없음
        {
            _fireTime -= TeamHJD.Game.Presentation.Scene.FieldSimulationTime.DeltaTime(this);
            if(_fireTime <= 0f) _fireTime = 0f;
            Animator.SetBool("isShoot", false);
        }
    }
    private void FindTarget()//raycast를 이용한 적 타워 반경 접근 확인 후 배열 추가(NoTargetInRange에서 적을 찾기위해 수행)
    {
        
        FieldTurretTargetingUtility.CollectByDistance(
            turret.position,
            Range,
            EnemyMask,
            _targetCandidates,
            MinimumRange);
        Target = _targetCandidates.Count == 0
            ? null
            : _targetCandidates[0].transform;
    }
    private bool CheckTargetIsInRange()//적이 사거리에 있는지 확인(FireRateController에서 수행)
    {
        return FieldTurretTargetingUtility.IsInAttackRange(turret, Target, MinimumRange, Range);
    }
    private bool IsTargetInSight()//적이 시야각에 있는지 확인(FireRateController, OverHeatAnimationController에서 수행)
    {
        return FieldTurretTargetingUtility.IsInSight(
            TurretRotationPoint,
            turret.position,
            Target,
            TargetingAngle);
    }
    protected override void ApplyCoolingHeat(float heatRatio)
        => _fireTime = heatRatio * Mathf.Max(0f, OverHeatTime);
    private void FireSound()//코루틴 함수 냉각 역할 수행(OverHeatAnimationController에서 수행)
    {
        // yield return new WaitForSeconds(0.2f);
        Collider2D player = Physics2D.OverlapCircle(turret.position, 50, playerMask);
        if(player is not null)
        {
            float distance = Vector2.Distance(turret.position, player.transform.position);
            // AudioManager.Instance.PlaySfx(AudioManager.Sfx.Fire, distance, 50);
        }
    }
    protected override void OnActivationChanged(bool isActivated)
    {
        if (isActivated)
        {
            // AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOn);
            return;
        }

        float heatRatio = OverHeatTime > 0f ? Mathf.Clamp01(_fireTime / OverHeatTime) : 0f;
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

    public int GetRpm()
    {
        return RPM;
    }

    public int GetDamage()
    {
        return Damage;
    }
    
}
