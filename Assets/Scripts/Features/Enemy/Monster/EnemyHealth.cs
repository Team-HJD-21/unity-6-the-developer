using System;
using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>
/// 적의 체력을 관리하고 사망 시 알린다.
/// </summary>
public class EnemyHealth : MonoBehaviour, IAttackableEnemy
{
    [SerializeField] private int currentHealth;
    [SerializeField, Min(1)] private int maxHealth = 20;
    private bool _isDead;
    private bool _isActive;
    private EnemyDamageRule _damageRule;
    public bool CanBeHealed =>
        isActiveAndEnabled &&
        _isActive &&
        !_isDead &&
        currentHealth < maxHealth;
    
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
        _damageRule = GetComponent<EnemyDamageRule>();
        currentHealth = Mathf.Max(1, maxHealth);
        _isDead = false;
        _isActive = true;
    }

    public void Deactivate()
    {
        _isActive = false;
    }

    /// <summary>
    /// 공격자 정보가 없는 기존 호출을 공통 피해 판정으로 전달한다.
    /// </summary>
    /// <param name="damage">적용할 피해량.</param>
    public void TakeDamage(int damage)
    {
        TryTakeDamage(damage, AttackSourceType.Unknown);
    }

    /// <summary>현재 상태와 공격자 종류에 따라 공격 가능 여부를 반환한다.</summary>
    public bool CanBeAttackedBy(AttackSourceType source)
    {
        if (!isActiveAndEnabled || !_isActive || _isDead)
            return false;

        return _damageRule == null || _damageRule.CanBeAttackedBy(source);
    }

    /// <summary>허용된 공격의 양수 피해를 적용하고, 사망 시 한 번 알린다.</summary>
    public bool TryTakeDamage(int damage, AttackSourceType source)
    {
        if (damage <= 0 || !CanBeAttackedBy(source))
            return false;

        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }

        return true;
    }

    public void Heal(int healAmount)
    {
        if (!CanBeHealed || healAmount <= 0)
            return;
        
        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0, maxHealth);
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
