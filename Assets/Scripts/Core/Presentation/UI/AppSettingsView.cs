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
        public void ShowScale(float value)
        {
            HudScale.SetValueWithoutNotify(Mathf.RoundToInt(value * 100));
            ScaleValue.text = $"{Mathf.RoundToInt(value * 100)}%";
            Preview.localScale = Vector3.one * value;
        }
    }
}
