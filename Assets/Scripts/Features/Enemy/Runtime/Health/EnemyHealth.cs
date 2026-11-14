using System;
using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>
/// 적의 체력을 관리하고 사망 시 알린다.
/// </summary>
public partial class EnemyHealth : MonoBehaviour, IAttackableEnemy
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

    /// <summary>권한·공격 규칙을 통과해 실제 줄어든 HP입니다. 사망/Despawn보다 먼저 전달합니다.</summary>
    public event Action<EnemyHealth, int, AttackSourceType> DamageApplied;

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

    /// <summary>
    /// 체력 변경 대상에서 제외해 제거·복원 중 추가 피격과 회복을 막는다.
    /// </summary>
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
        var network = GetComponent<EnemyNetworkController>();
        if (network != null && network.IsSpawned && !network.IsServer) return false;
        if (!TeamHJD.Game.Presentation.Scene.FieldSimulationTime.CanAdvance(this)) return false;
        if (damage <= 0 || !CanBeAttackedBy(source))
            return false;

        int actual = Math.Min(damage, currentHealth);
        currentHealth -= actual;
        bool died = currentHealth == 0;
        // 피해 관측자가 재진입해도 HP0 대상은 다시 공격/회복되지 않습니다.
        if (died) _isDead = true;
        if (DamageApplied != null)
            foreach (Action<EnemyHealth, int, AttackSourceType> observer in DamageApplied.GetInvocationList())
                try { observer(this, actual, source); }
                catch (Exception error) { Debug.LogException(error, this); }
        if (died) Die();

        return true;
    }

    /// <summary>
    /// 회복 가능한 상태에서 양수 회복량을 적용하고 최대 체력을 넘지 않게 한다.
    /// </summary>
    public void Heal(int healAmount)
    {
        if (!CanBeHealed || healAmount <= 0)
            return;
        
        currentHealth = Mathf.Clamp(currentHealth + healAmount, 0, maxHealth);
    }

    /// <summary>
    /// 사망 상태를 확정하고 구독자별 예외를 분리해 사망 알림을 전달한다.
    /// </summary>
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
