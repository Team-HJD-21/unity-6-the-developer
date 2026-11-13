// 부채꼴의 실제 모양으로 raycast하고, hover 설명과 drag 의도만 Picker로 전달합니다.
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class CombatSkillWedge : MonoBehaviour, ICanvasRaycastFilter,
    IPointerEnterHandler, IPointerExitHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public CombatSkillPicker Picker;
    public CombatHudStyle Style;
    public PlayerSkillId Skill;
    public int Index;
    public Image Edge, Surface;
    public RectTransform Art;
    public CanvasGroup LiftShadow, Grip;
    private float _hover;
    private Vector2 _rest;
    private bool _restKnown;
    public bool IsRaycastLocationValid(Vector2 screen, Camera camera)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, camera, out Vector2 local);
        return CombatHudGeometry.Wedge(local) == Index;
    }
    public void Refresh(bool hovered, bool showPickup = true)
    {
        _hover = Mathf.MoveTowards(_hover, hovered ? 1 : 0, Time.unscaledDeltaTime / Style.HoverSeconds);
        Edge.sprite = Style.Get($"picker_wedge_{Index}_edge_{(hovered ? "hover" : "idle")}");
        Surface.color = Color.Lerp(Color.white, new Color(1.15f, 1.1f, .85f), CombatHudMotion.Sigmoid(_hover));
        if (Art == null) return;
        if (!_restKnown) { _rest = Art.anchoredPosition; _restKnown = true; }
        float lift = showPickup && (Picker == null || !Picker.IsDragging) ? CombatHudMotion.Sigmoid(_hover) : 0;
        Art.anchoredPosition = _rest + new Vector2(3, 7) * lift;
        Art.localScale = Vector3.one * (1 + .045f * lift);
        if (LiftShadow != null) LiftShadow.alpha = .18f * lift;
        if (Grip != null) Grip.alpha = .7f * lift;
    }
    public void ResetMotion()
    {
        _hover = 0;
        if (Art != null && _restKnown) { Art.anchoredPosition = _rest; Art.localScale = Vector3.one; }
        if (LiftShadow != null) LiftShadow.alpha = 0;
        if (Grip != null) Grip.alpha = 0;
    }
    public void OnPointerEnter(PointerEventData data) => Picker.Hover(this, true);
    public void OnPointerExit(PointerEventData data) => Picker.Hover(this, false);
    public void OnBeginDrag(PointerEventData data)
    {
        if (data.button == PointerEventData.InputButton.Left) Picker.BeginDrag(Skill, data.position);
    }
    public void OnDrag(PointerEventData data) => Picker.Drag(data.position);
    public void OnEndDrag(PointerEventData data) => Picker.Release(data.position);
    private void OnDisable() => ResetMotion();
}
