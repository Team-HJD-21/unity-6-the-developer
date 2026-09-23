using System.Collections;
using System.Collections.Generic;
using TeamHjd.Game.Turrets;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;

public abstract class DefaultMissileTurret : TurretBase, IActivateTower
{   
    [SerializeField] protected GameObject missilePrefab;
    
    
    protected Transform[] Targets;
    protected int OverHeatMissileCount => Definition.OverHeatMissileCount;
 
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
        if (Definition == null)
        {
            Debug.LogError($"Turret Definition is missing on {name}.", this);
            enabled = false;
            return;
        }
        OriginPower = GameObject.Find("ControlUnit");
        ControlUnitStatus = OriginPower.GetComponent<ControlUnitStatus>();//제어장치 정보 가져오기 위함
        ShowRange = false;
    }
    private void Update()
    {
        CheckToggle();//사용자에 의한 타워 가동 토글 확인
        TowerIsActivatedNow();//사용자에 의해 타워가 가동 됐다면 역할 수행
    }
    private void CheckToggle()//Checks toggle of isActivated
    {
        RangeRenderer.enabled = ShowRange;
        if (ActivationChanged)//toggle check
        {
            if (IsActivated)
            {
                AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOn);
                AddTurret();
            }
            else
            {
                
                Animator.SetBool("isShoot", false);
                AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOff);
                StartCoroutine(DeactivateProcess());
                DeleteTurret();
            }

            CommitActivationState();
        }
    }
    private void TowerIsActivatedNow()//사용자에 의해 타워가 가동 됐다면 역할 수행(Update에서 수행)
    {
        if (IsActivated)
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
        Collider2D[] hits = Physics2D.OverlapCircleAll(turret.position, Range, EnemyMask);
        if (hits.Length == 0) return;

        // 사용할 수 있는 타겟들의 리스트를 만듭니다
        List<(Collider2D collider, float distance)> availableTargets = new List<(Collider2D, float)>();
        foreach (var hit in hits)
        {
            float distance = Vector2.Distance(turret.position, hit.transform.position);
            availableTargets.Add((hit, distance));
        }
        availableTargets.Sort((a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < Targets.Length; i++)
        {
            if (availableTargets.Count == 0)
            {
                break;
            }

            if ((availableTargets.Count > 0))
            {
                if(!availableTargets[0].collider.GetComponent<Monster>().isTargeted)
                {
                    Targets[i] = availableTargets[0].collider.transform;
                    availableTargets[0].collider.GetComponent<Monster>().isTargeted = true;
                    availableTargets.RemoveAt(0); // 할당된 타겟은 리스트에서 제거
                    if (availableTargets.Count != 0)
                        availableTargets.RemoveAt(0);
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
            float angle =
                Mathf.Atan2(
                    (Targets[0].position.y + Targets[1].position.y) / 2 -
                    turret.position.y,
                    (Targets[0].position.x + Targets[1].position.x) / 2 -
                    turret.position.x) * Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(new Vector3(0f, 0f, angle));
            TurretRotationPoint.rotation = Quaternion.RotateTowards(TurretRotationPoint.rotation, targetRotation,
                RotationSpeed * Time.deltaTime);
        }
        else
        {
            float angle =
                Mathf.Atan2(Targets[0].position.y - turret.position.y,
                    Targets[0].position.x - turret.position.x) *
                Mathf.Rad2Deg - 90f;
            Quaternion targetRotation = Quaternion.Euler(new Vector3(0f, 0f, angle));
            TurretRotationPoint.rotation = Quaternion.RotateTowards(TurretRotationPoint.rotation, targetRotation,
                RotationSpeed * Time.deltaTime);
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
                SynchronizeActivationState(false);
                StartCoroutine(OverHeat());
            }
        }
    }
    private bool CheckTargetIsInRange()//적이 사거리에 있는지 확인(FireRateController에서 수행)
    {
        if (Targets[0] == null) return false;
        return Vector2.Distance(Targets[0].position, turret.position) <= Range;
    }
    private bool IsTargetInSight()//적이 시야각에 있는지 확인(FireRateController, OverHeatAnimationController에서 수행)
    {
        if(Targets[0]==null) return false;
        float angleToTarget = Mathf.Atan2(Targets[0].position.y - turret.position.y, Targets[0].position.x - turret.position.x) * Mathf.Rad2Deg - 90f;
        float turretAngle = TurretRotationPoint.eulerAngles.z;
        float angleDifference = Mathf.DeltaAngle(turretAngle, angleToTarget);
        return Mathf.Abs(angleDifference) <= TargetingAngle;
        
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
    //for Control Unit----------------------------------------------------------------------
    private void AddTurret()//ControlUnitStatus script 사용(CheckToggle에서 수행)
    {
        if (ControlUnitStatus.GetCurrentPower() >= Power)
        {
            ControlUnitStatus.AddUnit(Power);
        }
        else
        {
            SynchronizeActivationState(false);
        }
    }
    private void DeleteTurret()//ControlUnitStatus script 사용(CheckToggle에서 수행)
    {
        ControlUnitStatus.RemoveUnit(Power);
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
        SynchronizeActivationState(true);
    }

    protected IEnumerator ShootAnimation()
    {
        Animator.SetBool("isShoot",true);
        yield return new WaitForSeconds(0.6f);
        Animator.SetBool("isShoot",false);
    }
    //---------------------------------------------------------------------------
    //For UI----------------------------------
    public void ActivateTurret()
    {
        SetActivated(true);
    }
    public void DeactivateTurret()
    {
        SetActivated(false);
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
