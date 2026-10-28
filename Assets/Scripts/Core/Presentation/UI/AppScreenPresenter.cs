// 실제 UGUI 운영 화면을 만들고 ScreenManager의 focus·폐기와 연결합니다.
// 전투 HUD는 유지하며 결과는 불변 자료, 로딩은 실제 Scene 상태를 표시합니다.
// 프레임 호출은 AppRoot가 담당하고 임의의 전역 UI/입력 Singleton을 만들지 않습니다.
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        private readonly Dictionary<long, View> _views = new Dictionary<long, View>();
        private readonly GameObject _root;
        private readonly Font _font;
        private long _loadingId;
        private bool _disposed, _actionPending;
        private string _actionFailure;
        public AppScreenPresenter(Transform parent, ScreenManager screens, SceneFlowController scenes,
            Func<string, Task> replace, Func<string> travelRestriction)
        {
            _screens = screens ?? throw new ArgumentNullException(nameof(screens));
            _scenes = scenes ?? throw new ArgumentNullException(nameof(scenes));
            _replace = replace ?? throw new ArgumentNullException(nameof(replace));
            _travelRestriction = travelRestriction ?? throw new ArgumentNullException(nameof(travelRestriction));
            _root = new GameObject("App Screens", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _root.transform.SetParent(parent, false);
            var canvas = _root.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 10000;
            var scale = _root.GetComponent<CanvasScaler>(); scale.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scale.referenceResolution = new Vector2(1280, 720); scale.matchWidthOrHeight = .5f;
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            _screens.SetPresenterFactory(Create);
        }
        private IScreenPresenter Create(ScreenState state)
        {
            EnsureEventSystem();
            var view = new View(this, state);
            _views.Add(state.Id, view);
            return view;
        }
        public void Render()
        {
            if (_disposed) return;
            if (_scenes.IsTransitioning)
            {
                if (_loadingId == 0) _loadingId = _screens.ShowLoading().Id;
            }
            else if (_loadingId != 0)
            {
                long loading = _loadingId; _loadingId = 0; _screens.Close(loading);
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (_screens.Top == null) _screens.ShowMenu();
                else _screens.Back();
            }
            foreach (View view in _views.Values) view.Refresh();
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
                    rect.sizeDelta = new Vector2(480, 410); panel.GetComponent<Image>().color = PanelColor;
                    var title = Label(panel.transform, "Title", 30, 30, 420, 45, 29);
                    title.color = Accent;
                    title.text = state.Kind == ScreenKind.Menu ? "MENU" : state.Kind == ScreenKind.Loading ? "LOADING" :
                        state.Completion.Result.Outcome == TeamHJD.Game.Domain.MatchOutcome.Victory ? "VICTORY" : "MATCH COMPLETE";
                    _body = Label(panel.transform, "Details", 30, 94, 420, 115, 18);
                    _status = Label(panel.transform, "Status", 30, 220, 420, 64, 15);
                    _status.color = new Color(.78f, .82f, .88f, 1);
                    _primary = Button(panel.transform, "Primary", 30, 310, 200, 48,
                        state.Kind == ScreenKind.Menu ? "Resume" : state.Kind == ScreenKind.Loading ? "Cancel" : "Restart");
                    _secondary = Button(panel.transform, "Secondary", 250, 310, 200, 48, "Return to spaceship");
                    if (state.Kind == ScreenKind.Menu) _primary.onClick.AddListener(() => owner._screens.Close(state.Id));
                    else if (state.Kind == ScreenKind.Loading) _primary.onClick.AddListener(() => owner._scenes.CancelCurrent());
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
                else if (_state.Kind == ScreenKind.Loading)
                {
                    float progress = _owner._scenes.Progress;
                    _body.text = "Scene transition\n" + System.IO.Path.GetFileNameWithoutExtension(_owner._scenes.TargetScene ?? "");
                    _status.text = float.IsNaN(progress) ? "Waiting for network Scene completion..." : $"Loading {progress:P0}";
                    _primary.interactable = _owner._scenes.CanCancel;
                    if (!_owner._scenes.CanCancel) _status.text += "\nCommitted transition; please wait.";
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
                label.color = Color.white; label.raycastTarget = false;
                return label;
            }
            private Button Button(Transform parent, string name, float x, float y, float width, float height, string title)
            {
                var button = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                button.transform.SetParent(parent, false); Position((RectTransform)button.transform, x, y, width, height);
                button.GetComponent<Image>().color = new Color(.12f, .25f, .32f, 1);
                var text = Label(button.transform, "Label", 0, 0, width, height, 16); text.text = title; text.alignment = TextAnchor.MiddleCenter;
                return button.GetComponent<Button>();
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
