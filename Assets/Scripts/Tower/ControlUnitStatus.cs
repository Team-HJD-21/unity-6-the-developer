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

    public event Action<int, int> PowerChanged;

    public int CurrentPower => currentPower;
    public int MaximumPower => maxPower;

    private bool attackCool;
    private int _pendingPowerRecovery;
    private Coroutine _powerRecoveryCoroutine;

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
        NotifyPowerChanged();
    }

    public bool TryConsumePower(int power)
    {
        if (power < 0 || currentPower < power)
        {
            return false;
        }

        SetCurrentPower(currentPower - power);
        return true;
    }

    public bool TryChangeReservation(int previousPower, int newPower)
    {
        if (previousPower < 0 || newPower < 0)
        {
            return false;
        }

        int difference = newPower - previousPower;
        if (difference > 0)
        {
            return TryConsumePower(difference);
        }

        if (difference < 0)
        {
            SetCurrentPower(currentPower - difference);
        }

        return true;
    }

    public void ReleasePower(int power)
    {
        if (power <= 0)
        {
            return;
        }

        int recoverablePower = Mathf.Max(
            0,
            maxPower - currentPower - _pendingPowerRecovery);
        int acceptedPower = Mathf.Min(power, recoverablePower);
        if (acceptedPower <= 0)
        {
            return;
        }

        _pendingPowerRecovery += acceptedPower;
        if (_powerRecoveryCoroutine == null && isActiveAndEnabled)
        {
            _powerRecoveryCoroutine = StartCoroutine(RecoverCoroutine());
        }
    }

    // Legacy entry points kept for Laser Turret until it is migrated.
    public void AddUnit(int power)
    {
        TryConsumePower(power);
    }

    public void RemoveUnit(int power)
    {
        ReleasePower(power);
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

    private void SetCurrentPower(int power)
    {
        currentPower = Mathf.Clamp(power, 0, maxPower);
        NotifyPowerChanged();
    }

    private void NotifyPowerChanged()
    {
        float ratio = maxPower <= 0
            ? 0f
            : currentPower / (float)maxPower;
        onCUPowerChange.Invoke(currentPower, maxPower, ratio);
        PowerChanged?.Invoke(currentPower, maxPower);
    }

    private IEnumerator RecoverCoroutine()
    {
        while (_pendingPowerRecovery > 0 && currentPower < maxPower)
        {
            _pendingPowerRecovery--;
            SetCurrentPower(currentPower + 1);
            yield return new WaitForSeconds(0.1f);
        }

        _pendingPowerRecovery = 0;
        _powerRecoveryCoroutine = null;
    }
}

