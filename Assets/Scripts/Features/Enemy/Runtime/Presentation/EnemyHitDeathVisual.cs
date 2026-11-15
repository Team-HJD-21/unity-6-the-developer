using UnityEngine;

/// <summary>
/// 몬스터가 피해를 받으면 한 번 붉게 점멸하고, 사망하면 정지한 채 페이드아웃한다.
/// </summary>
public sealed class EnemyHitDeathVisual : MonoBehaviour
{
    // Inspector 설정과 제작 참조

    [Header("피격 연출")]
    [SerializeField] private Color hitColor = new(1f, 0.25f, 0.25f, 1f);
    [SerializeField, Min(0f)] private float hitFlashDuration = 0.12f;

    [Header("사망 연출")]
    [SerializeField, Min(0f)] private float deathFadeDuration = 0.55f;

    // 실행 중 참조와 상태
    private SpriteRenderer[] _spriteRenderers;
    private Color[] _originalColors;
    private Collider2D[] _colliders;
    private bool[] _originalColliderStates;
    private Rigidbody2D _rigidbody2D;
    private bool _originalRigidbodySimulation;
    private float _hitFlashElapsed;
    private float _deathElapsed;
    private bool _isDying;

    /// <summary>서버가 네트워크 제거까지 기다릴 사망 페이드 시간을 반환한다.</summary>
    public float DeathFadeDuration => deathFadeDuration;

    // Unity 생명주기

    /// <summary>재사용 시 복원할 원래 색상과 충돌·물리 상태를 저장한다.</summary>
    private void Awake()
    {
        _spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        _originalColors = new Color[_spriteRenderers.Length];
        for (int i = 0; i < _spriteRenderers.Length; i++)
            _originalColors[i] = _spriteRenderers[i].color;

        _colliders = GetComponentsInChildren<Collider2D>(true);
        _originalColliderStates = new bool[_colliders.Length];
        for (int i = 0; i < _colliders.Length; i++)
            _originalColliderStates[i] = _colliders[i].enabled;

        _rigidbody2D = GetComponent<Rigidbody2D>();
        if (_rigidbody2D != null)
            _originalRigidbodySimulation = _rigidbody2D.simulated;
    }

    /// <summary>사망 페이드를 우선 갱신하고 생존 중에는 피격 색을 원래 색으로 되돌린다.</summary>
    private void Update()
    {
        if (_isDying)
        {
            _deathElapsed += Time.deltaTime;
            ApplyDeathFade();
            return;
        }

        if (_hitFlashElapsed < 0f)
            return;

        _hitFlashElapsed += Time.deltaTime;
        float progress = hitFlashDuration <= 0f
            ? 1f
            : Mathf.Clamp01(_hitFlashElapsed / hitFlashDuration);

        ApplyHitFlash(1f - progress);
        if (progress >= 1f)
            _hitFlashElapsed = -1f;
    }

    // 외부 호출과 상태 변경

    /// <summary>재사용되는 몬스터의 연출과 충돌 상태를 초기화한다.</summary>
    public void ResetPresentation()
    {
        _isDying = false;
        _hitFlashElapsed = -1f;
        _deathElapsed = 0f;
        RestoreOriginalColors();

        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = _originalColliderStates[i];
        }

        if (_rigidbody2D != null)
            _rigidbody2D.simulated = _originalRigidbodySimulation;
    }

    /// <summary>피격 색을 즉시 적용한 뒤 짧게 원래 색으로 되돌린다.</summary>
    public void PlayHitFlash()
    {
        if (_isDying)
            return;

        _hitFlashElapsed = 0f;
        if (hitFlashDuration <= 0f)
        {
            RestoreOriginalColors();
            _hitFlashElapsed = -1f;
            return;
        }

        ApplyHitFlash(1f);
    }

    /// <summary>네트워크 사망 시각을 기준으로 페이드를 시작하고 물리 충돌을 끈다.</summary>
    public void BeginDeathFade(double deathTime, double currentServerTime)
    {
        if (_isDying)
            return;

        _isDying = true;
        _hitFlashElapsed = -1f;
        _deathElapsed = Mathf.Max(0f, (float)(currentServerTime - deathTime));

        foreach (Collider2D collider in _colliders)
        {
            if (collider != null)
                collider.enabled = false;
        }

        if (_rigidbody2D != null)
        {
            _rigidbody2D.linearVelocity = Vector2.zero;
            _rigidbody2D.angularVelocity = 0f;
            _rigidbody2D.simulated = false;
        }

        ApplyDeathFade();
    }

    // 내부 처리

    /// <summary>원래 투명도를 유지하면서 피격 색의 혼합 비율을 적용한다.</summary>
    private void ApplyHitFlash(float amount)
    {
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color tint = hitColor;
            tint.a = _originalColors[i].a;
            spriteRenderer.color = Color.Lerp(_originalColors[i], tint, amount);
        }
    }

    /// <summary>사망 경과 시간에 따라 모든 자식 스프라이트의 투명도를 낮춘다.</summary>
    private void ApplyDeathFade()
    {
        float progress = deathFadeDuration <= 0f
            ? 1f
            : Mathf.Clamp01(_deathElapsed / deathFadeDuration);

        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            SpriteRenderer spriteRenderer = _spriteRenderers[i];
            if (spriteRenderer == null)
                continue;

            Color color = _originalColors[i];
            color.a *= 1f - progress;
            spriteRenderer.color = color;
        }
    }

    /// <summary>피격과 사망 연출 이전에 저장한 색상을 복원한다.</summary>
    private void RestoreOriginalColors()
    {
        for (int i = 0; i < _spriteRenderers.Length; i++)
        {
            if (_spriteRenderers[i] != null)
                _spriteRenderers[i].color = _originalColors[i];
        }
    }
}
