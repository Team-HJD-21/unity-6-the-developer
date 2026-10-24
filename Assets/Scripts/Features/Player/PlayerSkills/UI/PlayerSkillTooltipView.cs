using TMPro;
using UnityEngine;

/// <summary>미리 배치된 툴팁의 내용과 화면 안의 위치를 갱신합니다.</summary>
public sealed class PlayerSkillTooltipView : MonoBehaviour
{
    [SerializeField] private TMP_Text _title;
    [SerializeField] private TMP_Text _description;
    [SerializeField] private Vector2 _pointerOffset = new(18, -18);
    [SerializeField, Min(0)] private float _edgeMargin = 8;
    private PlayerSkillHud _hud;
    private Object _source;
    private PlayerSkillId _skill;
    public bool IsConfigured => _title != null && _description != null;
    public void Bind(PlayerSkillHud hud) { Hide(); _hud = hud; }
    public void Show(Object source, PlayerSkillId skill)
    {
        _source = source;
        _skill = skill;
        Refresh();
    }
    public void Hide(Object source = null)
    {
        // 이전 카드의 Exit가 새 카드의 Enter 뒤에 도착해도 새 툴팁을 숨기지 않습니다.
        if (source != null && _source != source) return;
        _source = null;
        _skill = PlayerSkillId.None;
        gameObject.SetActive(false);
    }
    public void Refresh()
    {
        if (_source == null || _skill == PlayerSkillId.None || _hud == null || _hud.Owner == null
            || (!_hud.IsOpen && !_hud.Owner.CanPrepare && !_hud.Owner.CanUse))
        {
            gameObject.SetActive(false);
            return;
        }
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        var owner = _hud.Owner;
        _title.text = PlayerSkillDescriptions.Name(_skill);
        string key = owner.Loadout.Q == _skill ? "Q · " : owner.Loadout.E == _skill ? "E · " : "";
        _description.text = key + PlayerSkillDescriptions.Description(_skill, owner);
        var canvas = (RectTransform)_hud.transform;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(canvas, GameInput.PointerPosition, null, out var point)) return;
        var rect = (RectTransform)transform;
        var bounds = canvas.rect;
        var size = rect.rect.size;
        rect.anchoredPosition = new Vector2(
            Mathf.Clamp(point.x + _pointerOffset.x, bounds.xMin + _edgeMargin, Mathf.Max(bounds.xMin + _edgeMargin, bounds.xMax - size.x - _edgeMargin)),
            Mathf.Clamp(point.y + _pointerOffset.y, Mathf.Min(bounds.yMax - _edgeMargin, bounds.yMin + size.y + _edgeMargin), bounds.yMax - _edgeMargin));
    }
}
