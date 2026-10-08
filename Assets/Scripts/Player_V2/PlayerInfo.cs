using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public class PlayerInfo : MonoBehaviour
{
    public int curHp;
    public int maxHp;
    
    public UnityEvent onDeath = new UnityEvent();
    public UnityEvent<int,int> onHpChange = new UnityEvent<int, int>();
    private bool _isDead;

    private void Awake()
    {
        maxHp = DataManager.GetAttributeData(AttributeType.PlayerHealth);
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
