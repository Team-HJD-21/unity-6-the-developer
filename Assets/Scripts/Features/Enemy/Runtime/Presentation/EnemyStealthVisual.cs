using UnityEngine;

/// <summary>탐지 상태에 따라 개체별 은신 셰이더 값을 부드럽게 전환한다.</summary>
[RequireComponent(typeof(SpriteRenderer), typeof(EnemyDetection))]
public sealed class EnemyStealthVisual : MonoBehaviour
{
    // 상수와 공통 식별자
    private static readonly int StealthAmountId =
        Shader.PropertyToID("_StealthAmount");

    // Inspector 설정과 제작 참조

    [Header("참조")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private EnemyDetection enemyDetection;

    [Header("전환 시간")]
    [SerializeField, Min(0f)] private float revealDuration = 1f;
    [SerializeField, Min(0f)] private float stealthDuration = 2f;
    [SerializeField, Range(0f, 1f)]
    private float detectedStealthAmount = 0.6f;
    [SerializeField, Range(0f, 1f)]
    private float undetectedStealthAmount = 0.95f;

    // 실행 중 참조와 상태
    private MaterialPropertyBlock _propertyBlock;
    private float _currentStealthAmount;
    private float _targetStealthAmount;
    private float _transitionStartAmount;
    private float _transitionElapsed;

    // Unity 생명주기

    /// <summary>스프라이트·탐지 참조와 개체별 셰이더 값 전달용 블록을 준비한다.</summary>
    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (enemyDetection == null)
            enemyDetection = GetComponent<EnemyDetection>();

        _propertyBlock = new MaterialPropertyBlock();
    }

    /// <summary>탐지 이벤트를 구독하고 현재 탐지 상태에 맞춰 은신값을 초기화한다.</summary>
    private void OnEnable()
    {
        enemyDetection.DetectionChanged += OnDetectionChanged;

        _currentStealthAmount = enemyDetection.IsDetected ? detectedStealthAmount : undetectedStealthAmount;
        _targetStealthAmount = _currentStealthAmount;
        _transitionStartAmount = _currentStealthAmount;
        _transitionElapsed = 0f;
        ApplyStealthAmount();
    }

    /// <summary>전환 시작값에서 목표 은신값까지 설정된 시간 동안 보간한다.</summary>
    private void Update()
    {
        if (Mathf.Approximately(_currentStealthAmount, _targetStealthAmount))
            return;

        float duration = _targetStealthAmount < _transitionStartAmount
            ? revealDuration
            : stealthDuration;

        if (duration <= 0f)
        {
            _currentStealthAmount = _targetStealthAmount;
        }
        else
        {
            _transitionElapsed += Time.deltaTime;

            float progress = Mathf.Clamp01(_transitionElapsed / duration);
            _currentStealthAmount = Mathf.Lerp(
                _transitionStartAmount,
                _targetStealthAmount,
                progress);
        }

        ApplyStealthAmount();
    }

    /// <summary>탐지 상태 이벤트 구독을 해제한다.</summary>
    private void OnDisable()
    {
        enemyDetection.DetectionChanged -= OnDetectionChanged;
    }

    // 내부 처리

    /// <summary>전환 도중 상태가 바뀌어도 현재 은신값에서 새 전환을 시작한다.</summary>
    private void OnDetectionChanged(bool isDetected)
    {
        _transitionStartAmount = _currentStealthAmount;
        _targetStealthAmount = isDetected ? detectedStealthAmount : undetectedStealthAmount;
        _transitionElapsed = 0f;
    }

    /// <summary>공유 머티리얼을 복제하지 않고 이 개체의 은신값만 전달한다.</summary>
    private void ApplyStealthAmount()
    {
        spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(StealthAmountId, _currentStealthAmount);
        spriteRenderer.SetPropertyBlock(_propertyBlock);
    }
}
