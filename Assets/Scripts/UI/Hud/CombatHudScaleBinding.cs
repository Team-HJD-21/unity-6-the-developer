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
    }
    public Element[] Elements;
    [Min(0)] public float MinimumScreenScale;
    [Min(1)] public float MinimumScreenWidth = 800;
    private UiDisplaySettings _settings;
    private Canvas _canvas;
    private float _screenFloor;
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
        _screenFloor = ScreenFloor();
        foreach (var element in Elements)
        {
            if (element.Target == null) continue;
            float amount = Mathf.Max(element.MinimumScale, scale, _screenFloor);
            element.Target.localScale = Vector3.one * amount;
            if (element.ScaleOffset) element.Target.anchoredPosition = element.BaseOffset * amount;
        }
    }
    private float ScreenFloor()
    {
        if (MinimumScreenScale <= 0) return 0;
        if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
        float factor = _canvas != null ? _canvas.scaleFactor : 1;
        // 작은 창의 명령 가독성을 유지하되 800px보다 좁으면 같은 배치 비율로 줄입니다.
        return MinimumScreenScale * Mathf.Min(1, Screen.width / Mathf.Max(1, MinimumScreenWidth)) / Mathf.Max(.001f, factor);
    }
    private void LateUpdate()
    {
        if (MinimumScreenScale > 0 && !Mathf.Approximately(_screenFloor, ScreenFloor())) Apply(CurrentScale);
    }
    private void OnDisable() { if (_settings != null) _settings.Changed -= Apply; }
}
