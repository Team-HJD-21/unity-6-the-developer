using System;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamHJD.Game.Presentation
{
    /// <summary>크기의 미리보기와 확정/취소를 분리하고 해당 화면의 정상 입력 수명만 닫습니다.</summary>
    public sealed class SettingsScreenPresenter : IScreenPresenter
    {
        private readonly AppSettingsView _view;
        private readonly UiDisplaySettings _display;
        private readonly Action _close, _released;
        private readonly float _before;
        private readonly GameAudioSettings _audio;
        private readonly AudioVolumeState _audioBefore;
        private GameObject _previousSelection;
        private bool _accepted, _disposed;
        public SettingsScreenPresenter(Transform parent, AppSettingsView prefab, UiDisplaySettings display, Action close, Action released, GameAudioSettings audio = null)
        {
            _display = display ?? throw new ArgumentNullException(nameof(display));
            _close = close ?? throw new ArgumentNullException(nameof(close)); _released = released ?? throw new ArgumentNullException(nameof(released));
            _before = display.HudScale;
            _audio = audio ?? new GameAudioSettings(); _audioBefore = _audio.Values;
            _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _view = UnityEngine.Object.Instantiate(prefab, parent, false); _view.name = "Screen:Settings";
            _view.ShowScale(_before);
            _view.ShowAudio(_audioBefore);
            _view.HudScale.onValueChanged.AddListener(Preview);
            if (_view.HasAudioControls)
            {
                _view.MasterVolume.onValueChanged.AddListener(PreviewAudio);
                _view.MusicVolume.onValueChanged.AddListener(PreviewAudio);
                _view.EffectsVolume.onValueChanged.AddListener(PreviewAudio);
            }
            _view.Defaults.onClick.AddListener(Defaults); _view.Back.onClick.AddListener(Cancel); _view.Apply.onClick.AddListener(Apply);
        }
        private void Preview(float percent) { _display.SetHudScale(percent / 100); _view.ShowScale(_display.HudScale); }
        private void PreviewAudio(float ignored)
        {
            _audio.Set(_view.MasterVolume.value / 100, _view.MusicVolume.value / 100, _view.EffectsVolume.value / 100);
            _view.ShowAudio(_audio.Values);
        }
        private void Defaults()
        { Preview(UiDisplaySettings.DefaultScale * 100); _audio.Set(1, 1, 1); _view.ShowAudio(_audio.Values); }
        private void Cancel() => _close();
        private void Apply() { _accepted = true; _display.Save(); _audio.Save(); _close(); }
        public void SetFocused(bool focused)
        {
            if (_disposed) return;
            _view.SetFocused(focused);
            if (focused)
            {
                AppScreenPresenter.EnsureEventSystem();
                EventSystem.current.SetSelectedGameObject(_view.HudScale.gameObject);
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _view.HudScale.onValueChanged.RemoveListener(Preview);
            _view.Defaults.onClick.RemoveListener(Defaults); _view.Back.onClick.RemoveListener(Cancel); _view.Apply.onClick.RemoveListener(Apply);
            if (_view.HasAudioControls)
            {
                _view.MasterVolume.onValueChanged.RemoveListener(PreviewAudio);
                _view.MusicVolume.onValueChanged.RemoveListener(PreviewAudio);
                _view.EffectsVolume.onValueChanged.RemoveListener(PreviewAudio);
            }
            if (!_accepted) { _display.SetHudScale(_before); _audio.Restore(_audioBefore); }
            if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_view.transform))
                EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            _released(); _view.CloseAndDestroy();
        }
    }
}
