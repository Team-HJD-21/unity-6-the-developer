using UnityEngine;

/// <summary>회수 가능한 포탑 아래로 프리팹의 F 키 안내를 이동합니다.</summary>
public sealed class PlayerTurretRecoveryPromptView : MonoBehaviour
{
    [SerializeField] private Vector2 _screenOffset = new(0, -38);
    private PlayerSkillHud _hud;
    private Camera _worldCamera;
    public void Bind(PlayerSkillHud hud)
    {
        _hud = hud;
        _worldCamera = null;
        gameObject.SetActive(false);
    }
    public void Refresh()
    {
        var turret = _hud != null && _hud.Owner != null ? _hud.Owner.RecoverableTurret : null;
        if (_worldCamera == null) _worldCamera = Camera.main;
        bool visible = turret != null && _worldCamera != null && !_hud.IsOpen;
        gameObject.SetActive(visible);
        if (!visible) return;
        var baseRenderer = turret.GetComponent<SpriteRenderer>();
        Vector3 bottom = turret.position;
        if (baseRenderer != null) bottom.y = baseRenderer.bounds.min.y;
        Vector3 screen = _worldCamera.WorldToScreenPoint(bottom);
        if (screen.z <= 0) { gameObject.SetActive(false); return; }
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)_hud.transform, screen, null, out var point))
            ((RectTransform)transform).anchoredPosition = point + _screenOffset;
    }
}
