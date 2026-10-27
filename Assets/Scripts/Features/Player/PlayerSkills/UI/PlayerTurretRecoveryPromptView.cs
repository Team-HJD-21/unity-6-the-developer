using UnityEngine;

/// <summary>회수 가능한 포탑 아래로 프리팹의 F 키 안내를 이동합니다.</summary>
public sealed class PlayerTurretRecoveryPromptView : MonoBehaviour
{
    [SerializeField] private Vector2 _screenOffset = new(0, -22);
    private PlayerSkillHud _hud;
    private Camera _worldCamera;
    private Transform _turret;
    private SpriteRenderer _baseRenderer;
    private RectTransform _rect;

    private void OnEnable() => Canvas.preWillRenderCanvases += RefreshPosition;
    private void OnDisable() => Canvas.preWillRenderCanvases -= RefreshPosition;

    public void Bind(PlayerSkillHud hud)
    {
        _hud = hud;
        _worldCamera = null;
        _turret = null;
        _baseRenderer = null;
        gameObject.SetActive(false);
    }
    public void Refresh()
    {
        var turret = _hud != null && _hud.Owner != null ? _hud.Owner.RecoverableTurret : null;
        if (_turret != turret)
        {
            _turret = turret;
            _baseRenderer = turret != null ? turret.GetComponent<SpriteRenderer>() : null;
        }
        if (_worldCamera == null) _worldCamera = Camera.main;
        bool visible = turret != null && _worldCamera != null && !_hud.IsOpen;
        gameObject.SetActive(visible);
        if (!visible) return;
        RefreshPosition();
    }

    // 추적 카메라의 LateUpdate가 끝난 최종 좌표로 그리기 직전에 다시 정렬합니다.
    private void RefreshPosition()
    {
        if (!isActiveAndEnabled || _hud == null || _turret == null || _worldCamera == null) return;
        if (_rect == null) _rect = (RectTransform)transform;
        Vector3 bottom = _turret.position;
        if (_baseRenderer != null) bottom.y = _baseRenderer.bounds.min.y;
        Vector3 screen = _worldCamera.WorldToScreenPoint(bottom);
        if (screen.z <= 0) { gameObject.SetActive(false); return; }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_hud.transform, screen, null, out var point))
            _rect.anchoredPosition = point + _screenOffset;
    }
}
