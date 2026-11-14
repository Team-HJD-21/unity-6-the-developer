// 운영 메뉴/로딩/결과 화면이 기존 프로젝트의 폰트와 UI sprite를 재사용하도록 묶습니다.
// 화면 수명과 입력 규칙은 ScreenManager가 소유하며 이 Asset은 표현 값만 제공합니다.
using UnityEngine;

namespace TeamHJD.Game.Presentation
{
    [CreateAssetMenu(menuName = "The Developer/Core/App Screen Style")]
    public sealed class AppScreenStyle : ScriptableObject
    {
        [SerializeField] private Font font;
        [SerializeField] private Sprite panelSprite;
        [SerializeField] private Sprite buttonSprite;
        [SerializeField] private Color textColor = Color.white;
        [SerializeField] private Color titleColor = Color.white;
        public Font Font => font;
        public Sprite PanelSprite => panelSprite;
        public Sprite ButtonSprite => buttonSprite;
        public Color TextColor => textColor;
        public Color TitleColor => titleColor;
        [Header("Authored common UI prefabs")]
        public AppScreenCanvasView CanvasPrefab;
        public AppPanelView MenuPrefab, ResultPrefab, RecoveryPrefab;
        public AppSettingsView SettingsPrefab;
        public AppLoadingView LoadingPrefab;
        public bool HasAuthoredViews => CanvasPrefab != null && CanvasPrefab.IsConfigured && MenuPrefab != null &&
            MenuPrefab.IsConfigured && ResultPrefab != null && ResultPrefab.IsConfigured && RecoveryPrefab != null &&
            RecoveryPrefab.IsConfigured && SettingsPrefab != null && LoadingPrefab != null;
    }
}
