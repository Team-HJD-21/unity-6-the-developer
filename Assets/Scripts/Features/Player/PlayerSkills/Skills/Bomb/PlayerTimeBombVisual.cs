using TMPro;
using UnityEngine;

/// <summary>시한폭탄의 타이머와 점멸을 표시하고 기존 폭발 애니메이션을 재생합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerTimeBombVisual : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _body;
    [SerializeField] private Animator _explosionAnimator;
    [Tooltip("기존 폭발 클립 전체의 보이는 최대 외곽 지름. 투명 여백을 제외한 스케일 1 기준입니다.")]
    [SerializeField, Min(.01f)] private float _explosionDiameterAtScaleOne = 11.521f;
    [SerializeField] private TMP_FontAsset _displayFont;
    [SerializeField] private Vector2 _displayOffset = new Vector2(0, .06f);
    private TextMeshPro _countdown;
    private bool _detonated;
    private static readonly int EffectState = Animator.StringToHash("Base Layer.effect");

    private void EnsureVisuals()
    {
        if (_body == null) _body = GetComponent<SpriteRenderer>();
        if (_countdown == null && _displayFont != null)
        {
            _countdown = new GameObject("Live Timer").AddComponent<TextMeshPro>();
            _countdown.transform.SetParent(transform, false);
            _countdown.transform.localPosition = _displayOffset;
            _countdown.font = _displayFont;
            _countdown.rectTransform.sizeDelta = new Vector2(.27f, .13f);
            _countdown.enableAutoSizing = true;
            _countdown.fontSizeMin = .5f;
            _countdown.fontSizeMax = 2f;
            _countdown.alignment = TextAlignmentOptions.Center;
            _countdown.renderer.sortingOrder = 111;
        }
    }

    public void BeginFuse(float seconds, float radius)
    {
        EnsureVisuals();
        _detonated = false;
        if (_body != null) { _body.enabled = true; _body.color = Color.white; }
        if (_countdown != null) _countdown.gameObject.SetActive(true);
        if (_explosionAnimator != null && _explosionAnimator.gameObject != gameObject)
            _explosionAnimator.gameObject.SetActive(false);
        UpdateFuse(0, seconds, radius);
    }

    public void UpdateFuse(float elapsed, float seconds, float radius)
    {
        if (_detonated) return;
        float progress = Mathf.Clamp01(elapsed / Mathf.Max(.01f, seconds));
        float wave = .5f + .5f * Mathf.Sin(elapsed * 2 * Mathf.PI * (2 + 6 * progress));
        Color warning = Color.Lerp(new Color(1, .65f, .15f), new Color(1, .2f, .08f), progress);
        if (_body != null) _body.color = Color.Lerp(Color.white, new Color(1, .55f, .45f), wave * progress * .65f);
        if (_countdown != null)
        {
            _countdown.SetText("{0:1}", Mathf.Max(0, seconds - elapsed));
            _countdown.color = Color.Lerp(warning * .65f, warning, wave);
        }
    }

    public void PlayExplosion(float radius, float seconds)
    {
        if (_detonated) return;
        EnsureVisuals();
        _detonated = true;
        if (_body != null) _body.enabled = false;
        if (_countdown != null) _countdown.gameObject.SetActive(false);
        if (_explosionAnimator == null || _explosionAnimator.runtimeAnimatorController == null) return;
        _explosionAnimator.gameObject.SetActive(true);
        _explosionAnimator.enabled = true;
        if (!_explosionAnimator.HasState(0, EffectState)) return;
        // 기존 폭발 클립만 재생해 대기 상태의 옛 폭탄 그림이 노출되지 않도록 합니다.
        _explosionAnimator.speed = 1;
        _explosionAnimator.Play(EffectState, 0, 0);
        _explosionAnimator.Update(0);
        MatchExplosionRadius(radius);
        float clipSeconds = _explosionAnimator.GetCurrentAnimatorStateInfo(0).length;
        _explosionAnimator.speed = Mathf.Max(.01f, clipSeconds) / Mathf.Max(.01f, seconds);
    }

    private void MatchExplosionRadius(float radius)
    {
        Transform effect = _explosionAnimator.transform;
        Vector3 parentScale = effect.parent != null ? effect.parent.lossyScale : Vector3.one;
        float diameter = Mathf.Max(.01f, radius) * 2f;
        float referenceDiameter = Mathf.Max(.01f, _explosionDiameterAtScaleOne);

        // 본체 크기와 무관하게 최대 폭발 외곽이 월드 피해 반경에 맞도록 부모 스케일을 상쇄합니다.
        effect.localScale = new Vector3(
            diameter / (referenceDiameter * Mathf.Max(.0001f, Mathf.Abs(parentScale.x))),
            diameter / (referenceDiameter * Mathf.Max(.0001f, Mathf.Abs(parentScale.y))),
            effect.localScale.z);
    }
}
