using System;
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    /// <summary>App이 소유하는 표시 설정입니다. 게임 데이터와 별개이며 Apply 때만 로컬에 저장합니다.</summary>
    public sealed class UiDisplaySettings
    {
        public const float MinimumScale = .75f, MaximumScale = 1.1f, DefaultScale = .85f;
        private const string PreferenceKey = "CBC.UI.HudScale";
        public float HudScale { get; private set; }
        public event Action<float> Changed;
        public UiDisplaySettings(bool loadSaved = true)
            => HudScale = Sanitize(loadSaved ? PlayerPrefs.GetFloat(PreferenceKey, DefaultScale) : DefaultScale);
        public static float Sanitize(float value) => float.IsFinite(value)
            ? Mathf.Round(Mathf.Clamp(value, MinimumScale, MaximumScale) * 100) / 100f : DefaultScale;
        public void SetHudScale(float value)
        {
            value = Sanitize(value);
            if (Mathf.Approximately(value, HudScale)) return;
            HudScale = value; Changed?.Invoke(value);
        }
        public void Save() { PlayerPrefs.SetFloat(PreferenceKey, HudScale); PlayerPrefs.Save(); }
    }

    public static class UiMotion
    {
        public static float Sigmoid(float time, float slope = 10f)
        {
            time = Mathf.Clamp01(time); slope = Mathf.Max(.01f, slope);
            float start = 1f / (1f + Mathf.Exp(slope * .5f));
            float end = 1f / (1f + Mathf.Exp(-slope * .5f));
            return (1f / (1f + Mathf.Exp(-slope * (time - .5f))) - start) / (end - start);
        }
    }
}

namespace TeamHJD.Game.Presentation.Scene
{
    public interface IUiDisplaySettingsConsumer
    {
        void BindUiDisplaySettings(UiDisplaySettings settings);
    }
}
