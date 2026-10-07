// 한 작업 영역의 기능 선택·필터·차트·스크롤을 독립적으로 관리하는 UI 뷰입니다.
// 모든 영역은 같은 진단 Session을 조회하며 Match나 수집기를 새로 만들지 않습니다.
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
    public sealed class GameDebuggerPane : VisualElement, IDisposable
    {
        public static IReadOnlyList<string> PageNames { get; } = Array.AsReadOnly(new[] {
            "Overview", "Battlefield", "Encounter", "Statistics", "Timeline", "Capture / Compare" });
        private readonly GameDebuggerPaneState _state;
        private readonly Action _requestRefresh;
        private readonly Action<string> _reportStatus;
        private readonly BattlefieldDebugPanel _battlefieldPanel;
        private readonly ScrollView _body;
        private readonly DropdownField _pageSelector;
        private readonly bool _isExpandedGraph;
        private GameDebugChart _chart;
        private GameDebugSpawnInspector _spawnInspector;
        private Label _axes, _recordStatus;
        private DropdownField _runs, _reference;
        private bool _refreshing;
        private int _builtPage;
        private bool _restoringScroll;
        private IVisualElementScheduledItem _scrollRestore;
        private string FileStatus { set => _reportStatus(value); }
        public int Index { get; }
        public GameDebuggerPaneState State => _state;

        public GameDebuggerPane(int index, GameDebuggerPaneState state, Action requestRefresh,
            Action<int> activate, Action<string> reportStatus, bool isExpandedGraph=false)
        {
            Index=index; _state=state; _requestRefresh=requestRefresh; _reportStatus=reportStatus;
            _isExpandedGraph=isExpandedGraph;
            _state.Validate();
            _battlefieldPanel=_state.BattlefieldPanel;
            _battlefieldPanel.Attach();
            AddToClassList("debug-pane");
            name="debug-pane-"+index;
            var toolbar=new VisualElement(); toolbar.AddToClassList("pane-toolbar"); Add(toolbar);
            toolbar.Add(new Label("P"+(index+1)) { tooltip="클릭한 영역이 왼쪽 탐색 메뉴의 대상입니다." });
            _pageSelector=new DropdownField(PageNames.ToList(),_state.Page);
            _pageSelector.AddToClassList("pane-selector");
            if (_isExpandedGraph) _pageSelector.SetEnabled(false);
            _pageSelector.RegisterValueChangedCallback(evt=> {
                int next=PageNames.ToList().IndexOf(evt.newValue);
                if (next>=0) { activate(Index); SelectPage(next); }
            });
            toolbar.Add(_pageSelector);
            _body=new ScrollView();
            _body.AddToClassList("content"); Add(_body);
            _body.RegisterCallback<GeometryChangedEvent>(evt=> {
                if (_isExpandedGraph && _chart!=null) _chart.style.height=Mathf.Max(330,evt.newRect.height-155);
            });
            _body.horizontalScroller.valueChanged+=_=>SaveScroll();
            _body.verticalScroller.valueChanged+=_=>SaveScroll();
            RegisterCallback<PointerDownEvent>(_=>activate(Index),TrickleDown.TrickleDown);
            RegisterCallback<FocusInEvent>(_=>activate(Index));
            BuildPage();
        }
        public void SelectPage(int page)
        {
            if (page<0 || page>=PageNames.Count) throw new ArgumentOutOfRangeException(nameof(page));
            SaveScroll(); _state.Page=page; BuildPage(); _requestRefresh();
        }
        public void SetActive(bool isActive) => EnableInClassList("active-pane",isActive);
        public void Dispose() { SaveScroll(); _scrollRestore?.Pause(); _battlefieldPanel.Dispose(); }
        private void SaveScroll()
        {
            if (!_restoringScroll && _builtPage>=0 && _builtPage<_state.ScrollOffsets.Length)
                _state.ScrollOffsets[_builtPage]=_body.scrollOffset;
        }
        private void RestoreScroll()
        {
            _restoringScroll=true;
            Vector2 offset=_state.ScrollOffsets[_builtPage];
            _body.scrollOffset=offset;
            _scrollRestore=_body.schedule.Execute(()=> {
                _body.scrollOffset=offset;
                _restoringScroll=false;
            });
        }
        private void BuildPage()
        {
            if (_body==null) return;
            // 재배치 중 스크롤 보정은 저장하지 않고 이전 기능의 예약 복원도 취소합니다.
            _restoringScroll=true;
            _scrollRestore?.Pause();
            _body.Clear(); _chart=null; _spawnInspector=null; _runs=_reference=null; _axes=_recordStatus=null;
            _pageSelector.SetValueWithoutNotify(PageNames[Mathf.Clamp(_state.Page,0,PageNames.Count-1)]);
            _builtPage=_state.Page;
            if (_state.Page==0) _body.Add(new IMGUIContainer(()=>GameDebugInspectionPanels.DrawOverview(_state.Monitor)));
            if (_state.Page==1) _body.Add(new IMGUIContainer(()=> { _battlefieldPanel?.Draw(1); GameDebugInspectionPanels.DrawTurrets(_state.Monitor); }));
            if (_state.Page==2) _body.Add(new IMGUIContainer(()=> { _battlefieldPanel?.Draw(2); GameDebugInspectionPanels.DrawRequests(GameDebuggerSession.Current,_state.Monitor); }));
            if (_state.Page==3 || _state.Page==4 || _state.Page==5) BuildRecordSelector();
            if (_state.Page==3 || _state.Page==5) BuildStatistics();
            if (_state.Page==4)
            {
                var search=new TextField("Search source / kind / text"){value=_state.Search}; search.RegisterValueChangedCallback(evt=>_state.Search=evt.newValue); _body.Add(search);
                var bookmark=new TextField("Bookmark"){value=_state.Bookmark}; bookmark.RegisterValueChangedCallback(evt=>_state.Bookmark=evt.newValue); _body.Add(bookmark);
                _body.Add(new IMGUIContainer(()=>GameDebugInspectionPanels.DrawTimeline(SelectedRun(),_state.Search,_state.Bookmark,_state.Monitor)));
            }
            if (_state.Page==5) BuildCapture();
            RestoreScroll();
            RefreshView();
        }
        private void BuildRecordSelector()
        {
            _runs=new DropdownField("A · Record",RunChoices(),_state.SelectedRun);
            _runs.RegisterValueChangedCallback(evt=> { if (!_refreshing) { _state.SelectedRun=evt.newValue; Refresh(); } }); _body.Add(_runs);
            _recordStatus=new Label(); _recordStatus.AddToClassList("record-status"); _body.Add(_recordStatus);
        }
        private List<string> RunChoices()
        {
            var choices=new List<string>{"LIVE"}; choices.AddRange(GameDebuggerSession.Recorder.Runs.Select(item=>item.MatchId));
            if (GameDebuggerSession.Imported!=null) choices.Add("IMPORTED"); return choices;
        }
        private GameDebugRun SelectedRun() => Resolve(_state.SelectedRun);
        private static GameDebugRun Resolve(string id) => id=="LIVE" ? GameDebuggerSession.Current : id=="IMPORTED" ? GameDebuggerSession.Imported : GameDebuggerSession.Recorder.Find(id);
        private void BuildStatistics()
        {
            var row=new VisualElement(); row.AddToClassList("filter-row"); _body.Add(row);
            var view=new DropdownField("View",new List<string>{"Time series","Spawn decisions / distribution"},_state.IsSpawnInspector?1:0);
            view.RegisterValueChangedCallback(evt=>{_state.IsSpawnInspector=evt.newValue!="Time series"; SaveScroll(); BuildPage();}); row.Add(view);
            row.Add(new Button(()=>GameDebugStatisticsWindow.Open(_state)){text="Open in window",tooltip="같은 기록·필터 설정으로 큰 통계 창을 엽니다. Match/수집기는 공유합니다."});
            if (_state.IsSpawnInspector)
            {
                _spawnInspector=new GameDebugSpawnInspector(); _body.Add(_spawnInspector); return;
            }
            var settings=new Foldout{text="Chart settings / filters",value=_state.IsChartSettingsExpanded};
            settings.RegisterValueChangedCallback(evt=>_state.IsChartSettingsExpanded=evt.newValue);
            settings.AddToClassList("debug-section"); _body.Add(settings);
            var clock=new Toggle("Real clock"){value=_state.IsRealClock}; clock.RegisterValueChangedCallback(evt=>{_state.IsRealClock=evt.newValue;Refresh();}); settings.Add(clock);
            var bin=new FloatField("Bin seconds"){value=_state.BinSeconds}; bin.RegisterValueChangedCallback(evt=>{if (float.IsNaN(evt.newValue)||float.IsInfinity(evt.newValue)) return; _state.BinSeconds=Mathf.Clamp(evt.newValue,0.1f,3600);Refresh();}); settings.Add(bin);
            var traffic=new Toggle("Requests / Spawned / Died together"){value=_state.ChartViewport.IsTrafficView};
            var metric=new EnumField("Single metric",_state.Metric); metric.SetEnabled(!traffic.value);
            traffic.RegisterValueChangedCallback(evt=>{_state.ChartViewport.IsTrafficView=evt.newValue; metric.SetEnabled(!evt.newValue);Refresh();}); settings.Add(traffic);
            metric.RegisterValueChangedCallback(evt=>{_state.Metric=(GameDebugMetric)evt.newValue;Refresh();}); settings.Add(metric);
            AddFilter(settings,"Enemy type",_state.EnemyFilter,value=>_state.EnemyFilter=value);
            AddFilter(settings,"SpawnPoint ID",_state.PointFilter,value=>_state.PointFilter=value);
            AddFilter(settings,"Squad preset",_state.PresetFilter,value=>_state.PresetFilter=value);
            var origins=new List<string>{"All"}; origins.AddRange(Enum.GetNames(typeof(EncounterRequestSource)));
            var source=new DropdownField("Request source",origins,_state.SourceFilter); source.RegisterValueChangedCallback(evt=>{_state.SourceFilter=evt.newValue;Refresh();}); settings.Add(source);
            _reference=new DropdownField("B · Compare",new List<string>{"None"},"None");
            _reference.RegisterValueChangedCallback(evt=>{if (!_refreshing) {_state.ReferenceRun=evt.newValue;Refresh();}}); settings.Add(_reference);
            _chart=new GameDebugChart(_state.ChartViewport); _chart.AddToClassList("chart"); _body.Add(_chart);
            _axes=new Label(); _axes.AddToClassList("chart-axes"); _body.Add(_axes);
            var notes=new Foldout{text="Data semantics / N/A",value=_state.IsChartNotesExpanded};
            notes.RegisterValueChangedCallback(evt=>_state.IsChartNotesExpanded=evt.newValue); notes.AddToClassList("debug-section");
            notes.Add(new Label("요청은 횟수, 생성·사망·제거는 객체 수/bin입니다. 사망은 실제 HP 사망 통지만 집계하고 제거와 구분합니다.\nAlive/Power/Ratio/Area/Length는 Match 전체 표본(필터 미적용). 종류/지점 필터 시 Request 집계는 N/A.\n빈 표본과 사망 계약 없는 과거 기록은 N/A이며 0으로 연결하지 않습니다. Bin 최대 2,048개."){style={whiteSpace=WhiteSpace.Normal}}); _body.Add(notes);
        }
        private void AddFilter(VisualElement parent,string label,string value,Action<string> apply)
        { var field=new TextField(label){value=value,isDelayed=true}; field.RegisterValueChangedCallback(evt=>{apply(evt.newValue);Refresh();});parent.Add(field); }
        private void BuildCapture()
        {
            var capture=new Foldout{text="Scene capture / metadata",value=false}; capture.AddToClassList("debug-section"); _body.Add(capture);
            capture.Add(new IMGUIContainer(BattlefieldDiagnosticPanels.DrawCapture));
            capture.Add(new Button(()=>BattlefieldDebugSession.ApplyPreset("Capture")){text="Scene Capture preset"});
            var files=new Foldout{text="Record export / import",value=false}; files.AddToClassList("debug-section"); _body.Add(files);
            files.Add(new Button(()=>Export("json")){text="Export run JSON + metadata"});
            files.Add(new Button(()=>Export("events.csv")){text="Export events CSV"});
            files.Add(new Button(()=>Export("samples.csv")){text="Export samples CSV"});
            files.Add(new Button(Import){text="Import read-only comparison JSON"});
            capture.Add(new Button(()=> {
                if (GameDebuggerSession.Current!=null) GameDebuggerSession.Current.BuildLabel=BattlefieldDebugSession.Display.CaptureLabel;
                Refresh();
            }){text="Stamp build/commit label on live record"});
            files.Add(new Label("Import/Compare는 관측 비교이며 게임 상태 복원/결정론적 리플레이가 아닙니다."){style={whiteSpace=WhiteSpace.Normal}});
        }
        private void Refresh()
        { _requestRefresh(); }
        public void RefreshView()
        {
            if (_body==null) return;
            _refreshing=true;
            try
            {
                var choices=RunChoices();
                if (_runs!=null) { _runs.choices=choices; if (!choices.Contains(_state.SelectedRun)) _state.SelectedRun="LIVE"; _runs.SetValueWithoutNotify(_state.SelectedRun); }
                var run=SelectedRun();
                if (_recordStatus!=null)
                {
                    _recordStatus.text=run==null ? "NO RECORD · Play Mode에서 Match를 시작하세요." :
                        $"{(ReferenceEquals(run,GameDebuggerSession.Current)?"LIVE":_state.SelectedRun=="IMPORTED"?"IMPORTED":"ARCHIVE")} · {run.Scene} · {run.Events.Count:N0} events / {run.Samples.Count:N0} samples · {(run.IsPartial?"PARTIAL":"FULL")}";
                    _recordStatus.tooltip=run==null?"":$"Match: {run.MatchId}\nBuild: {run.BuildLabel}\nseed: {(run.HasSeed?run.RandomSeed.ToString():"N/A")} · {(run.IsComplete?"ENDED":"end marker not received")}";
                }
                _spawnInspector?.SetRun(run);
                if (_chart!=null)
                {
                    var references=new List<string>{"None"}; references.AddRange(choices);
                    _reference.choices=references; if (!references.Contains(_state.ReferenceRun)) _state.ReferenceRun="None"; _reference.SetValueWithoutNotify(_state.ReferenceRun);
                    EncounterRequestSource? origin=Enum.TryParse(_state.SourceFilter,out EncounterRequestSource parsed)?parsed:(EncounterRequestSource?)null;
                    var reference=_state.ReferenceRun=="None"?null:Resolve(_state.ReferenceRun);
                    // 같은 bin 폭을 사용하고 같은 x축으로 비교합니다. 설정 차이는 숨기지 않습니다.
                    double duration=0;
                    foreach (var item in new[]{run,reference}) if (item!=null)
                    { foreach(var e in item.Events) duration=Math.Max(duration,_state.IsRealClock?e.RealSeconds:e.GameSeconds); foreach(var s in item.Samples) duration=Math.Max(duration,_state.IsRealClock?s.RealSeconds:s.GameSeconds); }
                    double width=Math.Max(_state.BinSeconds,(duration+0.0001)/2048);
                    bool invalidRequestFilter=!_state.ChartViewport.IsTrafficView && (!string.IsNullOrEmpty(_state.EnemyFilter)||!string.IsNullOrEmpty(_state.PointFilter)) && (_state.Metric==GameDebugMetric.Planned||_state.Metric==GameDebugMetric.Executed||_state.Metric==GameDebugMetric.Failures||_state.Metric==GameDebugMetric.Requests);
                    _chart.SetData(invalidRequestFilter?null:GameDebugStatistics.Build(run,width,_state.IsRealClock,_state.EnemyFilter,_state.PointFilter,_state.PresetFilter,origin),
                        invalidRequestFilter?null:GameDebugStatistics.Build(reference,width,_state.IsRealClock,_state.EnemyFilter,_state.PointFilter,_state.PresetFilter,origin),_state.Metric,_state.IsRealClock);
                    _axes.text=invalidRequestFilter ? "N/A: Request 결과에는 Enemy type/SpawnPoint별 배분 계약이 없습니다." :
                        $"{(_state.IsRealClock?"REAL":"GAME")} clock · {width:F2}s / bin"+(run!=null&&!run.HasDeathEvents?" · Death: N/A (legacy coverage)":"");
                    if (reference!=null && run!=null && (run.GridDescription!=reference.GridDescription || run.RandomSeed!=reference.RandomSeed || run.ContentVersion!=reference.ContentVersion || run.Scene!=reference.Scene))
                        _axes.text+="\n⚠ A/B Scene, Grid, seed 또는 content 설정이 다릅니다. 동일 조건의 재현으로 해석하지 마세요.";
                }
            }
            finally { _refreshing=false; }
        }
        private void Export(string type)
        {
            var run=SelectedRun(); if (run==null) { FileStatus="내보낼 기록이 없습니다."; Refresh(); return; }
            string path=EditorUtility.SaveFilePanel("Export Game Debugger record","", "game-debug-"+run.MatchId+(type=="json"?"":"-"+type.Split('.')[0]),type=="json"?"json":"csv");
            if (string.IsNullOrEmpty(path)) return;
            try { File.WriteAllText(path,type=="json"?GameDebugExport.ToJson(run):type=="events.csv"?GameDebugExport.EventsCsv(run):GameDebugExport.SamplesCsv(run),new System.Text.UTF8Encoding(true)); FileStatus="Exported: "+path; }
            catch (Exception exception) { FileStatus="Export failed: "+exception.Message; }
            Refresh();
        }
        private void Import()
        {
            string path=EditorUtility.OpenFilePanel("Import comparison JSON","","json"); if (string.IsNullOrEmpty(path)) return;
            try
            {
                if (new FileInfo(path).Length>16*1024*1024) throw new ArgumentException("파일이 16MiB 제한을 넘었습니다.");
                GameDebuggerSession.SetImported(GameDebugExport.FromJson(File.ReadAllText(path))); _state.ReferenceRun="IMPORTED"; FileStatus="Imported read-only: "+path;
            }
            catch (Exception exception) { FileStatus="Import failed: "+exception.Message; }
            Refresh();
        }

    }
}

