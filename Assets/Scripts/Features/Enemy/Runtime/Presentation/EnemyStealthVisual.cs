using UnityEngine;

[RequireComponent(typeof(SpriteRenderer), typeof(EnemyDetection))]
public sealed class EnemyStealthVisual : MonoBehaviour
{
    private static readonly int StealthAmountId =
        Shader.PropertyToID("_StealthAmount");

    [Header("참조")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private EnemyDetection enemyDetection;

    [Header("전환 시간")]
    [SerializeField, Min(0f)] private float revealDuration = 1f;
    [SerializeField, Min(0f)] private float stealthDuration = 2f;
    
    [SerializeField, Range(0f, 1f)]
    private float detectedStealthAmount = 0.7f;
    [SerializeField, Range(0f, 1f)]
    private float undetectedStealthAmount = 0.95f;
    
    private MaterialPropertyBlock _propertyBlock;
    private float _currentStealthAmount;
    private float _targetStealthAmount;

    private void Awake()
    {
        if (spriteRenderer == null)
            spriteRenderer = GetComponent<SpriteRenderer>();

        if (enemyDetection == null)
            enemyDetection = GetComponent<EnemyDetection>();

        _propertyBlock = new MaterialPropertyBlock();
    }

    private void OnEnable()
    {
        enemyDetection.DetectionChanged += OnDetectionChanged;

        _currentStealthAmount = enemyDetection.IsDetected ? detectedStealthAmount : undetectedStealthAmount;
        _targetStealthAmount = _currentStealthAmount;
        ApplyStealthAmount();
    }

    private void OnDisable()
    {
        enemyDetection.DetectionChanged -= OnDetectionChanged;
    }

    private void Update()
    {
        if (Mathf.Approximately(_currentStealthAmount, _targetStealthAmount))
            return;

        float duration = _targetStealthAmount < _currentStealthAmount
            ? revealDuration
            : stealthDuration;

        if (duration <= 0f)
        {
            _currentStealthAmount = _targetStealthAmount;
        }
        else
        {
            _currentStealthAmount = Mathf.MoveTowards(
                _currentStealthAmount,
                _targetStealthAmount,
                Time.deltaTime / duration);
        }

        ApplyStealthAmount();
    }

    private void OnDetectionChanged(bool isDetected)
    {
        _targetStealthAmount = isDetected ? detectedStealthAmount : undetectedStealthAmount;
    }

    private void ApplyStealthAmount()
    {
        spriteRenderer.GetPropertyBlock(_propertyBlock);
        _propertyBlock.SetFloat(StealthAmountId, _currentStealthAmount);
        spriteRenderer.SetPropertyBlock(_propertyBlock);
    }
}