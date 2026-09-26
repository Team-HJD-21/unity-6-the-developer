using UnityEngine;

/// <summary>
/// 플레이어와 터렛을 <see cref="ITargetable"/>로 연결하는 PoC용 컴포넌트다.
/// 타깃 유형과 체력, 화력 상태를 점수 계산에 사용할 수 있는 형태로 제공한다.
/// </summary>
public class PoCTargetable : MonoBehaviour, ITargetable
{
    [Header("타깃 설정")]
    [SerializeField] private TargetType targetType;

    [Header("체력")]
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentHealth;

    [Header("화력")]
    [SerializeField] private float maxFirepower;
    [SerializeField] private float currentFirepower;

    /// <summary>
    /// 점수 계산에 사용할 타깃 종류를 반환한다.
    /// </summary>
    public TargetType TargetType => targetType;

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
    }

    /// <summary>
    /// 최대 체력을 기준으로 정규화한 현재 체력 비율을 반환한다.
    /// </summary>
    public float HealthRatio =>
        maxHealth <= 0f
            ? 0f
            : Mathf.Clamp01(currentHealth / maxHealth);

    /// <summary>
    /// 실제 상태 시스템에서 전달받은 현재 화력과 최대 화력을 저장한다.
    /// </summary>
    /// <param name="currentFirepower">현재 화력.</param>
    /// <param name="maxFirepower">최대 화력.</param>
    public void SetFirepower(float currentFirepower, float maxFirepower)
    {
        // 최대 화력을 먼저 보정한 뒤 현재 화력을 유효 범위로 제한한다.
        this.maxFirepower = Mathf.Max(0f, maxFirepower);
        this.currentFirepower = Mathf.Clamp(currentFirepower, 0f, this.maxFirepower);
    }

    /// <summary>
    /// 최대 화력을 기준으로 정규화한 현재 화력 비율을 반환한다.
    /// </summary>
    public float FirepowerRatio =>
        maxFirepower <= 0f
            ? 0f
            : Mathf.Clamp01(currentFirepower / maxFirepower);

    /// <summary>
    /// 오브젝트가 활성화되어 있고 체력이 남아 있는지 반환한다.
    /// </summary>
    public bool CanBeTargeted =>
        isActiveAndEnabled && maxHealth > 0f && currentHealth > 0f;
}
