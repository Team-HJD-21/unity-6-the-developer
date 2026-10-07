// 상위 Game Debugger 창: 공간 도구·시간 제어·관측 통계·타임라인·보고서를 묶습니다.
// 게임 정책은 Runtime Feature에 두고 기록/집계/뷰를 별도 클래스로 위임합니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TeamHJD.Game.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TeamHJD.Game.Editor
{
    public sealed class GameDebuggerWindow : EditorWindow
    {
        [SerializeField] private int _page;
        [SerializeField] private string _selectedRun="LIVE", _referenceRun="None", _search="", _bookmark="", _enemyFilter="", _pointFilter="", _presetFilter="", _sourceFilter="All";
        [SerializeField] private float _binSeconds=5;
        [SerializeField] private bool _realClock;
        [SerializeField] private GameDebugMetric _metric;
        private BattlefieldDebugWindow _battlefieldPanel;
        private Label _header, _footer, _axes, _recordStatus;
        private VisualElement _body;
        private GameDebugChart _chart;
        private DropdownField _runs, _reference;
        private bool _refreshing;
        private bool _pendingRefresh;
        private double _nextUiRefresh;
        private string _fileStatus;
        private static readonly string[] Pages={"Overview","Battlefield","Encounter","Statistics","Timeline","Capture / Compare"};

        [MenuItem("Tools/TeamHJD/Game Debugger")]
        public static void Open()
        {
            var window=GetWindow<GameDebuggerWindow>("Game Debugger");
            window.minSize=new Vector2(660,450);
        }
        private void OnEnable()
        {
            _battlefieldPanel=CreateInstance<BattlefieldDebugWindow>();
            _battlefieldPanel.hideFlags=HideFlags.HideAndDontSave;
            GameDebuggerSession.Changed+=Refresh;
            BattlefieldDebugSession.Changed+=Refresh;
            EditorApplication.update+=UpdateView;
        }
        private void OnDisable()
        {
            GameDebuggerSession.Changed-=Refresh;
            BattlefieldDebugSession.Changed-=Refresh;
            EditorApplication.update-=UpdateView;
            if (_battlefieldPanel!=null) DestroyImmediate(_battlefieldPanel);
        }
        public void CreateGUI()
        {
            var root=rootVisualElement; root.Clear();
            var stylesheet=AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/PoC/Core/Editor/GameDebugger/GameDebugger.uss");
            if (stylesheet!=null) root.styleSheets.Add(stylesheet);
            root.AddToClassList("game-debugger");
            _header=new Label(); _header.AddToClassList("status-header"); root.Add(_header);
            root.Add(new IMGUIContainer(()=> {
                using (new EditorGUI.DisabledGroupScope(BattlefieldDebugSession.Display.IsCaptureMode)) GameSimulationControls.DrawToolbar();
            }));
            var workspace=new VisualElement(); workspace.AddToClassList("workspace"); root.Add(workspace);
            var navigation=new VisualElement(); navigation.AddToClassList("navigation"); workspace.Add(navigation);
            for (int index=0;index<Pages.Length;index++)
            {
                int page=index; var button=new Button(()=>{ _page=page; BuildPage(); }) { text=Pages[index], name="page-"+index };
                navigation.Add(button);
            }
            navigation.Add(new Button(BattlefieldDebugWindow.Open){text="Legacy window ↗"});
            _body=new ScrollView(); _body.AddToClassList("content"); workspace.Add(_body);
            _footer=new Label(); _footer.AddToClassList("footer"); root.Add(_footer);
            BuildPage();
        }
        private void BuildPage()
        {
            if (_body==null) return;
            _body.Clear(); _chart=null; _runs=_reference=null; _axes=_recordStatus=null;
            for (int index=0;index<Pages.Length;index++) rootVisualElement.Q<Button>("page-"+index)?.EnableInClassList("active-page",index==_page);
            var title=new Label(Pages[Mathf.Clamp(_page,0,Pages.Length-1)]); title.AddToClassList("page-title"); _body.Add(title);
            if (_page==0) _body.Add(new IMGUIContainer(GameDebugInspectionPanels.DrawOverview));
            if (_page==1) _body.Add(new IMGUIContainer(()=> { _battlefieldPanel?.DrawEmbeddedPanel(1); GameDebugInspectionPanels.DrawTurrets(); }));
            if (_page==2) _body.Add(new IMGUIContainer(()=> { _battlefieldPanel?.DrawEmbeddedPanel(2); GameDebugInspectionPanels.DrawRequests(GameDebuggerSession.Current); }));
            if (_page==3 || _page==4 || _page==5) BuildRecordSelector();
            if (_page==3 || _page==5) BuildStatistics();
            if (_page==4)
            {
                var search=new TextField("Search source / kind / text"){value=_search}; search.RegisterValueChangedCallback(evt=>_search=evt.newValue); _body.Add(search);
                var bookmark=new TextField("Bookmark"){value=_bookmark}; bookmark.RegisterValueChangedCallback(evt=>_bookmark=evt.newValue); _body.Add(bookmark);
                _body.Add(new IMGUIContainer(()=>GameDebugInspectionPanels.DrawTimeline(SelectedRun(),_search,_bookmark)));
            }
            if (_page==5) BuildCapture();
            RefreshView();
        }
        private void BuildRecordSelector()
        {
            _runs=new DropdownField("A · Record",RunChoices(),_selectedRun);
            _runs.RegisterValueChangedCallback(evt=> { if (!_refreshing) { _selectedRun=evt.newValue; Refresh(); } }); _body.Add(_runs);
            _recordStatus=new Label(); _recordStatus.AddToClassList("record-status"); _body.Add(_recordStatus);
        }
        private List<string> RunChoices()
        {
            var choices=new List<string>{"LIVE"}; choices.AddRange(GameDebuggerSession.Recorder.Runs.Select(item=>item.MatchId));
            if (GameDebuggerSession.Imported!=null) choices.Add("IMPORTED"); return choices;
        }
        private GameDebugRun SelectedRun() => Resolve(_selectedRun);
        private static GameDebugRun Resolve(string id) => id=="LIVE" ? GameDebuggerSession.Current : id=="IMPORTED" ? GameDebuggerSession.Imported : GameDebuggerSession.Recorder.Find(id);
        private void BuildStatistics()
        {
            var row=new VisualElement(); row.AddToClassList("filter-row"); _body.Add(row);
            var clock=new Toggle("Real clock"){value=_realClock}; clock.RegisterValueChangedCallback(evt=>{_realClock=evt.newValue;Refresh();}); row.Add(clock);
            var bin=new FloatField("Bin seconds"){value=_binSeconds}; bin.RegisterValueChangedCallback(evt=>{if (float.IsNaN(evt.newValue)||float.IsInfinity(evt.newValue)) return; _binSeconds=Mathf.Clamp(evt.newValue,0.1f,3600);Refresh();}); row.Add(bin);
            var metric=new EnumField("Metric",_metric); metric.RegisterValueChangedCallback(evt=>{_metric=(GameDebugMetric)evt.newValue;Refresh();}); _body.Add(metric);
            AddFilter("Enemy type",_enemyFilter,value=>_enemyFilter=value);
            AddFilter("SpawnPoint ID",_pointFilter,value=>_pointFilter=value);
            AddFilter("Squad preset",_presetFilter,value=>_presetFilter=value);
            var origins=new List<string>{"All"}; origins.AddRange(Enum.GetNames(typeof(EncounterRequestSource)));
            var source=new DropdownField("Request source",origins,_sourceFilter); source.RegisterValueChangedCallback(evt=>{_sourceFilter=evt.newValue;Refresh();}); _body.Add(source);
            _reference=new DropdownField("B · Compare",new List<string>{"None"},"None");
            _reference.RegisterValueChangedCallback(evt=>{if (!_refreshing) {_referenceRun=evt.newValue;Refresh();}}); _body.Add(_reference);
            _chart=new GameDebugChart(); _chart.AddToClassList("chart"); _body.Add(_chart);
            _axes=new Label(); _axes.AddToClassList("chart-axes"); _body.Add(_axes);
            _body.Add(new Label("수량: 사실 이벤트. Alive/Power/Ratio/Area/Length: Match 전체 표본 (필터 미적용).\n종류/지점 필터 시 Request 집계는 N/A. 빈 표본은 0으로 연결하지 않습니다. Bin은 최대 2,048개로 제한됩니다."){style={whiteSpace=WhiteSpace.Normal}});
        }
        private void AddFilter(string label,string value,Action<string> apply)
        { var field=new TextField(label){value=value,isDelayed=true}; field.RegisterValueChangedCallback(evt=>{apply(evt.newValue);Refresh();});_body.Add(field); }
        private void BuildCapture()
        {
            _body.Add(new IMGUIContainer(BattlefieldDiagnosticPanels.DrawCapture));
            _body.Add(new Button(()=>BattlefieldDebugSession.ApplyPreset("Capture")){text="Scene Capture preset"});
            _body.Add(new Button(()=>Export("json")){text="Export run JSON + metadata"});
            _body.Add(new Button(()=>Export("events.csv")){text="Export events CSV"});
            _body.Add(new Button(()=>Export("samples.csv")){text="Export samples CSV"});
            _body.Add(new Button(Import){text="Import read-only comparison JSON"});
            _body.Add(new Button(()=> {
                if (GameDebuggerSession.Current!=null) GameDebuggerSession.Current.BuildLabel=BattlefieldDebugSession.Display.CaptureLabel;
                Refresh();
            }){text="Stamp build/commit label on live record"});
            _body.Add(new Label("Import/Compare는 관측 비교이며 게임 상태 복원/결정론적 리플레이가 아닙니다.\n캡처 preset은 Scene 표시만 바꿉니다. 게임 시간 제어는 상단의 명시적 명령입니다."){style={whiteSpace=WhiteSpace.Normal}});
        }
        private void Refresh()
        { _pendingRefresh=true; }
        private void UpdateView()
        {
            if (!_pendingRefresh || EditorApplication.timeSinceStartup<_nextUiRefresh) return;
            _pendingRefresh=false; _nextUiRefresh=EditorApplication.timeSinceStartup+0.25;
            RefreshView();
        }
        private void RefreshView()
        {
            if (_header==null) return;
            _refreshing=true;
            try
            {
                var source=GameDebuggerSession.Source;
                _header.text=$"GAME DEBUGGER  ·  {BattlefieldDebugSession.Mode}  ·  {(source==null?"authority N/A":source.IsServer?"SERVER":"CLIENT/OFFLINE")}  ·  game {source?.GameSeconds ?? 0:F1}s / real {source?.RealSeconds ?? 0:F1}s";
                _footer.text=_fileStatus ?? (GameDebuggerSession.Warnings.Count==0 ? "READ-ONLY diagnostics · explicit commands · local Editor pause only" : string.Join(" | ",GameDebuggerSession.Warnings.Take(3)));
                var choices=RunChoices();
                if (_runs!=null) { _runs.choices=choices; if (!choices.Contains(_selectedRun)) _selectedRun="LIVE"; _runs.SetValueWithoutNotify(_selectedRun); }
                var run=SelectedRun();
                if (_recordStatus!=null) _recordStatus.text=run==null ? "NO RECORD · Play Mode에서 Match를 시작하세요." :
                    $"{(ReferenceEquals(run,GameDebuggerSession.Current)?"LIVE":_selectedRun=="IMPORTED"?"IMPORTED / READ-ONLY":"ARCHIVE / READ-ONLY")} · {run.MatchId}\n{run.Scene} · {run.BuildLabel} · seed {(run.HasSeed?run.RandomSeed.ToString():"N/A")} · {(run.IsPartial?"PARTIAL":"event coverage from Match start")} · {(run.IsComplete?"ENDED":"end marker not received")}";
                if (_chart!=null)
                {
                    var references=new List<string>{"None"}; references.AddRange(choices);
                    _reference.choices=references; if (!references.Contains(_referenceRun)) _referenceRun="None"; _reference.SetValueWithoutNotify(_referenceRun);
                    EncounterRequestSource? origin=Enum.TryParse(_sourceFilter,out EncounterRequestSource parsed)?parsed:(EncounterRequestSource?)null;
                    var reference=_referenceRun=="None"?null:Resolve(_referenceRun);
                    // 같은 bin 폭을 사용하고 같은 x축으로 비교합니다. 설정 차이는 숨기지 않습니다.
                    double duration=0;
                    foreach (var item in new[]{run,reference}) if (item!=null)
                    { foreach(var e in item.Events) duration=Math.Max(duration,_realClock?e.RealSeconds:e.GameSeconds); foreach(var s in item.Samples) duration=Math.Max(duration,_realClock?s.RealSeconds:s.GameSeconds); }
                    double width=Math.Max(_binSeconds,(duration+0.0001)/2048);
                    bool invalidRequestFilter=(!string.IsNullOrEmpty(_enemyFilter)||!string.IsNullOrEmpty(_pointFilter)) && (_metric==GameDebugMetric.Planned||_metric==GameDebugMetric.Executed||_metric==GameDebugMetric.Failures);
                    _chart.SetData(invalidRequestFilter?null:GameDebugStatistics.Build(run,width,_realClock,_enemyFilter,_pointFilter,_presetFilter,origin),
                        invalidRequestFilter?null:GameDebugStatistics.Build(reference,width,_realClock,_enemyFilter,_pointFilter,_presetFilter,origin),_metric);
                    _axes.text=invalidRequestFilter ? "N/A: Request 결과에는 Enemy type/SpawnPoint별 배분 계약이 없습니다." : $"{(_realClock?"REAL":"GAME")} seconds · bin {width:F2}s · {_chart.AxisDescription}";
                    if (reference!=null && run!=null && (run.GridDescription!=reference.GridDescription || run.RandomSeed!=reference.RandomSeed || run.ContentVersion!=reference.ContentVersion || run.Scene!=reference.Scene))
                        _axes.text+="\n⚠ A/B Scene, Grid, seed 또는 content 설정이 다릅니다. 동일 조건의 재현으로 해석하지 마세요.";
                }
                Repaint();
            }
            finally { _refreshing=false; }
        }
        private void Export(string type)
        {
            var run=SelectedRun(); if (run==null) { _fileStatus="내보낼 기록이 없습니다."; Refresh(); return; }
            string path=EditorUtility.SaveFilePanel("Export Game Debugger record","", "game-debug-"+run.MatchId+(type=="json"?"":"-"+type.Split('.')[0]),type=="json"?"json":"csv");
            if (string.IsNullOrEmpty(path)) return;
            try { File.WriteAllText(path,type=="json"?GameDebugExport.ToJson(run):type=="events.csv"?GameDebugExport.EventsCsv(run):GameDebugExport.SamplesCsv(run),new System.Text.UTF8Encoding(true)); _fileStatus="Exported: "+path; }
            catch (Exception exception) { _fileStatus="Export failed: "+exception.Message; }
            Refresh();
        }
        private void Import()
        {
            string path=EditorUtility.OpenFilePanel("Import comparison JSON","","json"); if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (new FileInfo(path).Length>16*1024*1024) throw new ArgumentException("파일이 16MiB 제한을 넘었습니다.");
                GameDebuggerSession.SetImported(GameDebugExport.FromJson(File.ReadAllText(path))); _referenceRun="IMPORTED"; _fileStatus="Imported read-only: "+path;
            }
            catch (Exception exception) { _fileStatus="Import failed: "+exception.Message; }
            Refresh();
        }
    }
}
