using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>씬 HUD 프리팹에 배치한 R 키 안내를 가까운 필드 터렛 아래에 표시합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerSkillHud))]
public sealed class PlayerTurretRepairPromptView : MonoBehaviour
{
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Button _button;
    [SerializeField] private TMP_Text _label;
    [SerializeField] private Image _border;
    [SerializeField] private Image _progressFill;
    [SerializeField] private Vector2 _screenOffset = new(0f, -22f);
    private PlayerSkillHud _hud;
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

    private void OnEnable() => Canvas.preWillRenderCanvases += RefreshPosition;
    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= RefreshPosition;
        if (_panel != null) _panel.gameObject.SetActive(false);
    }

    private void LateUpdate()
    {
        if (!IsConfigured) return;
        _repair = _hud != null && _hud.Owner != null ? _hud.Owner.GetComponent<PlayerTurretRepair>() : null;
        var turret = _repair != null && _repair.isActiveAndEnabled ? _repair.Target : null;
        Transform target = turret != null ? turret.transform : null;
        if (_target != target)
        {
            _target = target;
            _baseRenderer = target != null ? target.GetComponent<SpriteRenderer>() : null;
        }
        _worldCamera = Camera.main;
        bool visible = target != null && _worldCamera != null && !_hud.IsOpen &&
            (turret.CurrentHealth < turret.MaxHealth || _repair.IsRepairing);
        _panel.gameObject.SetActive(visible);
        if (!visible) return;

        _button.interactable = _repair.CanRepair;
        _border.color = _repair.IsRepairing ? new Color(.35f, 1f, .65f) :
            _repair.CanRepair ? new Color(.35f, .95f, 1f) : new Color(.5f, .55f, .6f);
        bool restoring = _repair.IsRestoring;
        float progress = _repair.Progress;
        _progressFill.rectTransform.anchorMax = new Vector2(progress, 1f);
        _progressFill.color = restoring ? new Color(1f, .72f, .3f) : new Color(.35f, 1f, .65f);
        if (_repair.IsRepairing)
            _label.SetText(restoring ? "복구 중 {0}%" : "수리 중 {0}%", Mathf.FloorToInt(100f * progress));
        else _label.text = _repair.RequiresRelease ? "R 다시 누르기" : restoring ? "누르고 복구" : "누르고 수리";
        RefreshPosition();
    }

    // 카메라 추적 LateUpdate 이후 최종 화면 좌표로 고정합니다.
    private void RefreshPosition()
    {
        if (_panel == null || !_panel.gameObject.activeSelf || _target == null || _worldCamera == null) return;
        Vector3 bottom = _target.position;
        if (_baseRenderer != null) bottom.y = _baseRenderer.bounds.min.y;
        Vector3 screen = _worldCamera.WorldToScreenPoint(bottom);
        if (screen.z <= 0f) { _panel.gameObject.SetActive(false); return; }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)transform, screen, null, out var point))
            _panel.anchoredPosition = point + _screenOffset;
    }
}
