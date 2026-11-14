using TMPro;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.UI;

/// <summary>씬 HUD 프리팹에 배치한 R 키 안내를 가까운 필드 터렛 중앙에 표시합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSkillHud))]
public sealed class PlayerTurretRepairPromptView : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image _border;
    [SerializeField] private Image _progressFill;
    [SerializeField] private Vector2 _screenOffset = Vector2.zero;
    private PlayerSkillHud _hud;
    private PlayerSkillController _owner;
    private PlayerTurretRepair _repair;
    private Transform _target;
    private SpriteRenderer _baseRenderer;
    private Camera _worldCamera;

    public bool IsConfigured => _panel != null && _button != null && _label != null && _border != null && _progressFill != null;

    private void Awake()
    {
        _hud = GetComponent<PlayerSkillHud>();
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += RefreshPosition;
        CameraManager.OutputChanged += OnCameraOutputChanged;
    }
    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= RefreshPosition;
        CameraManager.OutputChanged -= OnCameraOutputChanged;
        _owner = null;
        _repair = null;
        _worldCamera = null;
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!IsConfigured) return;
        var owner = _hud != null ? _hud.Owner : null;
        if (_owner != owner)
        {
            _owner = owner;
            _repair = owner != null ? owner.GetComponent<PlayerTurretRepair>() : null;
            _worldCamera = null;
        }
        Transform target = _repair != null && _repair.isActiveAndEnabled ? _repair.TargetTransform : null;
        if (_target != target)
        {
            _target = target;
            _baseRenderer = target != null ? target.GetComponent<SpriteRenderer>() : null;
            if (_baseRenderer == null && target != null)
                _baseRenderer = target.Find("Base")?.GetComponent<SpriteRenderer>();
        }
        // 화면 위치는 매 프레임 계산하고, 참조는 초기 연결·Player/카메라 전환 시에만 다시 찾습니다.
        if (target != null && (_worldCamera == null || !_worldCamera.isActiveAndEnabled ||
            !_worldCamera.CompareTag("MainCamera")))
            _worldCamera = CameraManager.Current != null ? CameraManager.Current.OutputCamera : Camera.main;
        bool visible = target != null && _worldCamera != null && !_hud.IsOpen &&
            (_repair.TargetNeedsRepair || _repair.IsRepairing);
        _panel.gameObject.SetActive(visible);
        if (!visible) return;

        _button.interactable = _repair.CanRepair;
        _border.color = _repair.IsRepairing ? new Color(.35f, 1f, .65f) :
            _repair.CanRepair ? new Color(.35f, .95f, 1f) : new Color(.5f, .55f, .6f);
        bool restoring = _repair.IsRestoring;
        float progress = _repair.Progress;
        _progressFill.rectTransform.anchorMax = new Vector2(progress, 1f);
        _progressFill.color = restoring ? new Color(1f, .72f, .3f) : new Color(.35f, 1f, .65f);
        _label.text = "R";
        RefreshPosition();
    }

    private void OnCameraOutputChanged(Camera camera) => _worldCamera = camera;

    // 카메라 추적 LateUpdate 이후 최종 화면 좌표로 고정합니다.
    private void RefreshPosition()
    {
        if (_panel == null || !_panel.gameObject.activeSelf || _target == null || _worldCamera == null) return;
        Vector3 center = _baseRenderer != null ? _baseRenderer.bounds.center : _target.position;
        Vector3 screen = _worldCamera.WorldToScreenPoint(center);
        if (screen.z <= 0f) { _panel.gameObject.SetActive(false); return; }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out var point))
            _panel.anchoredPosition = point + _screenOffset;
    }
}
