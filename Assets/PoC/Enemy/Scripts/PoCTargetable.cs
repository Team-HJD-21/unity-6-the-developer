using UnityEngine;

// 이 오브젝트를 AI의 공격 대상으로 만들어주는 컴포넌트
public class PoCTargetable : MonoBehaviour, ITargetable
{
    // 타깃 종류: Player, Turret 등
    [SerializeField]
    private TargetType targetType;

    // 최대 체력
    [SerializeField]
    private float maxHealth;

    // 현재 체력
    [SerializeField]
    private float currentHealth;

    // 최대 화력
    [SerializeField] 
    private float maxPower;
    
    // 현재 화력
    [SerializeField] 
    private float currentPower; 
    
    // AI가 추적하고 바라볼 위치
    public Transform TargetTransform => transform;

    // 이 오브젝트의 타깃 종류를 외부에 제공
    public TargetType TargetType => targetType;

    // 현재 체력을 0~1 사이의 비율로 반환
    public float HealthRatio =>
        maxHealth <= 0f
            ? 0f
            : currentHealth / maxHealth;
    
    public float PowerRatio =>
        maxPower <= 0f
            ? 0f
            : currentPower / maxPower;

    // 활성화되어 있고 살아 있으면 타깃으로 선택 가능
    public bool IsTargetable =>
        isActiveAndEnabled && currentHealth > 0f;
}


