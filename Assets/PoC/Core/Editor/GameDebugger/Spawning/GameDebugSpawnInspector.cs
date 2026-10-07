// 스폰 요청을 선택해 당시 판단·후보·계획·실제 위치를 구조화된 표로 조회합니다.
// 기록 선택은 게임 실행과 분리되며 Scene 선택은 같은 활성 Match에만 허용됩니다.
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugSpawnInspector : VisualElement
    {
        private GameDebugRun _run;
        private GameDebugSpawnDecision _decision;
        private readonly DropdownField _requests;
        private readonly Toggle _follow;
        private readonly TextField _filter;
        private readonly DropdownField _monsterFilter;
        private readonly Label _summary,_selection,_warning;
        private readonly GameDebugSpawnMap _map;
        private readonly VisualElement _candidates,_plans,_actuals;
        private readonly Button _selectScene;
        private string _selectedPoint="";
        private string _selectedEntity="";
        private readonly List<GameDebugSpawnDecision> _choices=new List<GameDebugSpawnDecision>();
        private int _previousCount=-1;
        private long _lastSequence=-1;
        public GameDebugSpawnInspector()
        {
            name="spawn-inspector";
            AddToClassList("spawn-inspector");
            var bar=new VisualElement();bar.style.flexDirection=FlexDirection.Row;bar.style.flexWrap=Wrap.Wrap;
            _requests=new DropdownField("Request",new List<string>{"No snapshots"},0);_requests.style.flexGrow=1;_requests.style.minWidth=240;
            _requests.RegisterValueChangedCallback(e=>{
                int index=_requests.choices.IndexOf(e.newValue);
                if(index>=0&&index<_choices.Count){_follow.SetValueWithoutNotify(false);Show(_choices[index]);}
            });
            _follow=new Toggle("Follow latest"){value=true};_follow.RegisterValueChangedCallback(_=>RefreshChoices(true));
            bar.Add(_requests);bar.Add(_follow);Add(bar);
            _monsterFilter=new DropdownField("Monster",new List<string>{"All"},0);
            _monsterFilter.RegisterValueChangedCallback(_=>{BuildResults();_map.SetMonsterFilter(_monsterFilter.value);});Add(_monsterFilter);
            _summary=new Label();_summary.style.whiteSpace=WhiteSpace.Normal;_summary.style.unityFontStyleAndWeight=FontStyle.Bold;_summary.style.marginTop=8;Add(_summary);
            _warning=new Label();_warning.style.whiteSpace=WhiteSpace.Normal;_warning.style.marginBottom=8;Add(_warning);
            var geometry=new Foldout{text="Decision snapshot · XY",value=true};geometry.AddToClassList("debug-section");
            var tools=new VisualElement();tools.style.flexDirection=FlexDirection.Row;
            tools.Add(new Button(()=>_map.FrameAll()){text="Frame snapshot"});
            _selectScene=new Button(SelectScene){text="Select point in Scene"};tools.Add(_selectScene);geometry.Add(tools);
            geometry.Add(new Label("Gold: frontline · Blue: allocated point · Red: excluded/unallocated · Green: actual spawn"));
            _map=new GameDebugSpawnMap();_map.PointSelected+=SelectPoint;_map.ActualSelected+=SelectActual;geometry.Add(_map);
            var bounds=new Label();bounds.name="snapshot-bounds";bounds.style.whiteSpace=WhiteSpace.Normal;geometry.Add(bounds);
            _selection=new Label();_selection.style.whiteSpace=WhiteSpace.Normal;_selection.style.marginTop=8;geometry.Add(_selection);Add(geometry);
            var candidates=new Foldout{text="Candidates · eligibility and allocation",value=true};candidates.AddToClassList("debug-section");
            _filter=new TextField("Search point / reason");_filter.RegisterValueChangedCallback(_=>BuildCandidates());candidates.Add(_filter);
            _candidates=new VisualElement();candidates.Add(_candidates);Add(candidates);
            var plans=new Foldout{text="Planned composition · actual result",value=true};plans.AddToClassList("debug-section");
            _plans=new VisualElement{name="spawn-composition"};plans.Add(_plans);Add(plans);
            var actuals=new Foldout{text="Actual objects · immutable spawn positions",value=false};actuals.AddToClassList("debug-section");
            _actuals=new VisualElement();actuals.Add(_actuals);Add(actuals);
            SetRun(null);
        }
        public void SetRun(GameDebugRun run)
        {
            bool changed=!ReferenceEquals(_run,run);
            _run=run;
            int count=run?.SpawnDecisions?.Count??0;
            long sequence=count>0?run.SpawnDecisions[count-1].Sequence:-1;
            if(changed||count!=_previousCount||sequence!=_lastSequence)RefreshChoices(changed);
            _previousCount=count;_lastSequence=sequence;
            _selectScene.SetEnabled(CanSelectScene());
        }
        public bool SelectRequest(long requestId)
        {
            var decision=_run?.SpawnDecisions?.FirstOrDefault(d=>d.RequestId==requestId);
            if(decision==null)return false;
            _follow.SetValueWithoutNotify(false);
            Show(decision);
            int index=_choices.FindIndex(d=>d.RequestId==requestId);
            if(index>=0)_requests.SetValueWithoutNotify(_requests.choices[index]);
            return true;
        }
        private void RefreshChoices(bool force)
        {
            long selected=_decision?.RequestId??-1;
            _choices.Clear();
            if(_run?.SpawnDecisions!=null)_choices.AddRange(_run.SpawnDecisions.AsEnumerable().Reverse());
            _requests.choices=_choices.Select(d=>$"#{d.RequestId} · {d.GameSeconds:F1}s · {d.Source} · {d.Preset} · {d.Planned} → {d.Spawned} · r{d.Revision}").ToList();
            if(_choices.Count==0) {
                _requests.choices=new List<string>{"No captured decision snapshots"};_requests.SetValueWithoutNotify(_requests.choices[0]);Show(null);return;
            }
            int index=_follow.value||force?0:Math.Max(0,_choices.FindIndex(d=>d.RequestId==selected));
            _requests.SetValueWithoutNotify(_requests.choices[index]);Show(_choices[index]);
        }
        private void Show(GameDebugSpawnDecision decision)
        {
            _decision=decision;
            _selectedEntity="";
            _selectedPoint=decision?.Candidates.FirstOrDefault(c=>c.Sets>0)?.Id??"";
            _summary.text=decision==null?"No spawn decision captured":$"Request #{decision.RequestId}  ·  planned {decision.Planned}  /  spawned {decision.Spawned}  ·  {decision.Outcome}";
            _warning.text=decision==null?"이전 기록 또는 계획 전 거부 요청은 공간 판단 자료를 포함하지 않습니다. 이후 요청부터 기록됩니다.":
                decision.Policy+(_run.DroppedSpawnDecisions>0?$"\n오래된 요청 {_run.DroppedSpawnDecisions}개는 보존 상한으로 제거되었습니다.":"")+
                (decision.DroppedItems>0?$"\n이 요청의 상세 항목 {decision.DroppedItems}개가 관측 상한으로 생략되었습니다.":"");
            var g=decision?.Grid;
            this.Q<Label>("snapshot-bounds").text=g==null?"":$"X [{g.CenterX-g.Width*.5:F1}, {g.CenterX+g.Width*.5:F1}]  /  Y [{g.CenterY-g.Height*.5:F1}, {g.CenterY+g.Height*.5:F1}]  ·  Grid {g.CellsX} × {g.CellsY}";
            _map.SetData(decision,_selectedPoint);
            string monster=_monsterFilter.value;
            _monsterFilter.choices=new List<string>{"All"};
            if(decision!=null)_monsterFilter.choices.AddRange(decision.Plans.Select(p=>p.Enemy).Distinct().OrderBy(v=>v));
            _monsterFilter.SetValueWithoutNotify(_monsterFilter.choices.Contains(monster)?monster:"All");
            _map.SetMonsterFilter(_monsterFilter.value);
            BuildCandidates();BuildResults();UpdateSelection();
        }
        private void BuildCandidates()
        {
            _candidates.Clear();if(_decision==null)return;
            string search=_filter.value??"";
            var rows=_decision.Candidates.Where(c=>(c.Id+" "+c.Reason).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
                .Select(c=>new Row(c.Id,new[]{c.Id,c.CellX<0?"N/A":$"({c.CellX}, {c.CellY})",c.CellX<0?"N/A":c.Enemies.ToString(),c.HasFrontline?c.Distance.ToString("F2"):"N/A",c.Sets.ToString(),c.Reason})).ToList();
            _candidates.Add(Table(new[]{"Point","Grid cell","Enemies","Frontline units","Sets","Decision"},new[]{150,90,70,100,60,290},rows,row=>SelectPoint(row.Key),200));
        }
        private void BuildResults()
        {
            _plans.Clear();_actuals.Clear();if(_decision==null)return;
            var plans=_decision.Plans.Where(p=>_monsterFilter.value=="All"||p.Enemy==_monsterFilter.value)
                .GroupBy(p=>new{p.Point,p.Enemy}).Select(group=>{
                int planned=group.Sum(p=>p.Count);
                int actual=_decision.Actuals.Count(a=>a.Point==group.Key.Point&&a.Enemy==group.Key.Enemy);
                string reasons=string.Join("; ",group.Select(p=>p.ExecutionReason).Where(v=>!string.IsNullOrEmpty(v)).Distinct());
                return new Row(group.Key.Point,new[]{group.Key.Point,group.Key.Enemy,planned.ToString(),actual.ToString(),
                    actual==planned?"Complete":_decision.DroppedActuals>0?"Partial capture":"Not fully spawned",reasons});
            }).ToList();
            _plans.Add(Table(new[]{"Point","Monster","Planned","Captured actual","Result","Execution rejection"},new[]{150,150,90,120,180,280},plans,row=>SelectPoint(row.Key),180));
            var actuals=_decision.Actuals.Where(a=>_monsterFilter.value=="All"||a.Enemy==_monsterFilter.value)
                .Select(a=>new Row(a.Entity,new[]{a.Entity,a.Enemy,a.Point,a.X.ToString("F2"),a.Y.ToString("F2")})).ToList();
            _actuals.Add(Table(new[]{"Network ID","Monster","Point","World X","World Y"},new[]{100,150,150,100,100},actuals,row=>SelectActual(row.Key),200));
        }
        private void SelectPoint(string id){_selectedEntity="";_selectedPoint=id??"";_map.SetData(_decision,_selectedPoint);UpdateSelection();}
        private void SelectActual(string entity)
        {
            var actual=_decision.Actuals.FirstOrDefault(a=>a.Entity==entity);
            if(actual==null)return;
            SelectPoint(actual.Point);_selectedEntity=entity;
            _selection.text+=$"\nObject {actual.Entity} · {actual.Enemy} · spawned XY ({actual.X:F2}, {actual.Y:F2})";
            _selectScene.text="Select object in Scene";
        }
        private void UpdateSelection()
        {
            var c=_decision?.Candidates.FirstOrDefault(v=>v.Id==_selectedPoint);
            _selection.text=c==null?"Select a candidate row or a map point.":$"{c.Id}  ·  World XY ({c.X:F2}, {c.Y:F2})\n{c.Reason} · initial cell enemies {(c.CellX<0?"N/A":c.Enemies.ToString())} · assigned sets {c.Sets}"+
                (c.HasFrontline?$" · frontline distance {c.Distance:F2}":" · no frontline distance criterion");
            _selectScene.SetEnabled(CanSelectScene());
            _selectScene.text="Select point in Scene";
        }
        private bool CanSelectScene()=>_decision!=null&&_selectedPoint.Length>0&&ReferenceEquals(_run,GameDebuggerSession.Current)&&
            _run.MatchId==_decision.MatchId;
        private void SelectScene()
        {
            if(!CanSelectScene())return;
            // Editor assembly는 Enemy 구현 assembly를 참조하지 않고 공개 ID 계약으로만 선택합니다.
            var matches=UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
                .Where(c=>c!=null&&c.gameObject.scene==UnityEngine.SceneManagement.SceneManager.GetActiveScene()&&
                    (_selectedEntity.Length>0 ? c.GetType().Name=="NetworkObject"&&
                        c.GetType().GetProperty("NetworkObjectId")?.GetValue(c)?.ToString()==_selectedEntity :
                    c.GetType().Name=="SpawnPoint"&&(c.GetType().GetProperty("SpawnPointId")?.GetValue(c) as string)==_selectedPoint)).ToArray();
            if(matches.Length!=1){_selection.text+="\nScene 지점이 없거나 ID가 중복입니다.";return;}
            Selection.activeGameObject=matches[0].gameObject;
            SceneView.lastActiveSceneView?.FrameSelected();
        }
        private sealed class Row
        {
            public string Key;public string[] Values;
            public Row(string key,string[] values){Key=key;Values=values;}
        }
        private static VisualElement Table(string[] headers,int[] widths,List<Row> rows,Action<Row> select,int height)
        {
            var scroll=new ScrollView(ScrollViewMode.Horizontal);scroll.style.height=height+26;scroll.AddToClassList("debug-table");
            int width=widths.Sum();
            var container=new VisualElement();container.style.width=width;
            var head=new VisualElement();head.style.flexDirection=FlexDirection.Row;head.style.height=24;
            head.style.backgroundColor=EditorGUIUtility.isProSkin?new Color(.22f,.22f,.22f):new Color(.75f,.75f,.75f);
            for(int i=0;i<headers.Length;i++){var label=new Label(headers[i]);label.style.width=widths[i];label.style.paddingLeft=6;label.style.unityFontStyleAndWeight=FontStyle.Bold;head.Add(label);}container.Add(head);
            var list=new ListView(rows,24,()=>{
                var element=new VisualElement();element.style.flexDirection=FlexDirection.Row;
                for(int i=0;i<headers.Length;i++){var label=new Label();label.style.width=widths[i];label.style.paddingLeft=6;label.style.overflow=Overflow.Hidden;label.style.whiteSpace=WhiteSpace.NoWrap;element.Add(label);}return element;
            },(element,index)=>{
                for(int i=0;i<rows[index].Values.Length;i++){((Label)element[i]).text=rows[index].Values[i];((Label)element[i]).tooltip=rows[index].Values[i];}
            });
            list.showAlternatingRowBackgrounds=AlternatingRowBackground.All;list.style.height=height;list.selectionType=SelectionType.Single;
            list.selectionChanged+=selection=>{var row=selection.OfType<Row>().FirstOrDefault();if(row!=null)select(row);};
            container.Add(list);scroll.Add(container);return scroll;
        }
    }
}
