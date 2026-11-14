// 운영 화면의 수명/입력/게임 흐름을 연결합니다. 외형과 계층은 제작된 프리팹이 소유합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TeamHJD.Game.Presentation
{
    public sealed class AppScreenPresenter : IDisposable
    {
        /// <summary>출격·재시작이 공통으로 사용하는 실제 Stage 1의 Build Settings 경로입니다.</summary>
        public const string StageScene = "Assets/Scenes/Gameplay/Stage_1.unity";
        public const string HubScene = "Assets/Scenes/Spaceship/SpaceShipGrayBox.unity";
        private readonly ScreenManager _screens;
        private readonly SceneFlowController _scenes;
        private readonly Func<string, Task> _replace;
        private readonly Func<string> _travelRestriction;
        private readonly Func<bool> _isGameFlowTransitioning;
        private readonly Func<bool> _canReturnToHub;
        private readonly Dictionary<long, IScreenPresenter> _views = new();
        private readonly AppScreenCanvasView _canvas;
        private readonly AppScreenStyle _style;
        private readonly SceneFadePresenter _sceneFade;
        private readonly UiDisplaySettings _display;
        private readonly GameAudioSettings _audio;
        private readonly AlertService _alerts;
        private readonly IDisposable _alertBinding;
        private readonly bool _ownsAlerts;
        private long _loadingId;
        private bool _disposed, _actionPending;
        private string _actionFailure;

        public AppScreenPresenter(Transform parent, ScreenManager screens, SceneFlowController scenes,
            Func<string, Task> replace, Func<string> travelRestriction, Func<bool> isGameFlowTransitioning = null,
            UiDisplaySettings display = null, Func<bool> canReturnToHub = null, AlertService alerts = null, GameAudioSettings audio = null)
        {
            _screens = screens ?? throw new ArgumentNullException(nameof(screens));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace));
            _travelRestriction = travelRestriction ?? throw new ArgumentNullException(nameof(travelRestriction));
            _isGameFlowTransitioning = isGameFlowTransitioning ?? (() => false);
            _canReturnToHub = canReturnToHub ?? (() => true);
            _display = display ?? new UiDisplaySettings();
            _audio = audio ?? new GameAudioSettings();
            _style = Resources.Load<AppScreenStyle>("Core/AppScreenStyle");
            if (_style == null || !_style.HasAuthoredViews)
                throw new InvalidOperationException("Configure the authored common UI prefabs in Core/AppScreenStyle.");
            _canvas = UnityEngine.Object.Instantiate(_style.CanvasPrefab, parent, false);
            _canvas.name = "App Screens";
            _screens.SetPresenterFactory(Create);
            _sceneFade = new SceneFadePresenter(_canvas.TravelFade);
            _alerts = alerts ?? new AlertService();
            _ownsAlerts = alerts == null;
            _alertBinding = GameAlerts.Bind(_alerts);
        }

        public Task FadeSceneAsync(Func<Task> move, CancellationToken cancellation)
        {
            _alerts.Tick(Time.unscaledTimeAsDouble, _canvas.Alerts.DisplaySeconds, suppressed: true);
            _canvas.Alerts.Hide();
            return _sceneFade.RunAsync(move, cancellation);
        }
        private IScreenPresenter Create(ScreenState state)
        {
            EnsureEventSystem();
            IScreenPresenter view;
            if (state.Kind == ScreenKind.Loading)
                view = new LoadingScreenPresenter(_canvas.Screens,
                    () => _scenes.IsTransitioning ? _scenes.Progress : float.NaN,
                    () => _views.Remove(state.Id), authored: _style.LoadingPrefab);
            else if (state.Kind == ScreenKind.Settings)
                view = new SettingsScreenPresenter(_canvas.Screens, _style.SettingsPrefab, _display,
                    () => _screens.Close(state.Id), () => _views.Remove(state.Id), _audio);
            else view = new View(this, state);
            _views.Add(state.Id, view); return view;
        }
        public void Render()
        {
            if (_disposed) return;
            if (!_sceneFade.IsVisible && (_scenes.IsTransitioning || _isGameFlowTransitioning() || _actionPending))
            {
                if (_loadingId == 0) _loadingId = _screens.ShowLoading().Id;
            }
            else if (_loadingId != 0)
            {
                long loading = _loadingId; _loadingId = 0; _screens.Close(loading);
            }
            if (!_sceneFade.IsVisible && UIEscapeInput.WasPressedThisFrame)
            {
                if (_screens.Top == null) _screens.ShowMenu();
                else _screens.Back();
            }
            foreach (var view in _views.Values)
            {
                if (view is LoadingScreenPresenter loading) loading.Refresh();
                else if (view is View panel) panel.Refresh();
            }
            // 개인 메뉴/설정의 결과는 읽게 두고, Scene 전환/결과 화면에는 묵은 알림을 남기지 않습니다.
            bool covered = _sceneFade.IsVisible || _scenes.IsTransitioning || _isGameFlowTransitioning() || _actionPending ||
                _screens.Top?.Kind == ScreenKind.Loading || _screens.Top?.Kind == ScreenKind.MatchResult;
            _alerts.Tick(Time.unscaledTimeAsDouble, _canvas.Alerts.DisplaySeconds, covered);
            _canvas.Alerts.Render(_alerts);
            _sceneFade.KeepOnTop();
        }
        internal static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            // Scene에 UI가 없는 격리 경로만 준비합니다. 제품 Scene에는 제작된 입력 모듈을 사용합니다.
            new GameObject("Core UI EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
        }
        private async void Travel(string scene)
        {
            if (_disposed || _actionPending) return;
            _actionFailure = null; _actionPending = true;
            try
            {
                if (_loadingId == 0) _loadingId = _screens.ShowLoading().Id;
                await _replace(scene);
            }
            catch (OperationCanceledException) { }
            catch (Exception error) { _actionFailure = "이동하지 못했습니다. 다시 시도해 주세요."; Debug.LogException(error); }
            finally
            {
                _actionPending = false;
                if (!_disposed && !_scenes.IsTransitioning && _loadingId != 0)
                { long loading = _loadingId; _loadingId = 0; _screens.Close(loading); }
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _alertBinding.Dispose();
            _alerts.Clear();
            if (_ownsAlerts) _alerts.Dispose();
            _sceneFade.Dispose(); UnityEngine.Object.Destroy(_canvas.gameObject);
        }

        private sealed class View : IScreenPresenter
        {
            private readonly AppScreenPresenter _owner;
            private readonly ScreenState _state;
            private readonly AppPanelView _view;
            private GameObject _previousSelection;
            private GameObject _lastSelection;
            private bool _disposed;
            public View(AppScreenPresenter owner, ScreenState state)
            {
                _owner = owner; _state = state;
                _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                bool menu = state.Kind == ScreenKind.Menu;
                _view = UnityEngine.Object.Instantiate(menu ? owner._style.MenuPrefab : owner._style.ResultPrefab, owner._canvas.Screens, false);
                _view.name = "Screen:" + state.Kind;
                if (menu)
                {
                    _view.Primary.onClick.AddListener(Resume);
                    _view.Settings.onClick.AddListener(Settings);
                    _view.Secondary.onClick.AddListener(ReturnToHub);
                }
                else
                {
                    _view.Primary.onClick.AddListener(Restart);
                    _view.Secondary.onClick.AddListener(ReturnToHub);
                }
                Refresh();
            }
            private void Resume() => _owner._screens.Close(_state.Id);
            private void Settings() => _owner._screens.ShowSettings();
            private void Restart() => _owner.Travel(StageScene);
            private void ReturnToHub() => _owner.Travel(HubScene);
            public void Refresh()
            {
                if (_disposed) return;
                string restriction = _owner._travelRestriction();
                if (_state.Kind == ScreenKind.Menu)
                {
                    _view.SetSecondaryVisible(_owner._canReturnToHub());
                    _view.Secondary.interactable = !_owner._actionPending && string.IsNullOrEmpty(restriction);
                    _view.Status.text = _owner._actionFailure ?? "";
                    _view.Status.gameObject.SetActive(_view.Status.text.Length > 0);
                    return;
                }
                var result = _state.Completion.Result;
                _view.Title.text = result.Outcome switch
                {
                    TeamHJD.Game.Domain.MatchOutcome.Victory => "VICTORY",
                    TeamHJD.Game.Domain.MatchOutcome.Defeat => "DEFEAT",
                    _ => "MISSION ENDED"
                };
                _view.Body.text = result.Outcome switch
                {
                    TeamHJD.Game.Domain.MatchOutcome.Victory => "승리",
                    TeamHJD.Game.Domain.MatchOutcome.Defeat => "패배",
                    _ => "임무 종료"
                };
                if(result.CompletedWave>0)_view.Body.text += $"\n완료 웨이브  {result.CompletedWave}";
                _view.Primary.interactable = _view.Secondary.interactable = !_owner._actionPending && string.IsNullOrEmpty(restriction);
                _view.Status.text = _owner._actionFailure ?? (!string.IsNullOrEmpty(restriction) ? "결과를 확인하고 있습니다." : "");
                _view.Status.gameObject.SetActive(_view.Status.text.Length > 0);
            }
            public void SetFocused(bool focused)
            {
                if (_disposed) return;
                if (!focused && EventSystem.current != null)
                {
                    var selected = EventSystem.current.currentSelectedGameObject;
                    if (selected != null && selected.transform.IsChildOf(_view.transform)) _lastSelection = selected;
                }
                _view.SetFocused(focused);
                if (focused)
                {
                    EnsureEventSystem();
                    var selectable = _lastSelection != null ? _lastSelection.GetComponent<UnityEngine.UI.Selectable>() : null;
                    EventSystem.current.SetSelectedGameObject(selectable != null && selectable.IsActive() && selectable.IsInteractable()
                        ? _lastSelection : _view.Primary.interactable ? _view.Primary.gameObject : null);
                }
                else ClearSelection(false);
            }
            private void ClearSelection(bool restore)
            {
                if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject == null ||
                    !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_view.transform)) return;
                EventSystem.current.SetSelectedGameObject(restore && _previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
            }
            public void Dispose()
            {
                if (_disposed) return; _disposed = true;
                _view.Primary.onClick.RemoveListener(Resume); _view.Primary.onClick.RemoveListener(Restart);
                if (_view.Secondary != null) _view.Secondary.onClick.RemoveListener(ReturnToHub);
                if (_view.Settings != null) _view.Settings.onClick.RemoveListener(Settings);
                ClearSelection(true); _owner._views.Remove(_state.Id); _view.CloseAndDestroy();
            }
        }
    }
}
