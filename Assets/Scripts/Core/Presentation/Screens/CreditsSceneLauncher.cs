using System;
using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace TeamHJD.Game.Presentation
{
    /// <summary>메뉴와 Match를 보존한 채 프레젠테이션 전용 Scene을 소유합니다.</summary>
    [DefaultExecutionOrder(-600)]
    public sealed class CreditsSceneLauncher : MonoBehaviour
    {
        public const string ScenePath = "Assets/Scenes/Presentation/Credits.unity";
        public Button Button;
        public Canvas[] MenuCanvases;
        public CanvasGroup Transition;
        public MenuSoundtrack MenuMusic;
        [Min(.05f)] public float FadeSeconds = .4f;
        public bool IsOpen { get; private set; }
        private GameFlowController _flow;
        private SceneFlowController _scenes;
        private ScreenManager _screens;
        private IInputBlocker _input;
        private IDisposable _lease;
        private CancellationTokenSource _lifetime;
        private CreditsCinematicDirector _director;
        private bool[] _canvasStates;
        private GameObject _selection;
        private bool _busy, _ownsScene;
        private UnityEngine.SceneManagement.Scene _ownedScene;

        public void Bind(GameFlowController flow, SceneFlowController scenes, IInputBlocker input, ScreenManager screens)
        {
            Unbind(); _flow = flow; _scenes = scenes; _input = input; _screens = screens;
            _lifetime = new CancellationTokenSource();
            if (Button != null) Button.onClick.AddListener(Open);
        }
        private void Update()
        {
            if (!IsOpen || !UIEscapeInput.TryConsume()) return;
            if (!_busy) Close();
        }
        public async void Open()
        {
            if (IsOpen || _busy || _scenes == null || _flow == null || _flow.IsTransitioning ||
                _flow.Phase != GameFlowPhase.MainMenu || _screens?.Top != null ||
                SceneManager.GetSceneByPath(ScenePath).isLoaded) return;
            _busy = IsOpen = true;
            var token = _lifetime.Token;
            _selection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _lease = _input.Acquire("Credits cinematic");
            try
            {
                await Fade(1, token);
                if (MenuMusic != null) MenuMusic.Suspend();
                _canvasStates = new bool[MenuCanvases.Length];
                for (int i = 0; i < MenuCanvases.Length; i++)
                { if (MenuCanvases[i] != null) { _canvasStates[i] = MenuCanvases[i].enabled; MenuCanvases[i].enabled = false; } }
                await _scenes.LoadAsync(ScenePath, token);
                _ownsScene = true;
                token.ThrowIfCancellationRequested();
                var scene = SceneManager.GetSceneByPath(ScenePath);
                _ownedScene = scene;
                foreach (var root in scene.GetRootGameObjects())
                { _director = root.GetComponentInChildren<CreditsCinematicDirector>(true); if (_director != null) break; }
                if (_director == null) throw new InvalidOperationException("The Credits Scene needs its authored director.");
                _director.ExitRequested += Close;
                _director.SetMusicGain(0);
                _director.Play();
                await Fade(0, token);
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                Debug.LogException(error, this); await Recover();
                if (Transition != null) { Transition.alpha = 0; Transition.blocksRaycasts = false; }
                SetSoundtrackGain(1);
            }
            finally { _busy = false; }
        }
        public async void Close()
        {
            if (!IsOpen || _busy) return;
            _busy = true;
            try { await Fade(1, _lifetime.Token); await Recover(); await Fade(0, _lifetime.Token); }
            catch (OperationCanceledException) { }
            catch (Exception error) { Debug.LogException(error, this); }
            finally { _busy = false; }
        }
        private async Task Recover()
        {
            if (_director != null) { _director.ExitRequested -= Close; _director.Pause(); _director = null; }
            try
            {
                if (_ownsScene && SceneManager.GetSceneByPath(ScenePath).isLoaded)
                    await _scenes.UnloadAsync(ScenePath, CancellationToken.None);
            }
            finally
            {
                _ownsScene = false;
                if (_canvasStates != null)
                    for (int i = 0; i < MenuCanvases.Length; i++) if (MenuCanvases[i] != null) MenuCanvases[i].enabled = _canvasStates[i];
                _canvasStates = null; _lease?.Dispose(); _lease = null; IsOpen = false;
                if (MenuMusic != null) MenuMusic.Resume();
                if (EventSystem.current != null && _selection != null) EventSystem.current.SetSelectedGameObject(_selection);
            }
        }
        private async Task Fade(float target, CancellationToken token)
        {
            if (Transition == null) { SetSoundtrackGain(1 - target); return; }
            Transition.blocksRaycasts = true;
            float start = Transition.alpha, elapsed = 0;
            while (elapsed < FadeSeconds)
            {
                token.ThrowIfCancellationRequested(); elapsed += Time.unscaledDeltaTime;
                Transition.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0, 1, Mathf.Clamp01(elapsed / FadeSeconds)));
                SetSoundtrackGain(1 - Transition.alpha);
                await Task.Yield();
            }
            Transition.alpha = target; Transition.blocksRaycasts = target > 0;
            SetSoundtrackGain(1 - target);
        }
        private void SetSoundtrackGain(float gain)
        {
            if (MenuMusic != null) MenuMusic.SetGain(gain);
            if (_director != null) _director.SetMusicGain(gain);
        }
        public void Unbind()
        {
            if (Button != null) Button.onClick.RemoveListener(Open);
            _lifetime?.Cancel(); _lifetime?.Dispose(); _lifetime = null;
            if (_director != null) { _director.ExitRequested -= Close; _director.Pause(); }
            _director = null; _lease?.Dispose(); _lease = null;
            if (_ownsScene)
            {
                _ownsScene = false;
                _ = ReleaseScene(_scenes, _ownedScene);
            }
            if (_canvasStates != null)
                for (int i = 0; i < MenuCanvases.Length; i++) if (MenuCanvases[i] != null) MenuCanvases[i].enabled = _canvasStates[i];
            _canvasStates = null; IsOpen = _busy = false;
            if (Transition != null) { Transition.alpha = 0; Transition.blocksRaycasts = false; }
            if (MenuMusic != null) { MenuMusic.Resume(); MenuMusic.SetGain(1); }
        }
        private void OnDestroy() => Unbind();
        private static async Task ReleaseScene(SceneFlowController scenes, UnityEngine.SceneManagement.Scene owned)
        {
            try
            {
                while (scenes.IsTransitioning) await Task.Yield();
                if (owned.IsValid() && owned.isLoaded && SceneManager.GetSceneByPath(ScenePath).handle == owned.handle)
                    await scenes.UnloadAsync(ScenePath, CancellationToken.None);
            }
            catch (ObjectDisposedException) { }
        }
    }
}
