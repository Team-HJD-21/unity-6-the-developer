// 기존 PlayerSkillHud의 준비 상태에 연결되는 반원/디스크 표시입니다. 실제 장착은 Player 원본 API만 호출합니다.
// 모든 참조는 프리팹 제작 시 연결하며, runtime에 UI 계층을 생성하지 않습니다.
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using TeamHJD.Game.Presentation;

[DefaultExecutionOrder(-250)]
public sealed class CombatSkillPicker : MonoBehaviour
{
    public CombatHudStyle Style;
    public RectTransform CanvasRect, PickerRect, CompactRect, DiskRect, TooltipRect;
    public CanvasGroup PickerGroup, CompactGroup, DiskGroup, TooltipGroup;
    public GameObject DropBand, CancelBadge, DestinationBadge;
    public Image LeftGradient, RightGradient, DiskBase;
    public TMP_Text DestinationText, DropHint, TooltipTitle, TooltipBody;
    public TMP_Text QDropLabel, EDropLabel;
    public Image HoverUnderline;
    public CombatHudArt DiskArt;
    public RectTransform QNeedle, ENeedle;
    public RectTransform QKeyBadge, EKeyBadge;
    public TMP_Text QNeedleLabel, ENeedleLabel;
    public CombatSkillWedge[] Wedges;
    public bool IsOpen => _open || _reveal.Value > .001f;
    public bool IsDragging => _drag;
    public int Destination { get; private set; } = -1;
    public float Reveal => _reveal.Value;
    public PlayerSkillHud Hud { get; private set; }
    private readonly CombatHudTransition _reveal = new(), _tooltip = new();
    private bool _open, _drag, _success, _rearm;
    private float _leaveAt, _successAt;
    private PlayerSkillId _dragSkill;
    private CombatSkillWedge _hover;
    private Vector2 _pointer;
    private float _qAngle = 150, _eAngle = 70;
    private PlayerSkillId _oldQ, _oldE;
    private float _needleAt;
    private float _fromQ, _fromE;

