// 실제 UGUI 운영 화면을 만들고 ScreenManager의 focus·폐기와 연결합니다.
// 전투 HUD는 유지하며 결과는 불변 자료, 로딩은 실제 Scene 상태를 표시합니다.
// 프레임 호출은 AppRoot가 담당하고 임의의 전역 UI/입력 Singleton을 만들지 않습니다.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace TeamHJD.Game.Presentation
{
    public sealed class AppScreenPresenter : IDisposable
    {
        public const string StageScene = "Assets/Scenes/Gameplay/Initial_Stage.unity";
        public const string HubScene = "Assets/Scenes/Spaceship/SpaceShipGrayBox.unity";
        private readonly ScreenManager _screens;
        private readonly SceneFlowController _scenes;
        private readonly Func<string, Task> _replace;
        private readonly Func<string> _travelRestriction;
        private readonly Dictionary<long, IScreenPresenter> _views = new Dictionary<long, IScreenPresenter>();
        private readonly Func<bool> _isGameFlowTransitioning;
        private readonly GameObject _root;
        private readonly Font _font;
        private readonly AppScreenStyle _style;
        private readonly SceneFadePresenter _sceneFade;
        private long _loadingId;
        private bool _disposed, _actionPending;
        private string _actionFailure;
        public AppScreenPresenter(Transform parent, ScreenManager screens, SceneFlowController scenes,
            Func<string, Task> replace, Func<string> travelRestriction, Func<bool> isGameFlowTransitioning = null)
        {
            _isGameFlowTransitioning = isGameFlowTransitioning ?? (() => false);
            _screens = screens ?? throw new ArgumentNullException(nameof(screens));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace));
            _travelRestriction = travelRestriction ?? throw new ArgumentNullException(nameof(travelRestriction));
            _root = new GameObject("App Screens", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(parent, false);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10000;
            var scale = _root.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720); scale.matchWidthOrHeight = .5f;
            _style = Resources.Load<AppScreenStyle>("Core/AppScreenStyle");
            _font = _style != null && _style.Font != null ? _style.Font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _screens.SetPresenterFactory(Create);
            _sceneFade = new SceneFadePresenter(_root.transform);
        }

        /// <summary>우주선 출입 조율부 전용 암전입니다. 일반 Startup 로딩의 진행 표시와 구분합니다.</summary>
        public Task FadeSceneAsync(Func<Task> move, CancellationToken cancellation) => _sceneFade.RunAsync(move, cancellation);
        private IScreenPresenter Create(ScreenState state)
        {
            EnsureEventSystem();
            IScreenPresenter view = state.Kind == ScreenKind.Loading
                ? new LoadingScreenPresenter(_root.transform,
                    () => _scenes.IsTransitioning ? _scenes.Progress : float.NaN, () => _views.Remove(state.Id), _font)
                : new View(this, state);
            _views.Add(state.Id, view);
            return view;
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
            _sceneFade.KeepOnTop();
        }
        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            // 현재 Scene에만 둡니다. 다음 Scene의 제작 EventSystem과 영구 중복되지 않습니다.
            new GameObject("Core UI EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
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
            catch (Exception error) { _actionFailure = error.Message; }
            finally
            {
                _actionPending = false;
                if (!_disposed && !_scenes.IsTransitioning && _loadingId != 0)
                {
                    long loading = _loadingId; _loadingId = 0; _screens.Close(loading);
                }
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _sceneFade.Dispose();
            // View의 소유자는 ScreenManager입니다. 조립부가 Screens를 먼저 닫은 뒤 Canvas를 해제합니다.
            UnityEngine.Object.Destroy(_root);
        }

        private sealed class View : IScreenPresenter
        {
            private static readonly Color PanelColor = new Color(.075f, .09f, .12f, .98f);
            private static readonly Color Accent = new Color(.22f, .72f, .86f, 1);
            private readonly AppScreenPresenter _owner;
            private readonly ScreenState _state;
            private readonly GameObject _overlay;
            private readonly Text _body, _status;
            private readonly Button _primary, _secondary;
            private GameObject _previousSelection;
            private bool _disposed;
            public View(AppScreenPresenter owner, ScreenState state)
            {
                _owner = owner; _state = state;
                _overlay = new GameObject("Screen:" + state.Kind, typeof(RectTransform), typeof(Image));
                _overlay.transform.SetParent(owner._root.transform, false);
                Stretch((RectTransform)_overlay.transform);
                _overlay.GetComponent<Image>().color = new Color(0, 0, 0, .7f);
                try
                {
                    var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
                    panel.transform.SetParent(_overlay.transform, false);
                    var rect = (RectTransform)panel.transform;
                    rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
                    rect.sizeDelta = new Vector2(480, 410);
                    ApplyImage(panel.GetComponent<Image>(), owner._style?.PanelSprite, PanelColor);
                    var title = Label(panel.transform, "Title", 30, 30, 420, 45, 29);
                    title.color = owner._style != null ? owner._style.TitleColor : Accent;
                    title.text = state.Kind == ScreenKind.Menu ? "MENU" :
                        state.Completion.Result.Outcome == TeamHJD.Game.Domain.MatchOutcome.Victory ? "VICTORY" : "MATCH COMPLETE";
                    _body = Label(panel.transform, "Details", 30, 94, 420, 115, 18);
                    _status = Label(panel.transform, "Status", 30, 220, 420, 64, 15);
                    _status.color = new Color(.78f, .82f, .88f, 1);
                    _primary = Button(panel.transform, "Primary", 30, 310, 200, 48,
                        state.Kind == ScreenKind.Menu ? "Resume" : "Restart");
                    _secondary = Button(panel.transform, "Secondary", 250, 310, 200, 48, "Return to spaceship");
                    if (state.Kind == ScreenKind.Menu) _primary.onClick.AddListener(() => owner._screens.Close(state.Id));
                    else _primary.onClick.AddListener(() => owner.Travel(StageScene));
                    _secondary.onClick.AddListener(() => owner.Travel(HubScene));
                    _secondary.gameObject.SetActive(state.Kind == ScreenKind.MatchResult);
                    var primaryNavigation = new Navigation { mode = Navigation.Mode.Explicit };
                    if (_secondary.gameObject.activeSelf)
                    { primaryNavigation.selectOnRight = _secondary; primaryNavigation.selectOnDown = _secondary; }
                    _primary.navigation = primaryNavigation;
                    _secondary.navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnLeft = _primary, selectOnUp = _primary };
                    Refresh();
                }
                catch { UnityEngine.Object.Destroy(_overlay); throw; }
            }
            public void Refresh()
            {
                if (_disposed) return;
                if (_state.Kind == ScreenKind.Menu)
                {
                    _body.text = "Local controls are locked while this menu is open.\nThe shared Match keeps running.";
                    _status.text = "Escape / Resume to return. Gameplay authority is unchanged.";
                }
                else
                {
                    var result = _state.Completion.Result;
                    _body.text = $"Outcome  {result.Outcome}\nCompleted wave  {result.CompletedWave}\nMatch  {result.MatchId.Value}";
                    string restriction = _owner._travelRestriction();
                    _primary.interactable = _secondary.interactable = !_owner._actionPending && string.IsNullOrEmpty(restriction);
                    _status.text = _owner._actionFailure ?? (string.IsNullOrEmpty(restriction) ?
                        "Result preserved. Rewards require the profile/backend policy." : restriction);
                }
            }
            public void SetFocused(bool focused)
            {
                if (_disposed) return;
                if (focused)
                {
                    EnsureEventSystem();
                    _previousSelection = EventSystem.current.currentSelectedGameObject;
                    _overlay.SetActive(true);
                    EventSystem.current.SetSelectedGameObject(_primary.interactable ? _primary.gameObject : null);
                }
                else
                {
                    _overlay.SetActive(false);
                    if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                        EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_overlay.transform))
                        EventSystem.current.SetSelectedGameObject(null);
                }
            }
            public void Dispose()
            {
                if (_disposed) return; _disposed = true;
                _primary.onClick.RemoveAllListeners(); _secondary.onClick.RemoveAllListeners();
                if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject != null &&
                    EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_overlay.transform))
                    EventSystem.current.SetSelectedGameObject(_previousSelection != null && _previousSelection.activeInHierarchy ? _previousSelection : null);
                _owner._views.Remove(_state.Id); UnityEngine.Object.Destroy(_overlay);
            }
            private Text Label(Transform parent, string name, float x, float y, float width, float height, int size)
            {
                var text = new GameObject(name, typeof(RectTransform), typeof(Text)); text.transform.SetParent(parent, false);
                Position((RectTransform)text.transform, x, y, width, height);
                var label = text.GetComponent<Text>(); label.font = _owner._font; label.fontSize = size;
                label.color = _owner._style != null ? _owner._style.TextColor : Color.white; label.raycastTarget = false;
                return label;
            }
            private Button Button(Transform parent, string name, float x, float y, float width, float height, string title)
            {
                var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                button.transform.SetParent(parent, false); Position((RectTransform)button.transform, x, y, width, height);
                ApplyImage(button.GetComponent<Image>(), _owner._style?.ButtonSprite, new Color(.12f, .25f, .32f, 1));
                var text = Label(button.transform, "Label", 0, 0, width, height, 16); text.text = title; text.alignment = TextAnchor.MiddleCenter;
                return button.GetComponent<Button>();
            }
            private static void ApplyImage(Image image, Sprite sprite, Color fallback)
            {
                image.sprite = sprite;
                image.color = sprite != null ? Color.white : fallback;
                image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            }

            private static void Position(RectTransform rect, float x, float y, float width, float height)
            {
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(x, -y); rect.sizeDelta = new Vector2(width, height);
            }
            private static void Stretch(RectTransform rect)
            { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero; }
        }
    }
}
