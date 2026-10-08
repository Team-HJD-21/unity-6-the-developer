// 플레이어의 체력, 피해, 회복과 사망 통지를 관리합니다.
// 초기 체력은 Prefab 설정을 사용하며 구형 성장 Manager에는 의존하지 않습니다.
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerInfo : MonoBehaviour
{
    public int curHp;
    public int maxHp = 400;
    
    public UnityEvent onDeath = new UnityEvent();
    public UnityEvent<int,int> onHpChange = new UnityEvent<int, int>();
    private bool _isDead;

    private void Awake()
    {
        // Legacy 제거로 인해 로직 무효화: DataManager 기반 성장 체력 조회.
        // maxHp = DataManager.GetAttributeData(AttributeType.PlayerHealth);
        if (maxHp <= 0) maxHp = 400;
        curHp = maxHp;
    }

    private void FixedUpdate()
    {
        onHpChange.Invoke(curHp, maxHp);

        if (curHp <= 0)
        {
            Die();
        }
    }

    public void Die()
    {
        if (_isDead) return;
        _isDead = true;
        GetComponent<PlayerControlState>()?.SetInputBlocked(true);
        GetComponent<PlayerMovement>()?.StopImmediately();
        GetComponent<PlayerSkillController>()?.CloseSelection();
        onDeath.Invoke();
    }

    public void TakeDamage(int damage)
    {
        if (_isDead || damage <= 0) return;
        curHp -= damage;
        
        if (curHp <= 0)
        {
            Die();
        }
    }

    public void RecoverHp()
    {
        StartCoroutine(RecoverCoroutine());
    }

    private IEnumerator RecoverCoroutine()
    {
        while (true)
        {
            if (curHp >= maxHp)
            {
                curHp = maxHp;
                yield break;
            }

            curHp++;
            onHpChange.Invoke(curHp, maxHp);
            yield return new WaitForSeconds(0.02f);
        }
    }
    
}
