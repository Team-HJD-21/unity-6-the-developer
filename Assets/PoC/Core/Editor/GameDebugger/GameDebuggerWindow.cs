// 단일 Game Debugger 창에서 여러 진단 패널을 동시에 배치합니다.
// 영역별 편집 상태는 유지하고 Match 관측·시간 제어는 공용 Session을 사용합니다.
// 실제 게임 정책과 데이터 수집은 이 창의 표시 수명과 분리되어 있습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TeamHJD.Game.Editor
{
    public sealed class GameDebuggerWindow : EditorWindow
    {
        [SerializeField] private GameDebuggerLayout _layout=GameDebuggerLayout.TwoColumns;
        [SerializeField] private List<GameDebuggerPaneState> _paneStates;
        [SerializeField] private int _activePane;

        // 이전 단일 탭 창의 직렬화 설정을 최초 한 번 첫 영역으로 이관합니다.
        [SerializeField, HideInInspector] private int _page;
        [SerializeField, HideInInspector] private string _selectedRun="LIVE", _referenceRun="None", _search="", _bookmark="", _enemyFilter="", _pointFilter="", _presetFilter="", _sourceFilter="All";
        [SerializeField, HideInInspector] private float _binSeconds=5;
        [SerializeField, HideInInspector] private bool _realClock;
        [SerializeField, HideInInspector] private GameDebugMetric _metric;
        [SerializeField, HideInInspector] private BattlefieldDebugPanel _battlefieldPanel;
        private readonly List<GameDebuggerPane> _panes=new List<GameDebuggerPane>();
        private static readonly List<string> LayoutNames=new List<string>{"Single", "2 · Columns", "2 · Rows", "4 · Panels"};
        private Label _header, _footer, _navigationTarget;
        private VisualElement _paneHost, _navigation;
        private DropdownField _layoutSelector;
        private bool _pendingRefresh;
        private double _nextUiRefresh;
        private string _fileStatus;
        public GameDebuggerLayout Layout => _layout;
        public int ActivePane => _activePane;
        public int VisiblePaneCount => _layout==GameDebuggerLayout.Single ? 1 : _layout==GameDebuggerLayout.FourPanels ? 4 : 2;

        [MenuItem("Tools/TeamHJD/Game Debugger")]
        public static void Open() => GetWindow<GameDebuggerWindow>("Game Debugger");
        private void OnEnable()
        {
            EnsurePaneStates();
            BattlefieldDebugSession.Display.IsEnabled=true;
            GameDebuggerSession.Changed+=Refresh;
            BattlefieldDebugSession.Changed+=Refresh;
            EditorApplication.update+=UpdateView;
            BattlefieldDebugSession.NotifyDisplayChanged();
        }
        private void OnDisable()
        {
            GameDebuggerSession.Changed-=Refresh;
            BattlefieldDebugSession.Changed-=Refresh;
            EditorApplication.update-=UpdateView;
            DisposePanes();
        }
        private void EnsurePaneStates()
        {
            if (!Enum.IsDefined(typeof(GameDebuggerLayout),_layout)) _layout=GameDebuggerLayout.TwoColumns;
            if (_paneStates==null || _paneStates.Count==0)
            {
                _paneStates=new List<GameDebuggerPaneState>{
                    new GameDebuggerPaneState{
                        Page=_page, SelectedRun=_selectedRun, ReferenceRun=_referenceRun,
                        Search=_search, Bookmark=_bookmark, EnemyFilter=_enemyFilter,
                        PointFilter=_pointFilter, PresetFilter=_presetFilter, SourceFilter=_sourceFilter,
                        BinSeconds=_binSeconds, IsRealClock=_realClock, Metric=_metric,
                        BattlefieldPanel=_battlefieldPanel ?? new BattlefieldDebugPanel()
                    }
                };
                _battlefieldPanel=null;
            }
            int[] defaults={0,3,2,4};
            while (_paneStates.Count<4) _paneStates.Add(new GameDebuggerPaneState{Page=defaults[_paneStates.Count]});
            for (int index=0;index<4;index++)
            {
                _paneStates[index] ??= new GameDebuggerPaneState{Page=defaults[index]};
                _paneStates[index].Validate();
            }
            _activePane=Mathf.Clamp(_activePane,0,VisiblePaneCount-1);
        }
        public GameDebuggerPaneState GetPaneState(int index)
        {
            if (index<0 || index>=4) throw new ArgumentOutOfRangeException(nameof(index));
            EnsurePaneStates();
            return _paneStates[index];
        }
        public void CreateGUI()
        {
            wantsMouseMove=true;
            DisposePanes(); EnsurePaneStates();
            var root=rootVisualElement; root.Clear();
            var stylesheet=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PoC/Core/Editor/GameDebugger/GameDebugger.uss");
            if (stylesheet!=null && !root.styleSheets.Contains(stylesheet)) root.styleSheets.Add(stylesheet);
            root.AddToClassList("game-debugger");
            root.EnableInClassList("light",!EditorGUIUtility.isProSkin);
            _header=new Label(); _header.AddToClassList("status-header"); root.Add(_header);
            root.Add(new IMGUIContainer(()=> {
                using (new EditorGUI.DisabledGroupScope(BattlefieldDebugSession.Display.IsCaptureMode))
                    GameSimulationControls.DrawToolbar();
            }));
            var layouts=new VisualElement(); layouts.AddToClassList("layout-toolbar"); root.Add(layouts);
            _layoutSelector=new DropdownField("Layout",LayoutNames,(int)_layout);
            _layoutSelector.RegisterValueChangedCallback(evt=>SetLayout((GameDebuggerLayout)LayoutNames.IndexOf(evt.newValue)));
            layouts.Add(_layoutSelector);
            layouts.Add(new Button(()=>ApplyPreset(1,3)){text="Battlefield + Stats"});
            layouts.Add(new Button(()=>ApplyPreset(2,3)){text="Encounter + Stats"});
            layouts.Add(new Button(()=>ApplyPreset(0,1,2,3)){text="Monitor ×4"});
            var workspace=new VisualElement(); workspace.AddToClassList("workspace"); root.Add(workspace);
            _navigation=new VisualElement(); _navigation.AddToClassList("navigation"); workspace.Add(_navigation);
            _navigationTarget=new Label(); _navigationTarget.AddToClassList("navigation-target"); _navigation.Add(_navigationTarget);
            for (int index=0;index<GameDebuggerPane.PageNames.Count;index++)
            {
                int page=index;
                _navigation.Add(new Button(()=>SelectPage(_activePane,page)){
                    text=GameDebuggerPane.PageNames[index], name="page-"+index});
            }
            _paneHost=new VisualElement {name="pane-workspace"};
            _paneHost.AddToClassList("pane-workspace"); workspace.Add(_paneHost);
            _footer=new Label(); _footer.AddToClassList("footer"); root.Add(_footer);
            RebuildPanes();
        }
        public void SetLayout(GameDebuggerLayout layout)
        {
            if (!Enum.IsDefined(typeof(GameDebuggerLayout),layout)) throw new ArgumentOutOfRangeException(nameof(layout));
            _layout=layout; EnsurePaneStates();
            _layoutSelector?.SetValueWithoutNotify(LayoutNames[(int)layout]);
            if (_paneHost!=null) RebuildPanes();
        }
        public void SetActivePane(int index)
        {
            if (index<0 || index>=VisiblePaneCount) throw new ArgumentOutOfRangeException(nameof(index));
            _activePane=index; RefreshNavigation();
        }
        public void SelectPage(int paneIndex,int page)
        {
            if (paneIndex<0 || paneIndex>=VisiblePaneCount) throw new ArgumentOutOfRangeException(nameof(paneIndex));
            if (page<0 || page>=GameDebuggerPane.PageNames.Count) throw new ArgumentOutOfRangeException(nameof(page));
            EnsurePaneStates(); SetActivePane(paneIndex);
            if (_panes.Count>paneIndex) _panes[paneIndex].SelectPage(page);
            else _paneStates[paneIndex].Page=page;
            RefreshNavigation();
        }
        private void ApplyPreset(params int[] pages)
        {
            // 배치는 명시적으로 선택한 기능만 변경하며 각 영역의 필터/기록은 보존합니다.
            SetLayout(pages.Length==4 ? GameDebuggerLayout.FourPanels : GameDebuggerLayout.TwoColumns);
            for (int index=0;index<pages.Length;index++) SelectPage(index,pages[index]);
            SetActivePane(0);
        }
        private void RebuildPanes()
        {
            DisposePanes(); _paneHost.Clear();
            for (int index=0;index<VisiblePaneCount;index++)
                _panes.Add(new GameDebuggerPane(index,_paneStates[index],Refresh,SetActivePane,value=>{_fileStatus=value;Refresh();}));
            minSize=_layout==GameDebuggerLayout.Single ? new Vector2(660,450) :
                _layout==GameDebuggerLayout.TwoColumns ? new Vector2(960,500) :
                _layout==GameDebuggerLayout.TwoRows ? new Vector2(660,700) : new Vector2(1000,740);
            if (_layout==GameDebuggerLayout.Single) _paneHost.Add(_panes[0]);
            else if (_layout==GameDebuggerLayout.TwoColumns)
                _paneHost.Add(Split(_panes[0],_panes[1],TwoPaneSplitViewOrientation.Horizontal,"columns",Mathf.Max(340,(position.width-180)/2)));
            else if (_layout==GameDebuggerLayout.TwoRows)
                _paneHost.Add(Split(_panes[0],_panes[1],TwoPaneSplitViewOrientation.Vertical,"rows",Mathf.Max(220,(position.height-160)/2)));
            else
            {
                var left=Split(_panes[0],_panes[2],TwoPaneSplitViewOrientation.Vertical,"quad-left",Mathf.Max(220,(position.height-160)/2));
                var right=Split(_panes[1],_panes[3],TwoPaneSplitViewOrientation.Vertical,"quad-right",Mathf.Max(220,(position.height-160)/2));
                _paneHost.Add(Split(left,right,TwoPaneSplitViewOrientation.Horizontal,"quad-columns",Mathf.Max(360,(position.width-180)/2)));
            }
            RefreshNavigation(); RefreshView();
        }
        private static TwoPaneSplitView Split(VisualElement first,VisualElement second,TwoPaneSplitViewOrientation orientation,string key,float initialSize)
        {
            var split=new TwoPaneSplitView(0,initialSize,orientation){viewDataKey="game-debugger-"+key};
            split.AddToClassList("pane-split"); split.Add(first); split.Add(second); return split;
        }
        private void DisposePanes()
        {
            foreach (var pane in _panes) pane.Dispose();
            _panes.Clear();
        }
        private void RefreshNavigation()
        {
            if (_navigation==null) return;
            _navigationTarget.text="ACTIVE · P"+(_activePane+1);
            for (int index=0;index<GameDebuggerPane.PageNames.Count;index++)
                _navigation.Q<Button>("page-"+index)?.EnableInClassList("active-page",index==_paneStates[_activePane].Page);
            foreach (var pane in _panes) pane.SetActive(pane.Index==_activePane);
        }
        private void Refresh() => _pendingRefresh=true;
        private void UpdateView()
        {
            if (!_pendingRefresh || EditorApplication.timeSinceStartup<_nextUiRefresh) return;
            _pendingRefresh=false; _nextUiRefresh=EditorApplication.timeSinceStartup+0.25; RefreshView();
        }
        private void RefreshView()
        {
            if (_header==null) return;
            var source=GameDebuggerSession.Source;
            _header.text=$"GAME DEBUGGER · {BattlefieldDebugSession.Mode} · {(source==null?"authority N/A":source.IsServer?"SERVER":"CLIENT/OFFLINE")} · game {source?.GameSeconds ?? 0:F1}s / real {source?.RealSeconds ?? 0:F1}s";
            _footer.text=_fileStatus ?? (GameDebuggerSession.Warnings.Count==0 ?
                "공용 Match / 시간 / Scene 표시 · 영역별 기능 / 기록 / 필터 · 경계선을 드래그해 크기 조절" :
                string.Join(" | ",GameDebuggerSession.Warnings.Take(3)));
            foreach (var pane in _panes) pane.RefreshView();
            RefreshNavigation(); Repaint();
        }
    }
}
