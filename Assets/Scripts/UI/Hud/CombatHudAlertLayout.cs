using TeamHJD.Game.Presentation;
using UnityEngine;

/// <summary>Alert를 실제 하단 HUD 경계 위에 두며 설명/드래그와 겹칠 때만 자리를 비웁니다.</summary>
[DefaultExecutionOrder(1000)]
public sealed class CombatHudAlertLayout : MonoBehaviour
{
    public PlayerSkillHud Hud;
    public CombatSkillPicker Picker;
    public RectTransform CompactTooltip;
    [Min(0)] public float CollisionPadding = 6;
    public AlertStripView View;
    private Canvas _canvas;
    private float _nextSearch;
    private bool _reserved;
    private TacticalCommandController _tactical;
    private float _nextTacticalSearch;
    private readonly Vector3[] _corners = new Vector3[4];

    private void LateUpdate() => RefreshLayout();
    public void RefreshLayout()
    {
        if (View == null && Time.unscaledTime >= _nextSearch)
        { _nextSearch = Time.unscaledTime + .5f; View = FindFirstObjectByType<AlertStripView>(); }
        if (View == null || Hud == null || Picker == null) return;
        if (_canvas == null) _canvas = Hud.GetComponent<Canvas>();
        if(_tactical==null && Time.unscaledTime>=_nextTacticalSearch)
        {
            _nextTacticalSearch=Time.unscaledTime+.5f;
            foreach(var tactical in FindObjectsByType<TacticalCommandController>(FindObjectsSortMode.None))
                if(tactical.gameObject.scene==Hud.gameObject.scene){_tactical=tactical;break;}
        }
        if(_tactical!=null && _tactical.IsOpen)
        {
            View.ResetBottomClearance(); _reserved=true;
            var tactical=_tactical.View;
            for(int pass=0;pass<3;pass++)
            {
                Avoid(tactical.Dock,true); Avoid(tactical.UpgradeDrawer,true);
                Avoid(tactical.Overview.parent as RectTransform,true);
                Avoid(tactical.AutoActivate.transform as RectTransform,true);
                Avoid(tactical.BatchActivate.transform as RectTransform,true);
                Avoid(tactical.BatchDeactivate.transform as RectTransform,true);
                Avoid(tactical.SelectMultiple.transform as RectTransform,true);
            }
            return;
        }
        if (!Hud.AreCombatControlsVisible || !_canvas.enabled)
        { ResetReservation(); return; }

        Rect compact = ScreenRect(Picker.CompactRect);
        // The compact dock slides out while the selector opens; retain its resting top as a minimum.
        Vector3 displacement = Picker.CompactRect.parent.TransformVector(new Vector3(0, -Picker.CompactRect.anchoredPosition.y, 0));
        Camera camera = UiCamera(Picker.CompactRect);
        float restingTop = compact.yMax + RectTransformUtility.WorldToScreenPoint(camera, Picker.CompactRect.position + displacement).y
            - RectTransformUtility.WorldToScreenPoint(camera, Picker.CompactRect.position).y;
        float occupied = Mathf.Max(0, restingTop);
        if (Picker.PickerGroup.alpha > .001f) occupied = Mathf.Max(occupied, ScreenRect(Picker.PickerRect).yMax);
        View.SetScreenBottomClearance(occupied); _reserved = true;

        // Far-away drag art does not move the alert. Only intersecting visible rectangles reserve more space.
        for (int pass = 0; pass < 3; pass++)
        {
            Avoid(CompactTooltip, CompactTooltip != null && CompactTooltip.gameObject.activeInHierarchy);
            Avoid(Picker.TooltipRect, Picker.TooltipGroup.alpha > .001f);
            bool disk = Picker.DiskGroup.alpha > .001f;
            Avoid(Picker.DiskBase.rectTransform, disk);
            Avoid(Picker.CancelBadge.transform as RectTransform, disk && Picker.CancelBadge.activeSelf);
            Avoid(Picker.DestinationBadge.transform as RectTransform, disk && Picker.DestinationBadge.activeSelf);
        }
    }
    private void Avoid(RectTransform target, bool visible)
    {
        if (!visible || target == null || !target.gameObject.activeInHierarchy) return;
        Rect obstacle = ScreenRect(target), alert = ScreenRect((RectTransform)View.transform);
        float padding = CollisionPadding * _canvas.scaleFactor;
        obstacle.xMin -= padding; obstacle.xMax += padding;
        obstacle.yMin -= padding; obstacle.yMax += padding;
        if (obstacle.Overlaps(alert)) View.SetScreenBottomClearance(obstacle.yMax);
    }
    private Rect ScreenRect(RectTransform rect)
    {
        rect.GetWorldCorners(_corners); Camera camera = UiCamera(rect);
        Vector2 min = new(float.PositiveInfinity, float.PositiveInfinity), max = new(float.NegativeInfinity, float.NegativeInfinity);
        foreach (var corner in _corners)
        { Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, corner); min = Vector2.Min(min, point); max = Vector2.Max(max, point); }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }
    private static Camera UiCamera(RectTransform rect)
    {
        var canvas = rect.GetComponentInParent<Canvas>();
        return canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
    }
    private void ResetReservation()
    { if (_reserved && View != null) View.ResetBottomClearance(); _reserved = false; }
    private void OnDisable() => ResetReservation();
    private void OnDestroy() => ResetReservation();
}
