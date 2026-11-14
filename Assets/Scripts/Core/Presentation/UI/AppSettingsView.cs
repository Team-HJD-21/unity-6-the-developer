using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    public sealed class AppSettingsView : AppPanelView
    {
        public Slider HudScale;
        public TMP_Text ScaleValue;
        public RectTransform Preview;
        public Button Defaults, Back, Apply;
        public Slider MasterVolume, MusicVolume, EffectsVolume;
        public TMP_Text MasterValue, MusicValue, EffectsValue;
        public bool HasAudioControls => MasterVolume != null && MusicVolume != null && EffectsVolume != null;
        public void ShowAudio(AudioVolumeState values)
        {
            if (!HasAudioControls) return;
            ShowVolume(MasterVolume, MasterValue, values.Master);
            ShowVolume(MusicVolume, MusicValue, values.Music);
            ShowVolume(EffectsVolume, EffectsValue, values.Effects);
        }
        private static void ShowVolume(Slider slider, TMP_Text label, float value)
        { slider.SetValueWithoutNotify(Mathf.RoundToInt(value * 100)); if (label != null) label.text = $"{Mathf.RoundToInt(value * 100)}%"; }
        public void ShowScale(float value)
        {
            HudScale.SetValueWithoutNotify(Mathf.RoundToInt(value * 100));
            ScaleValue.text = $"{Mathf.RoundToInt(value * 100)}%";
            Preview.localScale = Vector3.one * value;
        }
    }
}
