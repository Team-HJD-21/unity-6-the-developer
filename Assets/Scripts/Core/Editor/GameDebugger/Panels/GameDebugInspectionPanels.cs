// 실시간 핵심 수치는 계기 카드, 반복 기록은 선택 가능한 표와 상세 영역으로 표시합니다.
// 목록 스크롤·접힘·선택은 영역별 상태로 유지하고 게임 상태는 변경하지 않습니다.
using System;
using TeamHJD.Game.Bootstrap.SceneComposition;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.FieldTurrets;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class GameDebugInspectionPanels
    {
        private static InitialStageCompositionRoot _timeScene;
        public static void DrawOverview(GameDebugMonitorState view)
        {
            GameDebugPlatformPanel.Draw();
            GameDebugGameFlowPanel.Draw();
            var source=GameDebuggerSession.Source;
            var run=GameDebuggerSession.Current;
            var sample=run?.Samples.LastOrDefault();
            var snapshot=BattlefieldDebugSession.Snapshot;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("MATCH MONITOR",EditorStyles.boldLabel);
                EditorGUILayout.LabelField(BattlefieldDebugSession.Mode+" · "+(source==null?"NO SOURCE":source.IsServer?"SERVER":"CLIENT"),EditorStyles.miniLabel);
                using (new EditorGUILayout.HorizontalScope())
                {
                    Tile("생존",sample==null?"—":Unknown(sample.AliveEnemies),"현재 Match 서버 생존 객체 수");
                    Tile("전력",sample==null?"—":$"{Unknown(sample.AvailablePower)} / {Unknown(sample.MaximumPower)}","사용 가능한 전력 / 최대");
                    Tile("가동 터렛",sample==null?"—":$"{sample.OperationalTurrets} / {sample.TurretCount}","현재 가동 / 등록된 터렛");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    Tile("게임 시간",source==null?"—":source.GameSeconds.ToString("F1")+"s","현재 Match의 공용 게임 시간: Host 정지·배속을 반영합니다.");
                    Tile("실제 시간",source==null?"—":source.RealSeconds.ToString("F1")+"s","Editor 정지와 clock 정책은 가이드 참고");
                    Tile("Snapshot",snapshot==null?"—":"r"+snapshot.Revision,"최신 snapshot revision");
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    Tile("영역 면적",sample==null?"—":sample.TerritoryArea.ToString("F1"),"기하 면적 (world²), 우세도가 아닙니다.");
                    Tile("외곽 길이",sample==null?"—":sample.FrontlineLength.ToString("F1"),"기하 외곽 길이 (world)");
                    Tile("수집",run==null?"—":run.IsPartial?"PARTIAL":run.IsComplete?"ENDED":"LIVE","중간 수집/손실이 있으면 PARTIAL");
                }
            }
            if (_timeScene == null) _timeScene = UnityEngine.Object.FindAnyObjectByType<InitialStageCompositionRoot>();
            GameDebugCheckpointPanel.Draw(_timeScene);
            var matchTime = _timeScene == null ? null : _timeScene.MatchTime;
            var presence = _timeScene == null ? null : _timeScene.MatchPresence;
            if (matchTime != null) using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("필드 시간 · Host 기준", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("상태 / 배속", !matchTime.IsAvailable ? "시간 원본 미준비" :
                    $"{(matchTime.Snapshot.IsPaused ? "PAUSED" : "RUNNING")} / {matchTime.Snapshot.Rate:F2}×");
                if (presence != null) foreach (var participant in presence.Locations)
                    EditorGUILayout.LabelField(participant.Key.Value, participant.Value.ToString());
                EditorGUILayout.LabelField("우주선 전원 입장 시 필드만 정지합니다. Editor Pause는 전체 Editor 실행을 정지합니다.", EditorStyles.wordWrappedMiniLabel);
            }
            var environment = BattlefieldDebugSession.AppRoot == null ? null : BattlefieldDebugSession.AppRoot.CurrentEnvironment;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Match 환경 · 현재", EditorStyles.boldLabel);
                EditorGUILayout.LabelField("Revision", environment == null ? "미제공" : $"r{environment.Revision} · {environment.GameSeconds:F1}s");
                EditorGUILayout.LabelField("날씨 / 시간대", environment == null ? "미제공 / 미제공" :
                    $"{(environment.HasWeather ? environment.WeatherId.Value : "미제공")} / {(environment.HasTimeOfDay ? environment.TimeOfDayId.Value : "미제공")}");
                var cycle = _timeScene == null ? null : _timeScene.GetComponentsInChildren<MatchDayNightCycle>().FirstOrDefault();
                // 환경 source는 Scene 내 Weather 객체에 있으므로 같은 Scene에서만 찾습니다.
                if (cycle == null && _timeScene != null)
                    cycle = _timeScene.gameObject.scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<MatchDayNightCycle>()).FirstOrDefault();
                if (cycle != null)
                    EditorGUILayout.LabelField("낮밤 전환까지", cycle.IsAuthority ? $"{cycle.RemainingSeconds:F1} 게임 초 · {cycle.Phase}" : $"{cycle.Phase} · Host 확정 상태");
                EditorGUILayout.LabelField("날씨 예고", environment == null || environment.Forecast.Count == 0 ? "미제공" :
                    string.Join(" → ", environment.Forecast.Select(value => value.Value)), EditorStyles.wordWrappedLabel);
            }
            view.IsConnectionsExpanded=EditorGUILayout.Foldout(view.IsConnectionsExpanded,"연결 및 수집 상태 · "+GameDebuggerSession.Warnings.Count+" warnings",true);
            if (view.IsConnectionsExpanded) using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Match",snapshot?.MatchId.ToString() ?? "N/A");
                EditorGUILayout.LabelField("Authority",source==null?"N/A":$"server {source.IsServer} · client {source.IsClient} · listening {source.IsListening}");
                EditorGUILayout.LabelField("이벤트 / 표본",run==null?"—":$"{run.Events.Count:N0} / {run.Samples.Count:N0}");
                EditorGUILayout.LabelField("손실 E / S / buffer",run==null?"—":$"{run.DroppedEvents} / {run.DroppedSamples} / {run.RuntimeDroppedEvents}");
                EditorGUILayout.LabelField("관측 revision age",GameDebuggerSession.SnapshotObservedAge<0?"—":$"{GameDebuggerSession.SnapshotObservedAge:F2}s");
                EditorGUILayout.LabelField("귀속 불명 객체",sample==null?"—":Unknown(sample.UnattributedEnemies));
                foreach (string warning in GameDebuggerSession.Warnings.Take(8)) EditorGUILayout.HelpBox(warning,MessageType.Warning);
                if (GameDebuggerSession.Warnings.Count>8) EditorGUILayout.LabelField("추가 경고 "+(GameDebuggerSession.Warnings.Count-8)+"개",EditorStyles.miniLabel);
            }
            DrawTurrets(view);
            view.IsNotesExpanded=EditorGUILayout.Foldout(view.IsNotesExpanded,"관측값 해석 / 제한",true);
            if (view.IsNotesExpanded) EditorGUILayout.HelpBox("제거 ≠ 사망. 전선 기하는 우세도/위험도가 아닙니다. revision 관측 age는 정확한 발행 age가 아닙니다. 수집/창 종료는 Match를 변경하지 않습니다.",MessageType.None);
        }
        private static string Unknown(int value) => value<0?"N/A":value.ToString();
        private static void Tile(string title,string value,string help)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox,GUILayout.MinWidth(72),GUILayout.ExpandWidth(true)))
            {
                GUILayout.Label(new GUIContent(title,help),EditorStyles.miniLabel);
                GUILayout.Label(new GUIContent(value,help),GameDebugVisualStyle.Number,GUILayout.Height(24));
            }
        }
        public static void DrawTurrets(GameDebugMonitorState view)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("터렛 · 현재 Scene",EditorStyles.boldLabel);
                if (!EditorApplication.isPlaying) { EditorGUILayout.LabelField("배치 Preview는 Battlefield에서 조회합니다.",EditorStyles.miniLabel); return; }
                view.TurretScroll=EditorGUILayout.BeginScrollView(view.TurretScroll,GUILayout.Height(160));
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                { Cell("ID",54); Cell("정의 / HP",120); Cell("전력",48); Cell("상태",100); Cell("XY",90); }
                foreach (var turret in GameDebuggerSession.GetSceneTurrets())
                {
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("#"+turret.InstanceId,EditorStyles.miniButton,GUILayout.Width(54)) &&
                            FieldTurretInstanceRegistry.TryGet(turret.InstanceId,out var component))
                        { Selection.activeGameObject=component.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
                        Cell($"{turret.DefinitionId}  {turret.CurrentHealth}/{turret.MaxHealth}",120);
                        Cell(turret.EffectivePower.ToString(),48);
                        Cell(turret.IsDestroyed?"DESTROYED":turret.IsOperational?"RUNNING":turret.IsActivated?"PAUSED":"OFF",100);
                        Cell($"{turret.Position.x:F1}, {turret.Position.y:F1}",90);
                    }
                }
                EditorGUILayout.EndScrollView();
            }
        }
        private static void Cell(string text,float width) =>
            GUILayout.Label(new GUIContent(text,text),EditorStyles.label,GUILayout.Width(width));
        public static void DrawRequests(GameDebugRun run,GameDebugMonitorState view)
        {
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("Encounter 요청 결과",EditorStyles.boldLabel);
                var entries=view.Lists.Requests(run);
                if (entries.Count==0) { EditorGUILayout.LabelField("아직 요청 결과가 없습니다.",EditorStyles.miniLabel); return; }
                view.RequestScroll=EditorGUILayout.BeginScrollView(view.RequestScroll,GUILayout.Height(190));
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                { Cell("시간",54); Cell("요청",58); Cell("Source",105); Cell("Preset",80); Cell("계획",40); Cell("생성",40); Cell("결과",60); }
                foreach (var item in entries) using (new EditorGUILayout.HorizontalScope(
                    item.Sequence==view.SelectedRequestSequence?EditorStyles.helpBox:GUIStyle.none))
                {
                    Cell(item.GameSeconds.ToString("F1")+"s",54);
                    if (GUILayout.Button("#"+item.RequestId,EditorStyles.miniButton,GUILayout.Width(58))) view.SelectedRequestSequence=item.Sequence;
                    Cell(item.Source.ToString(),105); Cell(item.SquadOrder,80); Cell(item.IsExecutionCountUnknown?"N/A":item.PlannedCount.ToString(),40);
                    Cell(item.IsExecutionCountUnknown?"N/A":item.ActualCount.ToString(),40); Cell(item.Kind==GameDiagnosticKind.RequestFailed?"FAILED":"OK",60);
                }
                EditorGUILayout.EndScrollView();
                var selected=entries.FirstOrDefault(e=>e.Sequence==view.SelectedRequestSequence);
                if (selected==null) return;
                view.IsRequestDetailsExpanded=EditorGUILayout.Foldout(view.IsRequestDetailsExpanded,"선택 요청 #"+selected.RequestId+" · r"+selected.Revision,true);
                if (view.IsRequestDetailsExpanded) using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Match",selected.MatchId);
                    EditorGUILayout.LabelField("계획 / 실제",selected.IsExecutionCountUnknown?"N/A · 실행 중 예외로 수량 미확정":selected.PlannedCount+" / "+selected.ActualCount);
                    DrawOriginDetails(selected);
                    EditorGUILayout.SelectableLabel(selected.Message ?? "",EditorStyles.wordWrappedLabel,GUILayout.Height(76));
                    EditorGUILayout.LabelField("공간 판단·분포는 Statistics > Spawn decisions에서 조회",EditorStyles.miniLabel);
                }
            }
        }
        public static void DrawTimeline(GameDebugRun run,string search,string bookmark,GameDebugMonitorState view)
        {
            bool live=run!=null && ReferenceEquals(run,GameDebuggerSession.Current);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                EditorGUILayout.LabelField("조건부 정지",EditorStyles.boldLabel);
                using (new EditorGUI.DisabledGroupScope(!live))
                {
                    GameDebuggerSession.ShouldPauseOnFailure=EditorGUILayout.Toggle("요청 실패",GameDebuggerSession.ShouldPauseOnFailure);
                    GameDebuggerSession.ShouldPauseOnTurretDestroyed=EditorGUILayout.Toggle("터렛 파괴",GameDebuggerSession.ShouldPauseOnTurretDestroyed);
                    GameDebuggerSession.ShouldPauseOnOutOfBounds=EditorGUILayout.Toggle("Grid 범위 초과",GameDebuggerSession.ShouldPauseOnOutOfBounds);
                    using (new EditorGUILayout.HorizontalScope())
                    {
                        if (GUILayout.Button("Rearm")) GameDebuggerSession.Rearm();
                        if (GUILayout.Button("Bookmark")) GameDebuggerSession.AddMarker("Bookmark: "+bookmark);
                    }
                }
                if (!string.IsNullOrEmpty(GameDebuggerSession.LastPauseReason)) EditorGUILayout.HelpBox(GameDebuggerSession.LastPauseReason,MessageType.Warning);
            }
            if (run==null) return;
            var entries=view.Lists.Timeline(run,search);
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                view.TimelineScroll=EditorGUILayout.BeginScrollView(view.TimelineScroll,GUILayout.Height(240));
                using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
                { Cell("시간",62); Cell("이벤트",145); Cell("요청",65); Cell("Source",105); Cell("개체 / 지점",170); }
                foreach (var row in entries) using (new EditorGUILayout.HorizontalScope())
                {
                    Cell(row.Time.ToString("F2")+"s",62);
                    var item=row.Event;
                    if (item!=null)
                    {
                        if (GUILayout.Button(item.Kind.ToString(),EditorStyles.miniButton,GUILayout.Width(145))) view.SelectedEventSequence=item.Sequence;
                        Cell(item.RequestId.ToString(),65); Cell(item.Source.ToString(),105); Cell(item.EnemyType+" / "+item.SpawnPointId,170);
                    }
                    else
                    {
                        var marker=row.Marker;
                        Cell("Editor marker",145); Cell("—",65); Cell("Editor",105); Cell(marker.Message,170);
                        if (live && marker.TurretId>0 && GUILayout.Button("Frame",GUILayout.Width(50)) &&
                            FieldTurretInstanceRegistry.TryGet(marker.TurretId,out var turret))
                        { Selection.activeGameObject=turret.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
                    }
                }
                EditorGUILayout.EndScrollView();
                var selected=run.Events.FirstOrDefault(e=>e.Sequence==view.SelectedEventSequence);
                if (selected!=null)
                {
                    view.IsEventDetailsExpanded=EditorGUILayout.Foldout(view.IsEventDetailsExpanded,"선택 이벤트 · "+selected.Kind,true);
                    if (view.IsEventDetailsExpanded)
                    {
                        DrawOriginDetails(selected);
                        EditorGUILayout.SelectableLabel(selected.Message ?? "",EditorStyles.wordWrappedLabel,GUILayout.Height(70));
                    }
                }
            }
        }
        private static void DrawOriginDetails(GameDebugEventEntry item)
        {
            if (string.IsNullOrEmpty(item.OriginKind)) return;
            EditorGUILayout.LabelField("Origin / Delivery", item.OriginKind + " / " + item.DeliveryMode);
            EditorGUILayout.LabelField("Request GUID", string.IsNullOrEmpty(item.RequestGuid) ? "미계측" : item.RequestGuid);
            if (!string.IsNullOrEmpty(item.CauseEventId)) EditorGUILayout.LabelField("원인 사건", item.CauseEventId);
            if (!string.IsNullOrEmpty(item.SpawnSourceId))
                EditorGUILayout.LabelField("Source / Generation", item.SpawnSourceId + " / " + item.SpawnSourceGeneration);
        }
    }
}
