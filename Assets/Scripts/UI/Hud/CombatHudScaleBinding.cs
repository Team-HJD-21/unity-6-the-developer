using System;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>화면 전체 판정은 그대로 두고 제작된 HUD 파츠만 기준 anchor에서 확대/축소합니다.</summary>
public sealed class CombatHudScaleBinding : MonoBehaviour, IUiDisplaySettingsConsumer
{
    [Serializable] public struct Element
    {
        public RectTransform Target;
        public bool ScaleOffset;
        public Vector2 BaseOffset;
        [Range(0, 1)] public float MinimumScale;
        [Range(0, 1)] public float MinimumScreenScale;
    }
    public Element[] Elements;
    [Min(0)] public float MinimumScreenScale;
    [Min(1)] public float MinimumScreenWidth = 800;
    private UiDisplaySettings _settings;
    private Canvas _canvas;
    private float _screenFloor;
    private float _screenUnit;
    public float CurrentScale => _settings != null ? _settings.HudScale : UiDisplaySettings.DefaultScale;
    public void BindUiDisplaySettings(UiDisplaySettings settings)
    {
        if (_settings != null) _settings.Changed -= Apply;
        _settings = settings;
        if (isActiveAndEnabled && _settings != null) _settings.Changed += Apply;
        Apply(CurrentScale);
    }
    private void OnEnable()
    {
        if (_settings != null) _settings.Changed += Apply;
        Apply(CurrentScale);
    }
    private void Apply(float scale)
    {
        if (Elements == null) return;
        _screenUnit = ScreenFloor(1); _screenFloor = MinimumScreenScale * _screenUnit;
        foreach (var element in Elements)
        {
            if (element.Target == null) continue;
            float amount = Mathf.Max(element.MinimumScale, scale, _screenFloor);
            if (element.MinimumScreenScale > 0)
            {
                var root = _canvas != null ? _canvas.rootCanvas.transform : null;
                float inherited = root != null && element.Target.parent != null
                    ? Mathf.Abs(element.Target.parent.lossyScale.x / Mathf.Max(.001f, root.lossyScale.x)) : 1;
                amount = Mathf.Max(amount, element.MinimumScreenScale * _screenUnit / Mathf.Max(.001f, inherited));
            }
            element.Target.localScale = Vector3.one * amount;
            if (element.ScaleOffset) element.Target.anchoredPosition = element.BaseOffset * amount;
        }
    }
    private float ScreenFloor(float minimum)
    {
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        float factor = _canvas != null ? _canvas.rootCanvas.scaleFactor : 1;
        // 작은 창의 명령 가독성을 유지하되 800px보다 좁으면 같은 배치 비율로 줄입니다.
        return minimum * Mathf.Min(1, Screen.width / Mathf.Max(1, MinimumScreenWidth)) / Mathf.Max(.001f, factor);
    }
    private void LateUpdate()
    {
        if (Elements == null || Elements.Length == 0) return;
        if (!Mathf.Approximately(_screenUnit, ScreenFloor(1))) Apply(CurrentScale);
    }
    private void OnDisable() { if (_settings != null) _settings.Changed -= Apply; }
}
