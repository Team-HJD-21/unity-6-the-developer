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
        private readonly Func<string> _recoveryError;
        private readonly Func<Task> _retryRecovery;
        private readonly Func<Task> _useSafety;
        private readonly Func<MatchRecovery> _missionRecovery;
        private readonly Func<string> _mainScene;
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
        private long _recoveryScreenId;
        private TeamHJD.Game.Domain.MatchId _recoveryMatch;
        private bool _disposed, _actionPending;
        private string _actionFailure;

        public AppScreenPresenter(Transform parent, ScreenManager screens, SceneFlowController scenes,
            Func<string, Task> replace, Func<string> travelRestriction, Func<bool> isGameFlowTransitioning = null,
            UiDisplaySettings display = null, Func<bool> canReturnToHub = null, AlertService alerts = null, GameAudioSettings audio = null,
            Func<string> recoveryError = null, Func<Task> retryRecovery = null,
            Func<MatchRecovery> missionRecovery = null, Func<string> mainScene = null, Func<Task> useSafety = null)
        {
            _screens = screens ?? throw new ArgumentNullException(nameof(screens));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace));
            _travelRestriction = travelRestriction ?? throw new ArgumentNullException(nameof(travelRestriction));
            _isGameFlowTransitioning = isGameFlowTransitioning ?? (() => false);
            _canReturnToHub = canReturnToHub ?? (() => true);
            _recoveryError = recoveryError;
            _retryRecovery = retryRecovery;
            _useSafety = useSafety;
            _missionRecovery = missionRecovery;
            _mainScene = mainScene ?? (() => "Assets/Scenes/Bootstrap/Startup.unity");
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
            if (_recoveryMatch != _screens.BoundMatchId)
            { _recoveryScreenId = 0; _recoveryMatch = _screens.BoundMatchId; }
            var recovery = _missionRecovery?.Invoke();
            bool needsRecovery = recovery?.IsRecoveryPending == true && !recovery.Ledger.IsEnded && _screens.LastCompletion == null;
            if (needsRecovery && _recoveryScreenId == 0)
                _recoveryScreenId = _screens.ShowRecovery()?.Id ?? 0;
            else if (!needsRecovery && _recoveryScreenId != 0)
            { _screens.Close(_recoveryScreenId); _recoveryScreenId = 0; }
            // 빌드에서도 기술 실패를 재시도할 수 있게 기존 메뉴를 엽니다. 별도 전투 결과/새 게임으로 바꾸지 않습니다.
            if (HasRecoveryFailure && !needsRecovery && !_actionPending && !_scenes.IsTransitioning && !_isGameFlowTransitioning() && _screens.Top == null)
                _screens.ShowMenu();
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
                else if (_screens.Top.Kind != ScreenKind.Menu || !HasRecoveryFailure) _screens.Back();
            }
            foreach (var view in _views.Values)
            {
                if (view is LoadingScreenPresenter loading) loading.Refresh();
                else if (view is View panel) panel.Refresh();
            }
            // 개인 메뉴/설정의 결과는 읽게 두고, Scene 전환/결과 화면에는 묵은 알림을 남기지 않습니다.
            bool covered = _sceneFade.IsVisible || _scenes.IsTransitioning || _isGameFlowTransitioning() || _actionPending ||
                _screens.Top?.Kind == ScreenKind.Loading || _screens.Top?.Kind == ScreenKind.MatchResult || HasRecoveryFailure;
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

        private bool HasRecoveryFailure => !string.IsNullOrEmpty(_recoveryError?.Invoke());

        private async void RetryRecovery(long menuId)
        {
            var action = _missionRecovery?.Invoke()?.IsAwaitingSafetyChoice == true ? _useSafety : _retryRecovery;
            if (_disposed || _actionPending || action == null) return;
            _actionFailure = null;
            _actionPending = true;
            try
            {
                await action();
                if (!_disposed && !HasRecoveryFailure) { _screens.Close(menuId); if (_recoveryScreenId == menuId) _recoveryScreenId = 0; }
            }
            catch (Exception)
            {
                // 원본 예외는 App/Scene이 보존합니다. 메뉴에는 짧은 안내만 표시합니다.
                if (!_disposed) _actionFailure = "복귀하지 못했습니다. 다시 시도해 주세요.";
            }
            finally { _actionPending = false; }
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
            private readonly string _menuTitle, _menuPrimary;
            private readonly TMPro.TMP_Text _primaryLabel;
            private bool _disposed;
            public View(AppScreenPresenter owner, ScreenState state)
            {
                _owner = owner; _state = state;
                _previousSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                bool menu = state.Kind == ScreenKind.Menu;
                bool recovery = state.Kind == ScreenKind.MissionRecovery;
                _view = UnityEngine.Object.Instantiate(menu ? owner._style.MenuPrefab : recovery ? owner._style.RecoveryPrefab : owner._style.ResultPrefab, owner._canvas.Screens, false);
                _view.name = "Screen:" + state.Kind;
                _primaryLabel = _view.Primary.GetComponentInChildren<TMPro.TMP_Text>(true);
                if (menu)
                {
                    _menuTitle = _view.Title.text;
                    _menuPrimary = _primaryLabel.text;
                    _view.Primary.onClick.AddListener(Resume);
                    _view.Settings.onClick.AddListener(Settings);
                    _view.Secondary.onClick.AddListener(ReturnToHub);
                }
                else if (recovery)
                {
                    _view.Primary.onClick.AddListener(UseSafety);
                    _view.Secondary.onClick.AddListener(Restart);
                }
                else
                {
                    _view.Primary.onClick.AddListener(Restart);
                    _view.Secondary.onClick.AddListener(ReturnToHub);
                }
                if (_view.MainMenu != null) _view.MainMenu.onClick.AddListener(ReturnToMain);
                Refresh();
            }
            private void Resume()
            {
                if (_owner.HasRecoveryFailure) _owner.RetryRecovery(_state.Id);
                else _owner._screens.Close(_state.Id);
            }
            private void Settings() => _owner._screens.ShowSettings();
            private void Restart() => _owner.Travel(StageScene);
            private void ReturnToHub() => _owner.Travel(HubScene);
            private void ReturnToMain() => _owner.Travel(_owner._mainScene());
            private void UseSafety() => _owner.RetryRecovery(_state.Id);
            public void Refresh()
            {
                if (_disposed) return;
                string restriction = _owner._travelRestriction();
                if (_view.MainMenu != null) _view.MainMenu.interactable = !_owner._actionPending && string.IsNullOrEmpty(restriction);
                if (_state.Kind == ScreenKind.Menu)
                {
                    bool recovering = _owner.HasRecoveryFailure;
                    _view.Title.text = recovering ? "복귀 오류" : _menuTitle;
                    _primaryLabel.text = recovering ? "같은 복귀 재시도" : _menuPrimary;
                    _view.Primary.interactable = !_owner._actionPending && (!recovering || _owner._retryRecovery != null);
                    _view.SetSecondaryVisible(_owner._canReturnToHub());
                    _view.Secondary.interactable = !_owner._actionPending && string.IsNullOrEmpty(restriction);
                    _view.Status.text = _owner._actionFailure ?? (recovering ? "복귀하지 못했습니다.\n재시도 시 안전장치 추가 차감 없음" : "");
                    _view.Status.gameObject.SetActive(_view.Status.text.Length > 0);
                    return;
                }
                if (_state.Kind == ScreenKind.MissionRecovery)
                {
                    var recovery = _owner._missionRecovery?.Invoke();
                    bool pending = recovery?.IsRecoveryPending == true && !recovery.Ledger.IsEnded;
                    _view.Body.text = FailureCaption(recovery?.FailureReason);
                    _view.SetSafetyUses(recovery?.RemainingSafetyUses ?? 0);
                    _primaryLabel.text = _owner.HasRecoveryFailure ? "복귀 재시도" : "안전장치 사용";
                    bool hasAction = recovery?.IsAwaitingSafetyChoice == true ? _owner._useSafety != null : _owner._retryRecovery != null;
                    _view.Primary.interactable = pending && !_owner._actionPending && string.IsNullOrEmpty(restriction) && hasAction;
                    _view.Secondary.interactable = !_owner._actionPending && string.IsNullOrEmpty(restriction);
                    _view.Status.text = _owner._actionFailure ?? (_owner.HasRecoveryFailure ? "복귀하지 못했습니다." : "");
                    _view.Status.gameObject.SetActive(_view.Status.text.Length > 0);
                    return;
                }
                var result = _state.Completion.Result;
                _view.Title.text = result.Outcome switch
                {
                    TeamHJD.Game.Domain.MatchOutcome.Victory => "승리",
                    TeamHJD.Game.Domain.MatchOutcome.Defeat => "패배",
                    _ => "임무 종료"
                };
                _view.Body.text = result.Outcome switch
                {
                    TeamHJD.Game.Domain.MatchOutcome.Victory => "임무를 완수했습니다.",
                    TeamHJD.Game.Domain.MatchOutcome.Defeat => result.EndReason switch
                    {
                        TeamHJD.Game.Domain.MatchEndReason.ControlUnitDestroyed => "CU가 파괴되었습니다.",
                        TeamHJD.Game.Domain.MatchEndReason.AllPlayersDown => "모든 대원이 쓰러졌습니다.\n안전장치를 모두 사용했습니다.",
                        TeamHJD.Game.Domain.MatchEndReason.RescueBudgetExhausted => "구조 시간이 소진되었습니다.\n안전장치를 모두 사용했습니다.",
                        _ => "임무를 계속할 수 없습니다."
                    },
                    _ => "임무가 종료되었습니다."
                };
                if (_view.OutcomeAccent != null) _view.OutcomeAccent.color = result.Outcome == TeamHJD.Game.Domain.MatchOutcome.Victory ? _view.VictoryColor : _view.DefeatColor;
                if(result.CompletedWave>0)_view.Body.text += $"\n완료 웨이브  {result.CompletedWave}";
                _view.Primary.interactable = _view.Secondary.interactable = !_owner._actionPending && string.IsNullOrEmpty(restriction);
                _view.Status.text = _owner._actionFailure ?? (!string.IsNullOrEmpty(restriction) ? "결과를 확인하고 있습니다." : "");
                _view.Status.gameObject.SetActive(_view.Status.text.Length > 0);
            }
            private static string FailureCaption(string reason) => reason switch
            {
                "RescueBudgetExhausted" => "구조 시간이 소진되었습니다.",
                "AllPlayersDown" => "모든 대원이 쓰러졌습니다.",
                _ => "전투가 중단되었습니다."
            };
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
                _view.Primary.onClick.RemoveListener(UseSafety);
                if (_view.Secondary != null) _view.Secondary.onClick.RemoveListener(ReturnToHub);
                if (_view.Secondary != null) _view.Secondary.onClick.RemoveListener(Restart);
                if (_view.MainMenu != null) _view.MainMenu.onClick.RemoveListener(ReturnToMain);
                if (_view.Settings != null) _view.Settings.onClick.RemoveListener(Settings);
                ClearSelection(true); _owner._views.Remove(_state.Id); _view.CloseAndDestroy();
            }
        }
    }
}
