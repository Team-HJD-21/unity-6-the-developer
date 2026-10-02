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
        // Debug.Log("Player Die");
        
        //  SceneController를 사용할 때는 반드시 Build Setting 확인
        
        GeneralManager.Instance.inGameManager.GameOver();
    }

    public void TakeDamage(int damage)
    {
        curHp -= damage;
        
        if (curHp <= 0)
        {
            Invoke("Die", 0f);
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
