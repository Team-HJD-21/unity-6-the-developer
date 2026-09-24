using System;
using System.Collections;
using System.Collections.Generic;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

public class ControlUnitStatus : MonoBehaviour, ITurretPowerSource
{
    [Header("Attributes")]
    [SerializeField] private int maxPower;
    [SerializeField] private int currentPower;
    [SerializeField] private int maxHealth;
    [SerializeField] private int curHealth;
    
    private GameObject[] units;//현재 가동 중인 타워 배열
    List<GameObject> unitsList = new List<GameObject>();
    
    // 몬스터가 공격할 제어 장치 접근 포인트들
    [Header("Access Points")]
    public Transform[] accessPoints; 

    //  UI와의 Event 연결
    public UnityEvent<int, int, float> onCUHpChange = new UnityEvent<int, int, float>();
    public UnityEvent<int, int, float> onCUPowerChange = new UnityEvent<int, int,float>();

    private bool attackCool;

    private void Start()
    {
        attackCool = false;
        
        ValidateData();
    }

    private void Update()
    {
        if (curHealth < 0)
        {
            curHealth = 0;
        }
    }
    private void ValidateData()
    {
        curHealth = maxHealth = DataManager.GetAttributeData(AttributeType.ControlUnitHealth);
        currentPower = maxPower = DataManager.GetAttributeData(AttributeType.ControlUnitPower);
    }

    public void AddUnit(int power)
    {
        onCUPowerChange.Invoke(currentPower-power, maxPower, currentPower/(float)maxPower);
        
        currentPower = currentPower - power;
    }

    public void RemoveUnit(int power)
    {
        //  반드시 이 Method를 거쳐야 합니다. (천천히 파워가 올라감)
        RecoverPower(power);
    }

    public int GetCurrentPower()
    {
        return currentPower;
    }
    
    private void Die()
    {
        // Debug.Log("Control Unit was Destroyed!!!");
        
        GeneralManager.Instance.inGameManager.GameOver();
    }

    public void GetDamage(int damage)
    {
        //  계속 보여주지 않기 위함
        if (!attackCool)
        {
            GeneralManager.Instance.alertManager.Show(4);
            StartCoroutine(AttackCoolCoroutine());
        }
        
        onCUHpChange.Invoke(curHealth- damage, maxHealth, curHealth/(float)maxHealth);
        curHealth -= damage;
        
        if (curHealth <= 0)
        {
            curHealth = 0;
            Invoke("Die", 1f);
        }
    }

    private IEnumerator AttackCoolCoroutine()
    {
        attackCool = true;
        yield return new WaitForSeconds(10f);
        attackCool = false;
    }

    //  사용하지 않음.
    // public void GetRepair(int repair)
    // {   
    //     curHealth += repair;
    //     onCUHpChange.Invoke(curHealth, maxHealth);
    // }

    public int GetMaxHp()
    {
        return maxHealth;
    }
    public int GetCurHp()
    {
        return curHealth;
    }
    public int GetMaxPower()
    {
        return maxPower;
    }
    public int GetCurPower()
    {
        return currentPower;
    }

    public bool CheckEnoughPower(int offset)
    {
        int diff = currentPower - offset;

        return (diff >= 0 ? true : false);
    }

    //  파워 회복량, 속도
    [SerializeField] private int powerOffset = 7;
    // [SerializeField] private float recoverSpeed = 0.1f;

    private void RecoverPower(int power)
    {
        StartCoroutine(RecoverCoroutine(power));
    }

    private IEnumerator RecoverCoroutine(int power)
    {
        //  tmp는 혹시 몰라 만들어 놓는다.(실제 회복량은 다를 수 있기에 복원을 위해)
        int tmp = power;
        while (true)
        {
            if (tmp <= 0) yield break;
            
            tmp--;
            
            onCUPowerChange.Invoke(currentPower + 1, maxPower, currentPower/(float)maxPower);
            
            currentPower++;
            
            yield return new WaitForSeconds(0.1f);
            
        }
    }
}

