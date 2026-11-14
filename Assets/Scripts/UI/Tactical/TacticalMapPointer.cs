using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>전술 화면의 UI 입력을 처리합니다. 전체도 드래그는 처음 잡은 위치를 유지합니다.</summary>
public sealed class TacticalMapPointer : MonoBehaviour, IPointerDownHandler, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler, IScrollHandler, IPointerEnterHandler, IPointerExitHandler
{
    public bool IsOverview;
    public TacticalHudView View;
    private bool _dragged;
    private Vector2 _start;
    public void OnPointerEnter(PointerEventData e) { if(!IsOverview)View.Owner?.SetMapPointerInside(true); }
    public void OnPointerExit(PointerEventData e) { if(!IsOverview)View.Owner?.SetMapPointerInside(false); }
    public void OnPointerDown(PointerEventData e)
    { _start = e.position; _dragged = false; GameInput.TryConsumePrimaryPointerPress(); }
    public void OnPointerClick(PointerEventData e)
    {
        if (!_dragged && View.Owner != null) View.Owner.PointerClick(e.position, IsOverview, e.button == PointerEventData.InputButton.Right);
    }
    public void OnBeginDrag(PointerEventData e)
    { _dragged = true; View.Owner?.BeginPointerDrag(_start, IsOverview); }
    public void OnDrag(PointerEventData e)
    { View.Owner?.PointerDrag(_start, e.position, e.delta, IsOverview, e.button == PointerEventData.InputButton.Left); }
    public void OnEndDrag(PointerEventData e) => View.Owner?.EndPointerDrag(_start, e.position, IsOverview);
    public void OnScroll(PointerEventData e) => View.Owner?.Zoom(e.scrollDelta.y);
}
