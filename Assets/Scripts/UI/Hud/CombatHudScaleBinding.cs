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
    private UiDisplaySettings _settings;
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
        foreach (var element in Elements)
        {
            if (element.Target == null) continue;
            float amount = Mathf.Max(element.MinimumScale, scale);
            element.Target.localScale = Vector3.one * amount;
            if (element.ScaleOffset) element.Target.anchoredPosition = element.BaseOffset * amount;
        }
    }
    private void OnDisable() { if (_settings != null) _settings.Changed -= Apply; }
}