    public void Bind(PlayerSkillHud hud)
    {
        ResetView(); Hud = hud;
        foreach (var wedge in Wedges) wedge.Picker = this;
    }
    public void Unbind() { ResetView(); Hud = null; }
    public void Open()
    {
        if (Hud?.Owner == null || !Hud.Owner.CanPrepare || _success) return;
        _open = true; _leaveAt = Time.unscaledTime; _reveal.Set(1, Style.SlideSeconds);
    }
    public void Close()
    {
        _open = _drag = _success = false; _hover = null; _rearm = true;
        Destination = -1; _reveal.Set(0, Style.SlideSeconds); _tooltip.Set(0, Style.HoverSeconds);
        DropBand.SetActive(false); DiskGroup.alpha = 0; DiskGroup.blocksRaycasts = false;
        DropHint.enabled = false; HoverUnderline.enabled = false;
    }
    public void Hover(CombatSkillWedge wedge, bool inside)
    {
        if (_drag || _success) return;
        if (inside) _hover = wedge;
        else if (_hover == wedge) _hover = null;
    }
    public bool BeginDrag(PlayerSkillId skill, Vector2 screen)
    {
        if (!_open || Hud?.Owner == null || !Hud.Owner.CanPrepare || !Hud.Owner.IsAvailable(skill)) return false;
        _drag = true; _success = false; _dragSkill = skill; _hover = null;
        foreach (var wedge in Wedges) wedge.ResetMotion();
        DiskArt.Show(skill); DiskBase.color = Color.white; DiskGroup.alpha = 1;
        _tooltip.Snap(1); DropBand.SetActive(true);
        GameInput.ReservePrimaryPointerForPlacement();
        Drag(screen); return true;
    }
    public void Drag(Vector2 screen)
    {
        if (!_drag) return;
        _pointer = Local(screen);
        Destination = CombatHudGeometry.DropSlot(_pointer + CanvasRect.rect.size * .5f, CanvasRect.rect.size);
        UpdateDragPresentation();
    }
    public bool Release(Vector2 screen)
    {
        if (!_drag) return false;
        Drag(screen); GameInput.ReservePrimaryPointerForPlacement();
        _drag = false;
        bool committed = Destination >= 0 && Hud?.Owner != null && Hud.Owner.TryEquip(Destination, _dragSkill);
        DropBand.SetActive(false); _tooltip.Set(0, Style.HoverSeconds);
        DropHint.enabled = false; HoverUnderline.enabled = false;
        if (committed)
        {
            _success = true; _successAt = Time.unscaledTime;
            DestinationBadge.SetActive(false); CancelBadge.SetActive(false);
        }
        else
        {
            DiskGroup.alpha = 0; Destination = -1;
            _leaveAt = Time.unscaledTime;
        }
        return committed;
    }
    private Vector2 Local(Vector2 screen)
    {
        var canvas = GetComponentInParent<Canvas>();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(CanvasRect, screen,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera, out Vector2 local);
        return local;
    }
    private void Update()
    {
        if (Hud?.Owner == null) return;
        if (!Hud.AreCombatControlsVisible)
        { ResetView(); CompactGroup.alpha = 0; CompactGroup.blocksRaycasts = false; return; }
        float scale = PickerRect.localScale.x;
        Vector2 p = Local(GameInput.PointerPosition);
        bool near = p.y < -CanvasRect.rect.height * .5f + Mathf.Max(80, 130 * scale) && Mathf.Abs(p.x) < Mathf.Max(150, 255 * scale);
        bool overPicker = p.y < -CanvasRect.rect.height * .5f + 375 * scale && Mathf.Abs(p.x) < 370 * scale;
        if (Hud.Owner.IsSelectingTurret) _rearm = true;
        if (!near) _rearm = false;
        if (!_open && !IsOpen && !_rearm && near && Hud.Owner.CanPrepare && !Hud.Owner.IsSelectingTurret) Hud.Open();
        if (IsOpen && UIEscapeInput.TryConsume()) { Close(); return; }
        if (IsOpen && !Hud.Owner.CanPrepare) Close();
        if (_drag)
        {
            GameInput.ReservePrimaryPointerForPlacement();
            Drag(GameInput.PointerPosition);
            if (GameInput.SecondaryPointerWasPressedThisFrame) { _drag = false; Close(); }
        }
        if (_success)
        {
            GameInput.ReservePrimaryPointerForPlacement();
            float t = Mathf.Clamp01((Time.unscaledTime - _successAt) / Style.EquipSeconds);
            DiskBase.color = Color.Lerp(Color.white, new Color(1.6f, 1.6f, 1.45f), Mathf.Sin(t * Mathf.PI));
            DiskGroup.alpha = 1 - CombatHudMotion.Sigmoid(t);
            if (t >= 1) Close();
        }
        if (_open && !_drag && !_success)
        {
            if (overPicker || _hover != null) _leaveAt = Time.unscaledTime;
            else if (Time.unscaledTime - _leaveAt >= Style.PickerLeaveSeconds) Close();
        }
        float v = _reveal.Tick(Time.unscaledDeltaTime);
        PickerRect.anchoredPosition = new Vector2(0, -360 * scale * (1 - v));
        PickerGroup.alpha = v; PickerGroup.blocksRaycasts = _open && !_success;
        CompactGroup.alpha = 1 - v; CompactGroup.blocksRaycasts = !IsOpen;
        CompactRect.anchoredPosition = new Vector2(0, -160 * v);
        UpdateTooltip(); UpdateNeedles();
        foreach (var wedge in Wedges)
            wedge.Refresh(wedge == _hover || (_drag && wedge.Skill == _dragSkill), wedge == _hover && !_drag && !_success);
    }
    private void UpdateTooltip()
    {
        PlayerSkillId skill = _drag ? _dragSkill : _hover != null ? _hover.Skill : PlayerSkillId.None;
        bool visible = _open && !_success && skill != PlayerSkillId.None;
        _tooltip.Set(visible ? 1 : 0, Style.HoverSeconds);
        TooltipGroup.alpha = _tooltip.Tick(Time.unscaledDeltaTime);
        TooltipGroup.blocksRaycasts = false;
        if (!visible) return;
        TooltipTitle.text = PlayerSkillDescriptions.Name(skill);
        TooltipBody.text = skill == PlayerSkillId.PlayerTurret ? "주변 8방향에 설치\n자동 공격 · 가까이서 F 회수" : PlayerSkillDescriptions.Summary(skill);
        float scale = PickerRect.localScale.x;
        Vector2 size = TooltipRect.rect.size * TooltipRect.localScale.x, pos;
        if (_drag) pos = DiskRect.anchoredPosition + new Vector2(0, -205 * scale);
        else
        {
            Vector2 point = CombatHudGeometry.Radial(262 * scale, CombatHudGeometry.SkillAngle(skill));
            pos = new Vector2(point.x - 30 * scale, -CanvasRect.rect.height * .5f + 440 * scale);
        }
        Vector2 half = CanvasRect.rect.size * .5f;
        pos.x = Mathf.Clamp(pos.x, -half.x + size.x * .5f + 10, half.x - size.x * .5f - 10);
        pos.y = Mathf.Clamp(pos.y, -half.y + size.y * .5f + 10, half.y - size.y * .5f - 10);
        TooltipRect.anchoredPosition = pos;
    }
    private void UpdateDragPresentation()
    {
        Vector2 half = CanvasRect.rect.size * .5f;
        float scale = PickerRect.localScale.x;
        DiskRect.anchoredPosition = new Vector2(Mathf.Clamp(_pointer.x - 65 * scale, -half.x + 110 * scale, half.x - 110 * scale), Mathf.Clamp(_pointer.y + 65 * scale, -half.y + 110 * scale, half.y - 110 * scale));
        CancelBadge.SetActive(Destination < 0); DestinationBadge.SetActive(Destination >= 0);
        DestinationText.text = Destination == 0 ? "Q" : "E";
        // 넓은 영역의 강조와 디스크 배지만으로 목적지/취소를 예고합니다.
        DropHint.enabled = false;
        QDropLabel.color = Destination == 0 ? Style.Accent : Style.Text;
        EDropLabel.color = Destination == 1 ? Style.Accent : Style.Text;
        HoverUnderline.enabled = false;
        LeftGradient.sprite = Style.Get(Destination == 0 ? "drop_global_horizontal_hover" : "drop_global_horizontal_idle");
        RightGradient.sprite = Style.Get(Destination == 1 ? "drop_global_horizontal_hover" : "drop_global_horizontal_idle");
    }
    private void UpdateNeedles()
    {
        var loadout = Hud.Owner.Loadout;
        if (_oldQ != loadout.Q || _oldE != loadout.E)
        {
            _fromQ = _qAngle; _fromE = _eAngle; _needleAt = Time.unscaledTime;
            _oldQ = loadout.Q; _oldE = loadout.E;
        }
        float t = CombatHudMotion.Sigmoid((Time.unscaledTime - _needleAt) / Style.SlideSeconds);
        _qAngle = Mathf.LerpAngle(_fromQ, CombatHudGeometry.SkillAngle(loadout.Q), t);
        _eAngle = Mathf.LerpAngle(_fromE, CombatHudGeometry.SkillAngle(loadout.E), t);
        Needle(QNeedle, QKeyBadge, QNeedleLabel, loadout.Q, _qAngle, 100);
        Needle(ENeedle, EKeyBadge, ENeedleLabel, loadout.E, _eAngle, 142);
    }
    private static void Needle(RectTransform needle, RectTransform badge, TMP_Text label, PlayerSkillId skill, float angle, float radius)
    {
        needle.gameObject.SetActive(skill != PlayerSkillId.None); label.enabled = skill != PlayerSkillId.None;
        needle.localRotation = Quaternion.Euler(0, 0, angle - 90);
        if (badge != null)
        {
            badge.gameObject.SetActive(skill != PlayerSkillId.None);
            badge.anchoredPosition = CombatHudGeometry.Radial(radius, angle);
        }
    }
    private void ResetView()
    {
        _open = _drag = _success = _rearm = false; _hover = null; Destination = -1;
        _reveal.Snap(0); _tooltip.Snap(0);
        if (PickerGroup != null) { PickerGroup.alpha = 0; PickerGroup.blocksRaycasts = false; }
        if (CompactGroup != null) { CompactGroup.alpha = 1; CompactGroup.blocksRaycasts = true; }
        if (DiskGroup != null) { DiskGroup.alpha = 0; DiskGroup.blocksRaycasts = false; }
        if (TooltipGroup != null) { TooltipGroup.alpha = 0; TooltipGroup.blocksRaycasts = false; }
        if (DropBand != null) DropBand.SetActive(false);
        if (CancelBadge != null) CancelBadge.SetActive(false);
        if (DestinationBadge != null) DestinationBadge.SetActive(false);
        if (DropHint != null) DropHint.enabled = false;
        if (HoverUnderline != null) HoverUnderline.enabled = false;
        if (PickerRect != null) PickerRect.anchoredPosition = new Vector2(0, -360);
        if (CompactRect != null) CompactRect.anchoredPosition = Vector2.zero;
        foreach (var wedge in Wedges) if (wedge != null) wedge.ResetMotion();
    }
    private void OnApplicationFocus(bool focus) { if (!focus && (_drag || _success)) Close(); }
    private void OnApplicationPause(bool paused) { if (paused && (_drag || _success)) Close(); }
    private void OnDisable() => ResetView();
}
