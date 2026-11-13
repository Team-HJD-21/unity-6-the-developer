using TMPro;
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    /// <summary>로딩 프리팹의 실제 진행 표시입니다. 알 수 없는 진행은 움직이는 구간으로 구분합니다.</summary>
    public sealed class AppLoadingView : MonoBehaviour
    {
        public RectTransform ProgressFill;
        public TMP_Text ProgressLabel;
        public void ShowProgress(float value)
        {
            if (float.IsFinite(value))
            {
                value = Mathf.Clamp01(value);
                ProgressFill.anchorMin = Vector2.zero; ProgressFill.anchorMax = new Vector2(value, 1);
                ProgressLabel.text = $"{Mathf.RoundToInt(value * 100)}%";
            }
            else
            {
                float position = Mathf.PingPong((float)(Time.realtimeSinceStartupAsDouble % 3) / 2, .75f);
                ProgressFill.anchorMin = new Vector2(position, 0); ProgressFill.anchorMax = new Vector2(position + .25f, 1);
                ProgressLabel.text = "";
            }
            ProgressFill.offsetMin = ProgressFill.offsetMax = Vector2.zero;
        }
    }
}
