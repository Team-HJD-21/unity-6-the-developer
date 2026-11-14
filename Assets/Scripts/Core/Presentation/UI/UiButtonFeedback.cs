using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    [RequireComponent(typeof(Button))]
    public sealed class UiButtonFeedback : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
    {
        public TMP_Text Label;
        public Image Edge;
        public Graphic Icon;
        public Sprite IdleEdge, HoverEdge;
        public CanvasGroup FocusMark;
        public bool PreserveLabelColor;
        public Color Text = new(.933f, .918f, .859f), Accent = new(.839f, .749f, .486f);
        public Color Disabled = new(.45f, .49f, .47f);
        [Min(.01f)] public float HoverSeconds = .12f;
        private Button _button;
        private bool _hover, _selected;
        private float _amount;
        private void Awake() => _button = GetComponent<Button>();
        public void OnPointerEnter(PointerEventData data) => _hover = true;
        public void OnPointerExit(PointerEventData data) => _hover = false;
        public void OnSelect(BaseEventData data) => _selected = true;
        public void OnDeselect(BaseEventData data) => _selected = false;
        private void Update()
        {
            bool active = _button.IsInteractable() && (_hover || _selected);
            _amount = Mathf.MoveTowards(_amount, active ? 1 : 0, Time.unscaledDeltaTime / HoverSeconds);
            Apply(UiMotion.Sigmoid(_amount));
            if (Edge != null && IdleEdge != null) Edge.sprite = active && HoverEdge != null ? HoverEdge : IdleEdge;
        }
        private void Apply(float value)
        {
            if (Label != null && !PreserveLabelColor) Label.color = _button != null && !_button.IsInteractable() ? Disabled : Color.Lerp(Text, Accent, value);
            if (Icon != null) Icon.color = _button != null && !_button.IsInteractable() ? Disabled : PreserveLabelColor && Label != null ? Label.color : Color.Lerp(Text,Accent,value);
            if (FocusMark != null) FocusMark.alpha = value;
        }
        private void OnDisable() { _hover = _selected = false; _amount = 0; Apply(0); }
    }
}
