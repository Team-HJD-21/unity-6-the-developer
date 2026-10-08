// Statistics를 독립된 큰 Editor 창에서 조회합니다.
// 패널 설정은 복사하고 공용 기록 수집·Match는 공유하므로 게임 실행을 복제하지 않습니다.
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugStatisticsWindow : EditorWindow
    {
        [SerializeField] private GameDebuggerPaneState _state=new GameDebuggerPaneState{Page=3};
        private GameDebuggerPane _pane;
        private Label _status;
        private bool _pending;
        private double _next;
        public static GameDebugStatisticsWindow Open(GameDebuggerPaneState state)
        {
            var window=CreateInstance<GameDebugStatisticsWindow>();
            window._state=JsonUtility.FromJson<GameDebuggerPaneState>(JsonUtility.ToJson(state));
            window._state.Page=3; window._state.Validate();
            window.titleContent=new GUIContent("Game Statistics");
            window.minSize=new Vector2(720,520);
            window.Show(); return window;
        }
        private void OnEnable()
        {
            minSize=new Vector2(720,520);
            _state ??= new GameDebuggerPaneState{Page=3};
            _state.Validate();
            GameDebuggerSession.Changed+=RequestRefresh;
            BattlefieldDebugSession.Changed+=RequestRefresh;
            EditorApplication.update+=UpdateView;
        }
        private void OnDisable()
        {
            GameDebuggerSession.Changed-=RequestRefresh;
            BattlefieldDebugSession.Changed-=RequestRefresh;
            EditorApplication.update-=UpdateView;
            _pane?.Dispose();
        }
        public void CreateGUI()
        {
            wantsMouseMove=true;
            _pane?.Dispose();
            var root=rootVisualElement; root.Clear(); root.AddToClassList("game-debugger");
            root.EnableInClassList("light",!EditorGUIUtility.isProSkin);
            var style=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Scripts/Core/Editor/GameDebugger/GameDebugger.uss");
            if (style!=null && !root.styleSheets.Contains(style)) root.styleSheets.Add(style);
            _state.Page=3;
            _status=new Label("공용 기록 · 독립 보기 설정 · 읽기 전용"); _status.AddToClassList("footer");
            _pane=new GameDebuggerPane(0,_state,RequestRefresh,_=>{},value=>_status.text=value,true);
            root.Add(_pane); root.Add(_status);
            _pane.RefreshView();
        }
        private void RequestRefresh() => _pending=true;
        private void UpdateView()
        {
            if (!_pending || EditorApplication.timeSinceStartup<_next) return;
            _pending=false; _next=EditorApplication.timeSinceStartup+0.25;
            _pane?.RefreshView(); Repaint();
        }
    }
}
