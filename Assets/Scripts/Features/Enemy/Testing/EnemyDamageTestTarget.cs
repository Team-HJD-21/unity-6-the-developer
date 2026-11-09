using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;

/// <summary>Enemy 테스트 씬에서 피해량과 중복 피격을 확인하는 표적이다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TargetableComponent))]
public sealed class EnemyDamageTestTarget : MonoBehaviour, IDamageableTarget
{
    [SerializeField, Min(1)] private int maxHealth = 100;

    [Header("피해 확인")]
    [SerializeField] private int currentHealth = 100;
    [SerializeField] private int hitCount;
    [SerializeField] private int lastDamage;
    [SerializeField] private int totalDamage;

    private TargetableComponent _targetable;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDestroyed => currentHealth <= 0;
    public int HitCount => hitCount;
    public int LastDamage => lastDamage;
    public int TotalDamage => totalDamage;

    private void Reset()
    {
        ResetHealth();
    }

    private void Awake()
    {
        ResetHealth();
    }

    /// <summary>
    /// 시험 피해를 체력에 반영해 적의 피해 전달 경로를 확인한다.
    /// </summary>
    public bool ApplyDamage(int damage)
    {
        if (!isActiveAndEnabled || IsDestroyed || damage <= 0)
            return false;

        hitCount++;
        lastDamage = damage;
        totalDamage += damage;
        currentHealth = Mathf.Max(0, currentHealth - damage);
        UpdateTargetState();
        return true;
    }

    /// <summary>
    /// 반복 시험을 위해 대상 체력을 초기 상태로 되돌린다.
    /// </summary>
    [ContextMenu("Reset Health And Hit Count")]
    public void ResetHealth()
    {
        maxHealth = Mathf.Max(1, maxHealth);
        currentHealth = maxHealth;
        hitCount = 0;
        lastDamage = 0;
        totalDamage = 0;
        UpdateTargetState();
    }

    private void UpdateTargetState()
    {
        if (_targetable == null)
            _targetable = GetComponent<TargetableComponent>();

        // 투사체가 IDamageableTarget 경로로 피해를 전달하도록 터렛 유형을 사용한다.
        _targetable.SetTargetType(TargetType.Turret);
        _targetable.SetHealth(currentHealth, maxHealth);
    }

    private void OnDrawGizmos()
    {
#if UNITY_EDITOR
        UnityEditor.Handles.Label(
            transform.position + Vector3.up * 0.6f,
            name + "\nHP " + currentHealth + "/" + maxHealth +
            " | Hits " + hitCount + " | Last " + lastDamage);
#endif
    }
}
