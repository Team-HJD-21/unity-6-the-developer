using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

namespace TeamHJD.Game.Presentation
{
    public sealed class StartupControlsView : MonoBehaviour
    {
        public Button Start, Settings, Exit;
        public CanvasGroup Group;
        public RectTransform Content;
        public Animator IntroLogo;
        public TMPro.TMP_Text VersionLabel;
        [Min(30)] public int MenuFrameRate = 60;
        [Min(.01f)] public float RevealSeconds = .4f;
        public bool IsRevealed { get; private set; }
        private Coroutine _intro;
        private Vector2 _home;
        private int _previousFrameRate;
        private void Awake()
        {
            if (Content != null) _home = Content.anchoredPosition;
            if (VersionLabel != null) VersionLabel.text = "v" + UnityEngine.Application.version.TrimStart('v', 'V');
            Hide();
        }
        private void OnEnable()
        {
            _previousFrameRate = UnityEngine.Application.targetFrameRate; UnityEngine.Application.targetFrameRate = MenuFrameRate;
            Hide(); _intro = StartCoroutine(Reveal());
        }
        private void Hide()
        {
            IsRevealed = false;
            if (Group != null) { Group.alpha = 0; Group.interactable = Group.blocksRaycasts = false; }
        }
        private System.Collections.IEnumerator Reveal()
        {
            yield return null;
            // Follow the existing logo controller's settled idle states, without changing its animation.
            while (IntroLogo != null && IntroLogo.isActiveAndEnabled)
            {
                var state = IntroLogo.GetCurrentAnimatorStateInfo(0);
                if (!IntroLogo.IsInTransition(0) && (state.IsName("Logo Up") || state.IsName("Logo Down"))) break;
                yield return null;
            }
            float elapsed = 0;
            while (elapsed < RevealSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float amount = UiMotion.Sigmoid(Mathf.Clamp01(elapsed / RevealSeconds));
                if (Group != null) Group.alpha = amount;
                if (Content != null) Content.anchoredPosition = _home + Vector2.down * (12 * (1 - amount));
                yield return null;
            }
            if (Group != null) { Group.alpha = 1; Group.interactable = Group.blocksRaycasts = true; }
            if (Content != null) Content.anchoredPosition = _home;
            IsRevealed = true; _intro = null;
        }
        private void LateUpdate()
        {
            // Seed navigation after the UI module has processed the first intentional move/submit.
            // Revealing the menu alone must not look like a mouse hover.
            if (!IsRevealed || Start == null || !Start.IsInteractable()) return;
            if (Group != null && (!Group.interactable || !Group.blocksRaycasts)) return;
            var events = EventSystem.current;
            if (events == null || !events.isFocused || !events.sendNavigationEvents || events.currentSelectedGameObject != null) return;
            if (events.currentInputModule is not InputSystemUIInputModule input) return;
            if (input.move?.action?.WasPerformedThisFrame() != true && input.submit?.action?.WasPerformedThisFrame() != true) return;
            var canvas = Start.GetComponentInParent<Canvas>();
            if (canvas != null && !canvas.isActiveAndEnabled) return;
            events.SetSelectedGameObject(Start.gameObject);
        }
        private void OnDisable()
        {
            if (_intro != null) StopCoroutine(_intro); _intro = null; Hide();
            if (UnityEngine.Application.targetFrameRate == MenuFrameRate) UnityEngine.Application.targetFrameRate = _previousFrameRate;
        }
    }
}
