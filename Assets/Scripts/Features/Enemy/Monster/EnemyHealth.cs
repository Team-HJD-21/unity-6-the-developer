using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 적의 체력을 관리하고 사망 시 알린다.
/// </summary>
public class EnemyHealth : NetworkBehaviour
{
    [SerializeField] private int currentHealth;
    [SerializeField, Min(1)] private int maxHealth = 20;
    private bool _isDead;
    private bool _isActive;

    /// <summary>
    /// 체력이 0이 되었을 때 한 번 발생한다.
    /// </summary>
    public event Action<EnemyHealth> Died;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;

    /// <summary>
    /// 현재 체력을 최대값으로 초기화한다.
    /// </summary>
    public void Initialize()
    {
        currentHealth = Mathf.Max(1, maxHealth);
        _isDead = false;
        _isActive = true;
    }

    public void Deactivate()
    {
        _isActive = false;
    }

    /// <summary>
    /// 양수 피해를 적용하고 체력이 0이 되면 사망을 알린다.
    /// </summary>
    /// <param name="damage">적용할 피해량.</param>
    public void TakeDamage(int damage)
    {
        if (!_isActive || _isDead || damage <= 0) return;

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        currentHealth = 0;
        _isDead = true;
        // 구독자 하나의 오류가 다른 사망 처리를 막지 않도록 각각 호출한다.
        if (Died != null)
            foreach (Action<EnemyHealth> observer in Died.GetInvocationList())
                try
                {
                    observer(this);
                }
                catch (Exception exception)
                {
                    Debug.LogException(exception, this);
                }
    }
}