// 바 하나의 Filled 수치와 hover/click 의도만 소유합니다. 고정 모드는 부모 그룹의 상태입니다.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatVitalsBar : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public CombatVitalsGroup Owner;
    public Image Fill;
    public Image DamageTrail;
    public bool DamageFeedback;
    public Color HitColor = new(.98f, .77f, .59f, 1);
    private Color _restColor;
    private bool _colorKnown;
    private float _hitAt = float.NegativeInfinity, _trailStart;
    public int Current { get; private set; }
    public int Maximum { get; private set; }
    public void Set(int current, int maximum)
    {
        int nextMaximum = Mathf.Max(0, maximum), next = Mathf.Clamp(current, 0, nextMaximum);
        bool sameMaximum = Maximum == nextMaximum;
        bool hit = DamageFeedback && Maximum > 0 && sameMaximum && next < Current;
        if (!_colorKnown) { _restColor = Fill.color; _colorKnown = true; }
        if (hit)
        {
            _trailStart = Mathf.Max(DamageTrail != null ? DamageTrail.fillAmount : 0, Fill.fillAmount);
            _hitAt = Time.unscaledTime;
            Owner.NotifyDamage();
        }
        Maximum = Mathf.Max(0, maximum); Current = Mathf.Clamp(current, 0, Maximum);
        Fill.fillAmount = Maximum > 0 ? (float)Current / Maximum : 0;
        if (!hit && (!sameMaximum || Maximum == 0 || (DamageTrail != null && DamageTrail.fillAmount < Fill.fillAmount)))
        {
            _hitAt = float.NegativeInfinity;
            if (DamageTrail != null) DamageTrail.fillAmount = Fill.fillAmount;
        }
    }
    private void LateUpdate()
    {
        if (!_colorKnown) return;
        float elapsed = Time.unscaledTime - _hitAt;
        Fill.color = Color.Lerp(HitColor, _restColor, Mathf.SmoothStep(0, 1, elapsed / .25f));
        if (DamageTrail != null)
            DamageTrail.fillAmount = Mathf.Lerp(_trailStart, Fill.fillAmount, Mathf.SmoothStep(0, 1, (elapsed - .12f) / .42f));
    }
    public void OnPointerEnter(PointerEventData data) => Owner.Hover(this, true);
    public void OnPointerExit(PointerEventData data) => Owner.Hover(this, false);
    public void OnPointerClick(PointerEventData data) { if (data.button == PointerEventData.InputButton.Left) Owner.TogglePin(); }
}
