using UnityEngine;

/// <summary>
/// 오브젝트의 타깃 유형과 점수 계산에 필요한 상태를 제공한다.
/// </summary>
public class PoCTargetable : MonoBehaviour, ITargetable
{
    [Header("타깃 설정")]
    [SerializeField] private TargetType targetType;

    [Header("체력")]
    [SerializeField] private float maxHealth;
    [SerializeField] private float currentHealth;

    [Header("화력")]
    [SerializeField] private float maxPower;
    [SerializeField] private float currentPower;

    public Transform TargetTransform => transform;
    public TargetType TargetType => targetType;

    public float HealthRatio =>
        maxHealth <= 0f
            ? 0f
            : Mathf.Clamp01(currentHealth / maxHealth);

    public float PowerRatio =>
        maxPower <= 0f
            ? 0f
            : Mathf.Clamp01(currentPower / maxPower);

    public bool IsTargetable =>
        isActiveAndEnabled && maxHealth > 0f && currentHealth > 0f;
}