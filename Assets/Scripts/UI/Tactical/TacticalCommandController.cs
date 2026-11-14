using System;
using System.Collections.Generic;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using EntityId = TeamHJD.Game.Domain.EntityId;

/// <summary>로컬 전술 카메라·입력·제작 HUD를 연결합니다. 상태 변경은 기존 Host 터렛 명령으로 요청합니다.</summary>
[DefaultExecutionOrder(-250)]
public sealed partial class TacticalCommandController : MonoBehaviour
{
    public InitialStagePlayerHUD Source;
    public TacticalHudStyle Style;
    [Header("Scene camera limits")]
    public Transform CameraLeftTop, CameraRightBottom;
    public bool IsOpen { get; private set; }
    public TacticalHudView View { get; private set; }
    public IReadOnlyList<TurretPresentationSnapshot> Turrets => _rows;
    public IReadOnlyList<Vector2> Enemies => _enemies;
    public RegionGridSnapshot Regions { get; private set; }
    public Rect WorldBounds { get; private set; } = new(-40, -40, 80, 80);
    public Vector2 PlayerPosition => _player != null ? (Vector2)_player.transform.position : Vector2.zero;
    public Vector2 CameraPosition => _rig != null ? (Vector2)_rig.position : PlayerPosition;
    public Camera OutputCamera => _camera != null ? _camera.OutputCamera : Camera.main;
    public Rect CurrentCameraRect => OutputCamera != null ? TacticalMapMath.CameraRect(OutputCamera.transform.position, OutputCamera.orthographicSize, OutputCamera.aspect) : new Rect();
    public EntityId SelectedId => _selected;
    public int SelectionCount => _batch.Count;
    public TurretPresentationSnapshot Hovered => _byId.TryGetValue(_hovered, out var hovered) ? hovered : null;
    public bool HasGoal => _hasGoal;
    public bool MultipleSelection { get; private set; }
    public Vector2 GoalPosition => _goal;
    private PlayerInfo _player;
    private PlayerControlState _control;
    private PlayerAttack _attack;
    private AppRoot _app;
    private CameraManager _camera;
    private Transform _rig;
    private IDisposable _cameraLease, _controlLease;
    private float _baseSize, _size, _nextSample, _lastShot = -100, _hoverSince;
    private EntityId _selected, _hovered;
    private readonly HashSet<EntityId> _batch = new();
    private Vector2 _previousView, _goal;
    private bool _hasPreviousView, _hasGoal, _boxSelecting;
    private bool _mapPointerInside;
    public void SetMapPointerInside(bool inside) { _mapPointerInside=inside; if(!inside && IsOpen)_hovered=default; }
    private bool _showGlyphs;
    private float _glyphAlpha;
    private readonly HashSet<EntityId> _visibleGlyphs = new();
    public bool HasTurretGlyph(EntityId id) => _visibleGlyphs.Contains(id);
    private Vector2 _overviewDragOffset;
    private Rect _defaultViewport;
    private float _viewAspect;
    private TacticalMapMath.CameraFrame Frame => TacticalMapMath.FrameCamera(WorldBounds, _size, FullAspect, _defaultViewport);
    private float FullAspect => Screen.width / (float)Mathf.Max(1, Screen.height) * _defaultViewport.width / Mathf.Max(.001f, _defaultViewport.height);
    public bool IsInMapViewport(Vector2 screen) => OutputCamera != null && OutputCamera.pixelRect.Contains(screen);
    private MatchId _match;
    private void Awake()
    {
        if (Source == null) Source = GetComponent<InitialStagePlayerHUD>();
        if (Source == null || Style == null || Style.ViewPrefab == null) { enabled = false; return; }
        View = Instantiate(Style.ViewPrefab, transform, false); View.name = "Tactical HUD (Local)"; View.Bind(this);
        var viewRect = (RectTransform)View.transform;
        viewRect.pivot = new Vector2(.5f, .5f);
        viewRect.anchorMin = Vector2.zero; viewRect.anchorMax = Vector2.one;
        viewRect.offsetMin = viewRect.offsetMax = Vector2.zero; viewRect.localScale = Vector3.one;
        View.ShowCommand(false); View.Tooltip.gameObject.SetActive(false); View.Prompt.gameObject.SetActive(false); View.Goal.gameObject.SetActive(false);
        // 카메라 월드 marker는 Canvas 배율/해상도 변화에 영향을 받지 않습니다.
        _rig = new GameObject("Tactical camera target").transform;
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(_rig.gameObject, gameObject.scene);
        View.ToggleMap.onClick.AddListener(Toggle); View.Close.onClick.AddListener(Close);
        View.ToggleTurret.onClick.AddListener(ToggleSelected); View.Upgrade.onClick.AddListener(ToggleUpgrade);
        View.LevelUp.onClick.AddListener(() => ChangeLevel(true)); View.LevelDown.onClick.AddListener(() => ChangeLevel(false));
        View.LowPlus.onClick.AddListener(() => ChangeBranch(true, true)); View.LowMinus.onClick.AddListener(() => ChangeBranch(true, false));
        View.FirePlus.onClick.AddListener(() => ChangeBranch(false, true)); View.FireMinus.onClick.AddListener(() => ChangeBranch(false, false));
        View.AutoActivate.onClick.AddListener(AutoActivate); View.BatchActivate.onClick.AddListener(() => BatchActivation(true));
        View.BatchDeactivate.onClick.AddListener(() => BatchActivation(false));
        View.SelectMultiple.onClick.AddListener(() => { MultipleSelection = !MultipleSelection; RefreshDock(); EventSystem.current?.SetSelectedGameObject(null); });
        View.FocusPlayer.onClick.AddListener(() => Focus(PlayerPosition));
        View.FocusCU.onClick.AddListener(() => { if (TryControlPosition(out var p)) Focus(p); });
        View.Back.onClick.AddListener(PreviousView);
        for (int i = 0; i < View.Incidents.Length; i++) { int index = i; View.Incidents[i].onClick.AddListener(() => FocusIncident(index)); }
        Source.TurretCommandCompleted += OnCommandResult;
    }
    private void Update()
    {
        if (View == null) return;
        if (_app == null) _app = FindFirstObjectByType<AppRoot>();
        var player = Source.LocalPlayer;
        if (_player != player || _match != Source.BoundMatchId)
        {
            Close(); if (_attack != null) _attack.AttackPerformed -= OnAttack;
            _player = player; _match = Source.BoundMatchId; _control = player != null ? player.GetComponent<PlayerControlState>() : null;
            _attack = player != null ? player.GetComponent<PlayerAttack>() : null; if (_attack != null) _attack.AttackPerformed += OnAttack;
            _selected = default; _hasGoal = false; _damage.Clear(); _nextSample = 0;
        }
        bool field = Source.HasFieldActor && _player != null && _player.curHp > 0;
        bool available = field && _control != null && _control.CanIssueTurretCommands &&
            !(_app != null && _app.IsInitialized && _app.LocalInput.IsBlocked);
        View.ToggleMap.gameObject.SetActive(available && !IsOpen);
        if (IsOpen && (!available || _camera == null || _camera.ActiveTarget != _rig)) Close();
        if (available && GameInput.WasPressedThisFrame(GameKey.M)) Toggle();
        if (IsOpen && UIEscapeInput.TryConsume()) Close();
        if (Time.unscaledTime >= _nextSample)
        {
            _nextSample = Time.unscaledTime + .15f; CaptureData(); RefreshDock();
        }
        if (IsOpen)
        {
            var hovered=_mapPointerInside && IsInMapViewport(GameInput.PointerPosition)?Pick(GameInput.PointerPosition,34):null;
            _hovered=hovered!=null?hovered.EntityId:default;
            Vector2 pan = GameInput.Move * (_size * Style.PanSpeed * Time.unscaledDeltaTime);
            if (Mouse.current?.middleButton.isPressed == true && IsInMapViewport(GameInput.PointerPosition))
                pan -= Mouse.current.delta.ReadValue() * (_size * 2 / Mathf.Max(1, Screen.height));
            if (pan.sqrMagnitude > 0)
            {
                if (!_axisPanning) BeginCameraPan();
                Pan(pan);
            }
            _axisPanning = pan.sqrMagnitude > 0;
            ApplyZoomBounds();
            if (GameInput.WasPressedThisFrame(GameKey.Return) && EventSystem.current?.currentSelectedGameObject == null) ToggleSelected();
            if (GameInput.WasPressedThisFrame(GameKey.U)) ToggleUpgrade();
            if (GameInput.WasPressedThisFrame(GameKey.Home)) Focus(PlayerPosition);
            View.Tooltip.gameObject.SetActive(false); View.Prompt.gameObject.SetActive(false);
        }
        else if (available) RefreshHover();
        else { View.Tooltip.gameObject.SetActive(false); View.Prompt.gameObject.SetActive(false); }
        RefreshGoal(field);
    }
    public void Toggle() { if (IsOpen) Close(); else Open(); }
    public bool Open()
    {
        if (IsOpen || Source == null || !Source.HasFieldActor || _control == null || !_control.CanIssueTurretCommands ||
            _player.curHp <= 0 || (_app != null && _app.IsInitialized && _app.LocalInput.IsBlocked)) return false;
        var skills = _player.GetComponent<PlayerSkillController>(); if (skills != null && skills.IsPlacingSkill) return false;
        var status=Source.GetComponent<PlayerStatusPanel>(); if(status!=null && status.IsOpen)return false;
        _camera = CameraManager.Current; if (_camera == null || _camera.HasOverride) return false;
        skills?.CloseSelection(); CaptureData(); UpdateBounds();
        _baseSize = _camera.OutputCamera.orthographicSize;
        _defaultViewport = _camera.OutputCamera.rect;
        _size = Mathf.Clamp(TacticalMapMath.FitAround(PlayerPosition, OccupiedBounds(), _camera.OutputCamera.aspect, _baseSize * Style.EntryZoom) * 1.08f,
            _baseSize * Style.MinimumZoom, _baseSize * Style.MaximumZoom);
        _size = TacticalMapMath.ClampViewSize(_size, WorldBounds, FullAspect, _baseSize * Style.MinimumZoom);
        var frame = Frame; _viewAspect = frame.Aspect;
        _rig.position = TacticalMapMath.ClampViewCenter(PlayerPosition, WorldBounds, frame.Size, frame.Aspect); _rig.rotation = Quaternion.identity;
        ResetCameraMotion();
        _cameraLease = _camera.PushView(_rig, 0, frame.Size, frame.Viewport); _controlLease = _control.EnterTacticalView();
        _player.GetComponent<PlayerMovement>()?.StopImmediately(); _player.GetComponent<PlayerInputHandler>()?.CancelAttackGesture();
        IsOpen = true; MultipleSelection = false; View.ShowCommand(true); View.CommandGroup.alpha = 1;
        View.UpgradeDrawer.gameObject.SetActive(false); View.SelectionBox.gameObject.SetActive(false);
        Cursor.visible = true; EventSystem.current?.SetSelectedGameObject(null); RefreshDock(); return true;
    }
    public void Close()
    {
        if (!IsOpen && _cameraLease == null && _controlLease == null) return;
        IsOpen = false; _cameraLease?.Dispose(); _cameraLease = null; _controlLease?.Dispose(); _controlLease = null;
        _batch.Clear(); _boxSelecting = false; MultipleSelection = false;
        _mapPointerInside=false; _hovered=default;
        _axisPanning=false;
        if (View != null) { View.ShowCommand(false); View.SelectionBox.gameObject.SetActive(false); }
        if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
    }
    public void Zoom(float wheel)
    {
        if (!IsOpen || Mathf.Approximately(wheel, 0)) return;
        _zoomFrom = _size; _zoomAt = Time.unscaledTime;
        _zoomGoal = TacticalMapMath.ClampViewSize(Mathf.Clamp(_zoomGoal / Mathf.Pow(Style.ZoomStep, Mathf.Sign(wheel)),
            _baseSize * Style.MinimumZoom, _baseSize * Style.MaximumZoom), WorldBounds, FullAspect, _baseSize * Style.MinimumZoom);
        // 가장자리에서 zoom-out하면 필요한 중심도 바뀝니다. 그 중심을 즉시 바꾸지 않고 실제 현재 위치에서 함께 보간합니다.
        _cameraFrom = CameraPosition; _cameraGoal = ClampCameraGoal(_cameraGoal); _cameraMoveAt = Time.unscaledTime;
    }
    private void ApplyZoomBounds()
    {
        UpdateBounds();
        _zoomGoal = TacticalMapMath.ClampViewSize(_zoomGoal, WorldBounds, FullAspect, _baseSize * Style.MinimumZoom);
        _size = TacticalMapMath.ClampViewSize(Mathf.Lerp(_zoomFrom, _zoomGoal,
            TacticalMapMath.SmoothProgress(Time.unscaledTime - _zoomAt, Style.CameraZoomSeconds)), WorldBounds, FullAspect, _baseSize * Style.MinimumZoom);
        var frame = Frame; _viewAspect = frame.Aspect;
        _cameraGoal = ClampCameraGoal(_cameraGoal);
        // 보간 중의 실제 viewport도 매 프레임 제한합니다. endpoint만 제한하면 zoom-out 중 경계를 넘을 수 있습니다.
        Vector2 position = Vector2.Lerp(_cameraFrom, _cameraGoal,
            TacticalMapMath.SmoothProgress(Time.unscaledTime - _cameraMoveAt, Style.CameraMoveSeconds));
        _rig.position = TacticalMapMath.ClampViewCenter(position, WorldBounds, frame.Size, frame.Aspect);
        _camera.TrySetViewSize(_rig, frame.Size); _camera.TrySetViewViewport(_rig, frame.Viewport);
    }
    private void Pan(Vector2 delta)
    {
        if (_rig == null) return;
        _cameraGoal = ClampCameraGoal(_cameraGoal + delta);
    }
    private void Focus(Vector2 position)
    {
        if (!IsOpen) return;
        _previousView = CameraPosition; _hasPreviousView = true; MoveCameraTo(position);
        EventSystem.current?.SetSelectedGameObject(null);
    }
    private void PreviousView()
    {
        if (!_hasPreviousView) return;
        var previous = _previousView; _previousView = CameraPosition; MoveCameraTo(previous);
        EventSystem.current?.SetSelectedGameObject(null);
    }
    public void PointerClick(Vector2 screen, bool overview, bool secondary)
    {
        if (!IsOpen) return;
        if (!overview && !IsInMapViewport(screen)) return;
        Vector2 position = overview ? OverviewToWorld(screen) : ScreenToWorld(screen);
        if (secondary) { _goal = position; _hasGoal = true; return; }
        if (overview) { Focus(position); return; }
        var target = Pick(screen, 34);
        if (target == null) { _selected = default; _batch.Clear(); RefreshDock(); return; }
        Select(target.EntityId);
    }
    public void PointerDrag(Vector2 start, Vector2 screen, Vector2 delta, bool overview, bool left)
    {
        if (!IsOpen) return;
        if (overview) { _cameraGoal = ClampCameraGoal(OverviewToWorld(screen) + _overviewDragOffset); return; }
        if (left && Source.IsAtControlUnit && (MultipleSelection || GameInput.IsPressed(GameKey.LeftShift) || GameInput.IsPressed(GameKey.RightShift)))
        {
            _boxSelecting = true; var a = CanvasPoint(start); var b = CanvasPoint(ClampMapPoint(screen));
            View.SelectionBox.gameObject.SetActive(true); View.SelectionBox.anchoredPosition = (a + b) * .5f;
            View.SelectionBox.sizeDelta = new Vector2(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y));
        }
    }
    public void BeginPointerDrag(Vector2 start, bool overview)
    {
        if (!IsOpen || !overview) return;
        Vector2 point = OverviewToWorld(start);
        _overviewDragOffset = CurrentCameraRect.Contains(point) ? CameraPosition - point : Vector2.zero;
        BeginCameraPan();
    }
    public void EndPointerDrag(Vector2 start, Vector2 end, bool overview)
    {
        if (!_boxSelecting || overview || !IsOpen) return;
        _boxSelecting = false; View.SelectionBox.gameObject.SetActive(false); _batch.Clear();
        end = ClampMapPoint(end);
        Rect box = Rect.MinMaxRect(Mathf.Min(start.x, end.x), Mathf.Min(start.y, end.y), Mathf.Max(start.x, end.x), Mathf.Max(start.y, end.y));
        foreach (var row in _rows) if (box.Contains(OutputCamera.WorldToScreenPoint(Point(row))) && CanOperate(row)) _batch.Add(row.EntityId);
        if (_batch.Count > 0) foreach (var id in _batch) { _selected = id; break; }
        RefreshDock();
    }
    private Vector2 ClampMapPoint(Vector2 p)
    { Rect r = OutputCamera.pixelRect; return new Vector2(Mathf.Clamp(p.x,r.xMin,r.xMax),Mathf.Clamp(p.y,r.yMin,r.yMax)); }
    public bool Select(EntityId id)
    {
        if (!IsOpen || !_byId.ContainsKey(id)) return false;
        if (Source.IsAtControlUnit && (MultipleSelection || GameInput.IsPressed(GameKey.LeftShift) || GameInput.IsPressed(GameKey.RightShift)))
        {
            if (_batch.Add(id)) _selected = id;
            else
            {
                _batch.Remove(id); _selected = default;
                foreach (var remaining in _batch) { _selected = remaining; break; }
            }
        }
        else { _selected = id; _batch.Clear(); _batch.Add(id); }
        View.UpgradeDrawer.gameObject.SetActive(false); EventSystem.current?.SetSelectedGameObject(null); RefreshDock(); return true;
    }
    private Vector2 OverviewToWorld(Vector2 screen)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(View.Overview, screen, null, out var p);
        return TacticalMapMath.Unproject(p, WorldBounds, View.Overview.rect);
    }
    public Vector2 ScreenToWorld(Vector2 screen)
    {
        var camera = OutputCamera; if (camera == null) return Vector2.zero;
        Ray ray = camera.ScreenPointToRay(screen);
        return new Plane(Vector3.forward, Vector3.zero).Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : Vector2.zero;
    }
    public Vector2 CanvasPoint(Vector2 screen)
    { RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)View.transform, screen, null, out var point); return point; }
    public Vector2 WorldCanvasPoint(Vector2 screen)
    { RectTransformUtility.ScreenPointToLocalPointInRectangle(View.WorldClip, screen, null, out var point); return point; }
    private void OnAttack() { _lastShot = Time.unscaledTime; if (View != null) View.Tooltip.gameObject.SetActive(false); }
    private void OnCommandResult(TurretCommandResult result)
    {
        StageTurretAlertFeedback.Show(StageTurretUIText.Command(result)); _nextSample = 0;
    }
    private void OnDisable()
    {
        Close(); if (View != null) View.gameObject.SetActive(false);
    }
    private void OnEnable() { if (View != null) View.gameObject.SetActive(true); }
    private void OnDestroy()
    {
        Close(); if (_attack != null) _attack.AttackPerformed -= OnAttack;
        if (Source != null) Source.TurretCommandCompleted -= OnCommandResult;
        if (_regionTexture != null) Destroy(_regionTexture);
        if (_rig != null) Destroy(_rig.gameObject);
    }
}
