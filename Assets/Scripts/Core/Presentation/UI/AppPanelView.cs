using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    /// <summary>메뉴/결과/설정 프리팹의 표시와 중단 가능한 전환만 담당합니다.</summary>
    public class AppPanelView : MonoBehaviour
    {
        public CanvasGroup Group;
        public RectTransform Panel;
        public TMP_Text Title, Body, Status;
        public Button Primary, Secondary, Settings;
        [Min(.01f)] public float TransitionSeconds = .18f;
        [Min(0)] public float SecondaryRowHeight;
        private Vector2 _home;
        private Vector2 _fullSize, _statusHome;
        private float _value, _from, _target, _elapsed;
        private bool _initialized, _closing;
        private bool _secondarySet, _secondaryVisible;
        public bool IsConfigured => Group != null && Panel != null && Title != null && Primary != null;
        protected virtual void Awake() => EnsureInitialized();
        private void EnsureInitialized()
        {
            if (_initialized) return;
            _home = Panel.anchoredPosition; _fullSize = Panel.sizeDelta;
            if (Status != null) _statusHome = Status.rectTransform.anchoredPosition;
            _initialized = true;
        }
        public void SetSecondaryVisible(bool visible)
        {
            EnsureInitialized();
            if (_secondarySet && _secondaryVisible == visible) return;
            _secondarySet = true; _secondaryVisible = visible;
            Secondary.gameObject.SetActive(visible);
            float removed = visible ? 0 : SecondaryRowHeight;
            Panel.sizeDelta = _fullSize - Vector2.up * removed;
            if (Status != null) Status.rectTransform.anchoredPosition = _statusHome + Vector2.up * removed;
            if (Settings != null)
            {
                var primary = Primary.navigation; primary.selectOnUp = visible ? Secondary : Settings; Primary.navigation = primary;
                var settings = Settings.navigation; settings.selectOnDown = visible ? Secondary : Primary; Settings.navigation = settings;
            }
        }
        public void SetFocused(bool focused)
        {
            if (_closing) return;
            gameObject.SetActive(focused);
            if (!focused) return;
            EnsureInitialized();
            _from = _value = 0; _target = 1; _elapsed = 0;
            Group.alpha = 0; Group.blocksRaycasts = Group.interactable = true;
        }
        public void CloseAndDestroy()
        {
            if (_closing) return;
            _closing = true;
            Group.blocksRaycasts = Group.interactable = false;
            if (!gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            _from = _value; _target = 0; _elapsed = 0;
        }
        private void Update()
        {
            _elapsed = Mathf.Min(TransitionSeconds, _elapsed + Time.unscaledDeltaTime);
            _value = Mathf.LerpUnclamped(_from, _target, UiMotion.Sigmoid(_elapsed / Mathf.Max(.001f, TransitionSeconds)));
            Group.alpha = _value;
            Panel.anchoredPosition = _home + Vector2.down * (12 * (1 - _value));
            if (_closing && _elapsed >= TransitionSeconds) Destroy(gameObject);
        }
    }
}
