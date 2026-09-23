using System;
using System.Collections;
using System.Collections.Generic;
using TeamHjd.Game.Turrets;
using Tower;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.Serialization;
public abstract class DefaultCanonTurret : TurretBase, IActivateTower
{   
    [SerializeField] protected GameObject bulletPrefab;
   
   
    protected Transform Target;             //target of bullets
    protected Transform RangeTransform;

    protected float OverHeatTime => Definition.OverHeatTime;
    protected float CoolTime => Definition.CoolTime;
    
    private float _fireTime = 0f;       //과열시 중지 위한 변수

    protected abstract void Shoot();//총알 객체화 후 목표로 발사(FireRateController에서 수행)
    private void Awake()
    {
        if (Definition == null)
        {
            Debug.LogError($"Turret Definition is missing on {name}.", this);
            enabled = false;
            return;
        }
        _originPower = GameObject.Find("ControlUnit");
        _cus = _originPower.GetComponent<ControlUnitStatus>();//제어장치 정보 가져오기 위함
        Name = "Canon Turret";
        ShowRange = false;
    }
    protected void Update()
    {
        CheckToggle();//사용자에 의한 타워 가동 토글 확인
        TowerIsActivatedNow();//사용자에 의해 타워가 가동 됐다면 역할 수행
        
    }
    private void CheckToggle()//Checks toggle of isActivated
    {
        RangeRenderer.enabled = ShowRange;
        if (isActivated != previousIsActivated)//toggle check
        {
            if (isActivated)
            {
                previousIsActivated = isActivated; // 이전 상태를 현재 상태로 업데이트
                AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOn);
                AddTurret();
            }
            else if (isActivated == false)
            {
                Animator.SetBool("isShoot", false);
                AudioManager.Instance.PlaySfx(AudioManager.Sfx.TurretOff);
                StartCoroutine(DeactivateProcess());
                previousIsActivated = isActivated; // 이전 상태를 현재 상태로 업데이트
                DeleteTurret();
            }
            
        }
    }
    private void TowerIsActivatedNow()//사용자에 의해 타워가 가동 됐다면 역할 수행(Update에서 수행)
    {
        if (isActivated)
        {
            NoTargetInRange();//적이 타워 범위에 없을 때 탐색(raycast 사용)
            RotateTowardsTarget();//적 발견시 적을 향해 타워 돌리기
            FireRateController();//총알 객체화 후 발사 동작 수행
            OverHeatAnimationController();//설정시간 도달 시 과열
        }
    }

    private void NoTargetInRange()//적이 타워 범위에 없을 때 탐색(TowerIsActivatedNow에서 수행)
    {
        if (Target == null)
        {
            _fireTime -= Time.deltaTime;
            if(_fireTime <= 0f) _fireTime = 0f;
            Animator.SetBool("isShoot", false);
            FindTarget();//(Overlap 사용)
        }
    }
    private void RotateTowardsTarget()//적향해 타워 z축 회전(TowerIsActivatedNow에서 수행)
    {
        if (Target != null) //
        {
            float angle =
                Mathf.Atan2(Target.position.y - turret.position.y, Target.position.x - turret.position.x) *
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
            _fireTime -= Time.deltaTime;
            if(_fireTime <= 0f) _fireTime = 0f;
            Animator.SetBool("isShoot", false);
            Target = null;
            _timeTilFire = 0f;
        }
        else//적이 범위에 있음
        {
            _timeTilFire += Time.deltaTime;
            if (_timeTilFire >= (1f / FireRate) && IsTargetInSight())//적이 타워의 시야각에 있고 RPS만큼 발사
            {
                FireSound();
                Shoot();
                _timeTilFire = 0f;

            }
        }
    }
    private void OverHeatAnimationController()//설정시간 도달 시 과열(TowerIsActivatedNow에서 수행)
    {
        GunRenderer.color = new Color(1f, 1-(_fireTime / OverHeatTime),1-(_fireTime / OverHeatTime));

        if (IsTargetInSight())//적이 사격 시야에 있음
        {
            _fireTime += Time.deltaTime;
            if (_fireTime >= OverHeatTime)//터렛 과열
            {
                isActivated = false;
                previousIsActivated = false;
                Animator.SetBool("isShoot", false);
                StartCoroutine(OverHeat());
            }
            else
            {
                Animator.SetBool("isShoot", true); 
            }
        }
        else//적이 사격 시야에 없음
        {
            _fireTime -= Time.deltaTime;
            if(_fireTime <= 0f) _fireTime = 0f;
            Animator.SetBool("isShoot", false);
        }
    }
    private void FindTarget()//raycast를 이용한 적 타워 반경 접근 확인 후 배열 추가(NoTargetInRange에서 적을 찾기위해 수행)
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

        foreach (var monster in availableTargets)
        {
            if (Target is null)
            {
                Target = monster.collider.transform;
            }
            else return;
        }
    }
    private bool CheckTargetIsInRange()//적이 사거리에 있는지 확인(FireRateController에서 수행)
    {
        if (Target == null) return false;
        return Vector2.Distance(Target.position, turret.position) <= Range;
    }
    private bool IsTargetInSight()//적이 시야각에 있는지 확인(FireRateController, OverHeatAnimationController에서 수행)
    {
        if (Target == null) return false;
        float angleToTarget = Mathf.Atan2(Target.position.y - turret.position.y, Target.position.x - turret.position.x) * Mathf.Rad2Deg - 90f;
        float turretAngle = TurretRotationPoint.eulerAngles.z;
        float angleDifference = Mathf.DeltaAngle(turretAngle, angleToTarget);
        return Mathf.Abs(angleDifference) <= _angleThreshold;
    }
    //Coroutine Methods--------------------------------------------------------------------------------------------------------
    private IEnumerator DeactivateProcess()
    {
        // _totCoolTime = _fireTime;
        while (_fireTime >= 0f)
        {
            GunRenderer.color = new Color(1f,1-(_fireTime / OverHeatTime),1-(_fireTime / OverHeatTime));
            _fireTime -= Time.deltaTime;
            yield return null;
        }
        GunRenderer.color = new Color(0.5f, 0.5f, 0.5f);
    }
    private IEnumerator OverHeat()//코루틴 함수 냉각 역할 수행(OverHeatAnimationController에서 수행)
    {
        _totCoolTime = CoolTime;
        while (_totCoolTime >= 0f)
        {
            GunRenderer.color = new Color(1f,1-(_totCoolTime / CoolTime),1-(_totCoolTime / CoolTime));
            _totCoolTime -= Time.deltaTime;
            yield return null;
        }
        Animator.SetBool("isShoot", false);
        //yield return new WaitForSeconds(5f);
        GunRenderer.color = Color.white;
        _fireTime = 0f;
        isActivated = true;
        previousIsActivated = true;
    }
    private void FireSound()//코루틴 함수 냉각 역할 수행(OverHeatAnimationController에서 수행)
    {
        // yield return new WaitForSeconds(0.2f);
        Collider2D player = Physics2D.OverlapCircle(turret.position, 50, playerMask);
        if(player is not null)
        {
            float distance = Vector2.Distance(turret.position, player.transform.position);
            AudioManager.Instance.PlaySfx(AudioManager.Sfx.Fire, distance, 50);
        }
    }
    //--------------------------------------------------------------------------------------------------------------------------
    //for Control Unit----------------------------------------------------------
    private void AddTurret()//ControlUnitStatus script 사용(CheckToggle에서 수행)
    {
        if (_cus.GetCurrentPower() >= Power)
        {
            _cus.AddUnit(Power);
        }
        else
        {
            isActivated = false;
            previousIsActivated = false;
        }
    }
    private void DeleteTurret()//ControlUnitStatus script 사용(CheckToggle에서 수행)
    {
        _cus.RemoveUnit(Power);
    }
    //----------------------------------------------------------------------------
    //For UI------------------------- 
    public void ActivateTurret()
    {
        isActivated = true;
    }
    public void DeactivateTurret()
    {
        isActivated = false;
    }
    
    //Getter
    public String GetName()
    {
        return Name;
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
