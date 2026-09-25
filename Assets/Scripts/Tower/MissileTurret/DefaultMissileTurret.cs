using System.Collections;
using System.Collections.Generic;
using TeamHJD.Game.Turrets;
using UnityEngine;

public abstract class DefaultMissileTurret : TurretBase
{   
    [SerializeField] protected GameObject missilePrefab;
    
    
    protected Transform[] Targets;
    protected int OverHeatMissileCount => Definition.OverHeatMissileCount;

    private readonly List<Collider2D> _targetCandidates = new();
    private float _currentMissileCount;   //과열시 중지 위한 변수

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
        GameObject powerObject = GameObject.Find("ControlUnit");
        if (powerObject == null ||
            !powerObject.TryGetComponent(out ControlUnitStatus powerSource) ||
            !ConfigureActivation(powerSource))
        {
            Debug.LogError($"Failed to initialize turret dependencies on {name}.", this);
            enabled = false;
            return;
        }

        ShowRange = false;
    }
    private void Update()
    {
        RangeRenderer.enabled = ShowRange;
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
        if (Targets[0] == null)
        {
            CurMissileCount -= Time.deltaTime;
            FindTarget();//(raycast 사용)
        }
    }
    private void FindTarget()
    {
        TurretTargetingUtility.CollectByDistance(
            turret.position,
            Range,
            EnemyMask,
            _targetCandidates);

        for (int i = 0; i < Targets.Length; i++)
        {
            if (_targetCandidates.Count == 0)
            {
                break;
            }

            if (_targetCandidates.Count > 0)
            {
                if(!_targetCandidates[0].GetComponent<Monster>().isTargeted)
                {
                    Targets[i] = _targetCandidates[0].transform;
                    _targetCandidates[0].GetComponent<Monster>().isTargeted = true;
                    _targetCandidates.RemoveAt(0); // 할당된 타겟은 리스트에서 제거
                    if (_targetCandidates.Count != 0)
                        _targetCandidates.RemoveAt(0);
                }
            }
        }
        if (Targets.Length > 1 && Targets[1] == null) Targets[1] = Targets[0];
    }
    private void RotateTowardsTarget() //적향해 타워 z축 회전(TowerIsActivatedNow에서 수행)
    {
        if (Targets[0] == null) return;
        if (Targets[1] != null)
        {
            Vector3 targetPosition = (Targets[0].position + Targets[1].position) / 2f;
            TurretTargetingUtility.RotateTowards(
                TurretRotationPoint,
                turret.position,
                targetPosition,
                RotationSpeed);
        }
        else
        {
            TurretTargetingUtility.RotateTowards(
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
            
            TimeTilFire += Time.deltaTime;
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
        GunRenderer.color = new Color(1f,(255f-255f* (CurMissileCount / OverHeatMissileCount))/255f,(255f-255f*
            (CurMissileCount / OverHeatMissileCount))/255f);
        if (IsTargetInSight())//적이 사격 시야에 있음
        {
            if (CurMissileCount >= OverHeatMissileCount)//터렛 과열
            {
                SetTemporarilySuspended(true);
                StartCoroutine(OverHeat());
            }
        }
    }
    private bool CheckTargetIsInRange()//적이 사거리에 있는지 확인(FireRateController에서 수행)
    {
        return TurretTargetingUtility.IsInRange(turret, Targets[0], Range);
    }
    private bool IsTargetInSight()//적이 시야각에 있는지 확인(FireRateController, OverHeatAnimationController에서 수행)
    {
        return TurretTargetingUtility.IsInSight(
            TurretRotationPoint,
            turret.position,
            Targets[0],
            TargetingAngle);
        
    }
    private void FireSound()//코루틴 함수 냉각 역할 수행(OverHeatAnimationController에서 수행)
    {
        // yield return new WaitForSeconds(0.2f);
        Collider2D player = Physics2D.OverlapCircle(turret.position, 70, playerMask);
        if (player != null)
        {
            float distance = Vector2.Distance(turret.position, player.transform.position);
            AudioManager.Instance.PlaySfx(AudioManager.Sfx.MissileLaunch, distance, 70);
        }
    }
    //Coroutine Methods------------------------------------------------------------------
    private IEnumerator DeactivateProcess()
    {
        while (CurMissileCount>=0)
        {
            GunRenderer.color = new Color(1f,(255f-255f* (CurMissileCount / OverHeatMissileCount))/255f,(255f-255f*
                (CurMissileCount / OverHeatMissileCount))/255f);
            CurMissileCount -= Time.deltaTime;
            yield return null;
        }

        CurMissileCount = 0;
        GunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
    }
    private IEnumerator OverHeat()//코루틴 함수 냉각 역할 수행(OverHeatAnimationController에서 수행)
    {
        while (CurMissileCount>=0)
        {
            GunRenderer.color = new Color(1f,(255f-255f* (CurMissileCount / OverHeatMissileCount))/255f,(255f-255f*
                (CurMissileCount / OverHeatMissileCount))/255f);
            CurMissileCount -= Time.deltaTime;
            yield return null;
        }
        Animator.SetBool("isShoot", false);
        GunRenderer.color = Color.white;
        CurMissileCount = 0f;
        SetTemporarilySuspended(false);
    }

    protected IEnumerator ShootAnimation()
    {
        Animator.SetBool("isShoot",true);
        yield return new WaitForSeconds(0.6f);
        Animator.SetBool("isShoot",false);
    }
    protected override void OnActivationChanged(bool isActivated)
    {
        if (isActivated)
        {
            AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOn);
            return;
        }

        Animator.SetBool("isShoot", false);
        AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOff);
        StartCoroutine(DeactivateProcess());
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
