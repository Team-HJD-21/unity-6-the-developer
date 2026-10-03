using System;
using System.Collections;
using System.Collections.Generic;
using TeamHJD.Game.Turrets.Contracts;
using TeamHJD.Game.Turrets;
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

    [SerializeField] private TurretController turretController;
    [SerializeField] private bool initializePowerFromLegacyData = true;
    public int CurrentPower => turretController != null ? turretController.CurrentPower : 0;
    public int MaximumPower => turretController != null ? turretController.MaximumPower : 0;

    private bool attackCool;
    private void Awake()
    {
        if (turretController == null)
            turretController = TurretController.GetOrCreateForScene(gameObject.scene);
        turretController.PowerChanged += HandlePowerChanged;
    }

    private void OnDestroy()
    {
        if (turretController != null)
            turretController.PowerChanged -= HandlePowerChanged;
    }

    private void HandlePowerChanged(int availablePower, int capacity)
    {
        // Serialized mirrors preserve old Inspector/UI integrations. The budget owns the values.
        currentPower = availablePower;
        maxPower = capacity;
        NotifyPowerChanged();
    }

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
        if (initializePowerFromLegacyData)
        {
            int capacity = DataManager.GetAttributeData(AttributeType.ControlUnitPower);
            if (turretController.TrySetMaximumPower(capacity)) return;
            Debug.LogError("CU power capacity is lower than existing turret reservations.", this);
        }
        HandlePowerChanged(CurrentPower, MaximumPower);
    }

    public bool TryConsumePower(int power)
    {
        return turretController != null && turretController.TryConsumePower(power);
    }

    public bool TryChangeReservation(int previousPower, int newPower)
    {
        return turretController != null && turretController.TryChangeReservation(previousPower, newPower);
    }

    public void ReleasePower(int power)
    {
        if (turretController != null) turretController.ReleasePower(power);
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
        return CurrentPower;
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
        return MaximumPower;
    }
    public int GetCurPower()
    {
        return CurrentPower;
    }

    private void NotifyPowerChanged()
    {
        float ratio = maxPower <= 0
            ? 0f
            : currentPower / (float)maxPower;
        onCUPowerChange.Invoke(currentPower, maxPower, ratio);
        PowerChanged?.Invoke(currentPower, maxPower);
    }

}

