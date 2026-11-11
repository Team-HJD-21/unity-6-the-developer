using System;
using UnityEngine;
using TeamHJD.Game.FieldTurrets.Contracts;

/// <summary>
/// 플레이어와 터렛을 <see cref="ITargetable"/>로 연결하는 타깃 상태 컴포넌트다.
/// 타깃 유형과 체력, 화력 상태를 점수 계산에 사용할 수 있는 형태로 제공한다.
/// </summary>
public class TargetableComponent : MonoBehaviour, ITargetable
{
    [Header("타깃 설정")]
    [SerializeField] private TargetType targetType;

    [Header("공격 인원 제한")]
    [SerializeField, Min(1)] private int maxAttackers = 10;

    [Header("체력")]
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentHealth;

    [Header("화력")]
    [SerializeField, Min(0f)] private float firepowerEfficiency;

    private bool _wasTargetable;
    private int _assignedAttackerCount;

    /// <summary>타깃 가능한 상태에서 불가능한 상태로 전환되면 발생한다.</summary>
    public event Action<ITargetable> TargetabilityLost;

    private void OnEnable()
    {
        RefreshTargetability();
    }

    private void OnDisable()
    {
        RefreshTargetability();
    }

    /// <summary>타깃 불가 상태로 전환되었을 때 한 번 알린다.</summary>
    private void RefreshTargetability()
    {
        bool canBeTargeted = CanBeTargeted;
        bool lostTargetability = _wasTargetable && !canBeTargeted;

        _wasTargetable = canBeTargeted;

        if (lostTargetability)
            TargetabilityLost?.Invoke(this);
    }


    /// <summary>
    /// 점수 계산에 사용할 타깃 종류를 반환한다.
    /// </summary>
    public TargetType TargetType => targetType;

    /// <summary>이 타깃에 배정할 수 있는 최대 적 인원을 반환한다.</summary>
    public int MaxAttackers => maxAttackers;

    /// <summary>현재 이 타깃에 배정된 적 인원.</summary>
    public int AssignedAttackerCount => _assignedAttackerCount;

    /// <summary>최대 공격 인원을 넘지 않으면 요청한 인원을 배정한다.</summary>
    public bool TryAssignAttackers(int count)
    {
        if (!CanBeTargeted || count <= 0)
            return false;

        int remaining = MaxAttackers - _assignedAttackerCount;

        if (count > remaining)
            return false;

        _assignedAttackerCount += count;
        return true;
    }

    /// <summary>분대가 반환한 배정 인원을 해제한다.</summary>
    public void UnassignAttackers(int count)
    {
        if (count <= 0)
            return;

        _assignedAttackerCount =
            Mathf.Max(0, _assignedAttackerCount - count);
    }

    /// <summary>
    /// 점수 계산에 사용할 타깃 종류를 지정한다.
    /// </summary>
    /// <param name="type">설정할 타깃 종류.</param>
    public void SetTargetType(TargetType type)
    {
        targetType = type;
    }

    /// <summary>
    /// AI가 추적할 현재 오브젝트의 위치를 반환한다.
    /// </summary>
    public Transform TargetTransform => transform;

    /// <summary>
    /// 실제 상태 시스템에서 전달받은 현재 체력과 최대 체력을 저장한다.
    /// </summary>
    /// <param name="currentHealth">현재 체력.</param>
    /// <param name="maxHealth">최대 체력.</param>
    public void SetHealth(float currentHealth, float maxHealth)
    {
        // 최대 체력을 먼저 보정한 뒤 현재 체력을 유효 범위로 제한한다.
        this.maxHealth = Mathf.Max(0f, maxHealth);
        this.currentHealth = Mathf.Clamp(currentHealth, 0f, this.maxHealth);
        RefreshTargetability();
    }

    /// <summary>
    /// 최대 체력을 기준으로 정규화한 현재 체력 비율을 반환한다.
    /// </summary>
    public float HealthRatio =>
        maxHealth <= 0f
            ? 0f
            : Mathf.Clamp01(currentHealth / maxHealth);

    public void SetFirepowerEfficiency(float dpsPerPower)
    {
        firepowerEfficiency = float.IsNaN(dpsPerPower) || float.IsInfinity(dpsPerPower) ? 0f : Mathf.Max(0f, dpsPerPower);
    }

    // Compatibility name: this is raw DPS per power, not a normalized ratio.
    public float FirepowerRatio => firepowerEfficiency;
    public float FirepowerEfficiency => firepowerEfficiency;

    /// <summary>
    /// 오브젝트가 활성화되어 있고 체력이 남아 있는지 반환한다.
    /// </summary>
    public bool CanBeTargeted =>
        isActiveAndEnabled && maxHealth > 0f && currentHealth > 0f;

    /// <summary>
    /// 연결된 피해 수신자로 피해를 전달하고 수신자가 없으면 적용하지 않는다.
    /// </summary>
    public bool TryApplyDamage(int damage)
    {
        MonoBehaviour[] candidates = GetComponentsInParent<MonoBehaviour>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            if (candidates[i] is IDamageableTarget damageable)
            {
                return damageable.ApplyDamage(damage);
            }
        }

        return false;
    }
}
