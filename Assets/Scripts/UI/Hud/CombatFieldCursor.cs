using System.Collections.Generic;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>필드 공격 문맥만 조준선으로 표시합니다. 조작 권한이나 사격 판정은 변경하지 않습니다.</summary>
[DefaultExecutionOrder(950)]
public sealed class CombatFieldCursor : MonoBehaviour
{
    public PlayerSkillHud Hud;
    public CombatCursorView ViewPrefab;
    public CombatWeaponStack Weapons;
    public RectTransform Header;
    [Min(0)] public float UiClearance = 10;
    public bool IsAiming { get; private set; }
    public CombatCursorView View => _view;
    private CombatCursorView _view;
    private PlayerAttack _attack;
    private AppRoot _app;
    private AppScreenCanvasView _screens;
    private bool _ownsCursor, _nearUi;
    private float _nextUiCheck;
    private Vector2 _lastPointer;
    private EventSystem _eventSystem;
    private PointerEventData _pointer;
    private readonly List<RaycastResult> _hits = new();
    private readonly Vector3[] _corners = new Vector3[4];

    private void Awake()
    {
        _view = Instantiate(ViewPrefab);
        _view.name = "Combat Cursor (Local)";
        _view.Present(Vector2.zero, false);
    }
    private void LateUpdate()
    {
        var attack = Hud.Owner != null ? Hud.Owner.GetComponent<PlayerAttack>() : null;
        if (_attack != attack)
        {
            if (_attack != null) _attack.ShotFired -= OnShot;
            _attack = attack;
            if (_attack != null) _attack.ShotFired += OnShot;
        }
        if (_app == null) _app = FindFirstObjectByType<AppRoot>();
        Vector2 position = GameInput.PointerPosition;
        bool inside = UnityEngine.InputSystem.Mouse.current != null && position.x >= 0 && position.y >= 0 && position.x < Screen.width && position.y < Screen.height;
        bool focused = Application.isFocused;
#if UNITY_EDITOR
        var window = UnityEditor.EditorWindow.mouseOverWindow;
        focused &= window != null && window.GetType().Name == "GameView";
#endif
        bool field = focused && inside && Hud.IsFieldVisible && Hud.Owner != null && Hud.Owner.CanUse &&
            !Hud.Owner.IsPlacingSkill && !Hud.IsOpen && (_app == null || !_app.LocalInput.IsBlocked) && !HasOverlay();
        if (field && (Time.unscaledTime >= _nextUiCheck || position != _lastPointer))
        {
            _nearUi = NearUi(position); _nextUiCheck = Time.unscaledTime + .05f; _lastPointer = position;
        }
        IsAiming = field && !_nearUi;
        if (IsAiming) { Cursor.visible = false; _ownsCursor = true; }
        else RestoreCursor();
        _view.Present(position, IsAiming);
    }
    private bool NearUi(Vector2 position)
    {
        if (Contains(Header, position)) return true;
        if (Weapons != null && Weapons.SelectedIndex >= 0)
        {
            if (Contains(Weapons.PanelRect, position)) return true;
            if (Contains((RectTransform)Weapons.Slots[Weapons.SelectedIndex].transform, position)) return true;
            if (Weapons.NameGroup != null && Weapons.NameGroup.alpha > .001f && Contains(Weapons.NameRect, position)) return true;
        }
        if (EventSystem.current == null) return false;
        if (_eventSystem != EventSystem.current)
        { _eventSystem = EventSystem.current; _pointer = new PointerEventData(_eventSystem); }
        // UI 가장자리에서는 일반 포인터로 먼저 돌아옵니다. 커서 자체에는 GraphicRaycaster가 없습니다.
        for (int i = 0; i < 5; i++)
        {
            Vector2 offset = i == 0 ? Vector2.zero : new Vector2(i < 3 ? -UiClearance : UiClearance, i % 2 == 0 ? -UiClearance : UiClearance);
            _pointer.position = position + offset; _hits.Clear(); _eventSystem.RaycastAll(_pointer, _hits);
            foreach (var hit in _hits) if (hit.module is GraphicRaycaster) return true;
        }
        return false;
    }
    private bool Contains(RectTransform rect, Vector2 point)
    {
        if (rect == null || !rect.gameObject.activeInHierarchy) return false;
        rect.GetWorldCorners(_corners);
        var min = (Vector2)_corners[0]; var max = min;
        foreach (var corner in _corners) { min = Vector2.Min(min, corner); max = Vector2.Max(max, corner); }
        return point.x >= min.x - UiClearance && point.x <= max.x + UiClearance &&
            point.y >= min.y - UiClearance && point.y <= max.y + UiClearance;
    }
    private bool HasOverlay()
    {
        if (_app == null) return false;
        if (_screens == null) _screens = FindFirstObjectByType<AppScreenCanvasView>();
        if (_screens == null) return false;
        for (int i = 0; i < _screens.Screens.childCount; i++)
        {
            var child = _screens.Screens.GetChild(i);
            if (!child.gameObject.activeInHierarchy) continue;
            var group = child.GetComponent<CanvasGroup>();
            if (group == null || group.alpha > .001f) return true;
        }
        return false;
    }
    private void OnShot(WeaponType _) => _view.Shot();
    private void RestoreCursor()
    { if (_ownsCursor) { Cursor.visible = true; _ownsCursor = false; } }
    private void OnApplicationFocus(bool focused) { if (!focused) RestoreCursor(); }
    private void OnDisable()
    {
        RestoreCursor(); IsAiming = false;
        if (_attack != null) _attack.ShotFired -= OnShot;
        _attack = null;
        if (_view != null) _view.Present(Vector2.zero, false);
    }
    private void OnDestroy() { RestoreCursor(); if (_view != null) Destroy(_view.gameObject); }
}
