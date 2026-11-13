using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    /// <summary>영구 App 화면 Canvas의 제작 참조입니다. GameObject를 runtime에 조립하지 않습니다.</summary>
    public sealed class AppScreenCanvasView : MonoBehaviour
    {
        public RectTransform Screens;
        public CanvasGroup TravelFade;
        public AlertStripView Alerts;
        public bool IsConfigured => Screens != null && TravelFade != null && Alerts != null && Alerts.IsConfigured;
    }
}
