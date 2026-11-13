using UnityEngine;
using UnityEngine.UI;

/// <summary>실제 사용 요청에 대한 짧은 눌림과 복귀. 선택 버튼의 클릭은 스킬 발동으로 간주하지 않습니다.</summary>
public sealed class CombatSkillSlotFeedback : MonoBehaviour
{
    public Image Overlay;
    public Color Accepted = new(.84f, .75f, .49f, .22f), Rejected = new(.83f, .48f, .41f, .25f);
    private float _at = float.NegativeInfinity;
    private Color _tint;
    public void Press(bool accepted)
    {
        _at = Time.unscaledTime; _tint = accepted ? Accepted : Rejected;
        transform.localScale = Vector3.one * .955f;
        if (Overlay != null) Overlay.color = _tint;
    }
    private void LateUpdate()
    {
        float t = Mathf.Clamp01((Time.unscaledTime - _at) / .22f);
        float weight = 1 - Mathf.SmoothStep(0, 1, t);
        transform.localScale = Vector3.one * (1 - .045f * weight);
        if (Overlay != null) { var c = _tint; c.a *= weight; Overlay.color = c; }
    }
    private void OnDisable()
    {
        _at = float.NegativeInfinity; transform.localScale = Vector3.one;
        if (Overlay != null) Overlay.color = Color.clear;
    }
}
