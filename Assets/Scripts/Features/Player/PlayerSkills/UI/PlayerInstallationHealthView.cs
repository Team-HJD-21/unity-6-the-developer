using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>커서가 가리키는 설치물의 실제 체력만 표시하는 비상호작용 HUD입니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerInstallationHealthView : MonoBehaviour
{
    [SerializeField] private Canvas _canvas;
    [SerializeField] private RectTransform _panel;
    [SerializeField] private Image _fill;
    [SerializeField] private TMP_Text _health;
    public bool IsConfigured => _canvas != null && _panel != null && _fill != null && _health != null;
    public bool IsVisible => IsConfigured && _panel.gameObject.activeSelf;
    public string HealthText => _health != null ? _health.text : "";
    public float HealthRatio => _fill != null ? _fill.fillAmount : 0;
    private void Awake() => Hide();
    public void Hide() { if (_panel != null) _panel.gameObject.SetActive(false); }
    public void Show(Camera camera, Vector3 anchor, float health, float maximum)
    {
        if (!IsConfigured || camera == null || health <= 0 || maximum <= 0) { Hide(); return; }
        Vector3 screen = camera.WorldToScreenPoint(anchor);
        if (screen.z <= 0 || !RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)_canvas.transform, screen, null, out var point)) { Hide(); return; }
        _panel.anchoredPosition = point;
        _fill.fillAmount = Mathf.Clamp01(health / maximum);
        _health.text = $"{Mathf.CeilToInt(health - .0001f)} / {Mathf.CeilToInt(maximum)}";
        _panel.gameObject.SetActive(true);
    }
}
