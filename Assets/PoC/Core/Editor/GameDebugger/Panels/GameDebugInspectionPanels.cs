// 현재 Match의 연결·터렛·요청 결과와 타임라인을 조회합니다.
// 과거 기록은 조회만 하고, 선택 명령은 현재 Scene의 객체만 대상으로 합니다.
using System;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Turrets;
using UnityEditor;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class GameDebugInspectionPanels
    {
        public static void DrawOverview()
        {
            var source=GameDebuggerSession.Source;
            BattlefieldDiagnosticPanels.DrawOverview();
            EditorGUILayout.LabelField("Authority", source==null ? "Unknown" : $"server={source.IsServer} client={source.IsClient} listening={source.IsListening}");
            EditorGUILayout.LabelField("Clock", source==null ? "N/A" : $"game {source.GameSeconds:F2}s / real {source.RealSeconds:F2}s");
            EditorGUILayout.LabelField("Snapshot observed age", GameDebuggerSession.SnapshotObservedAge<0 ? "N/A" : $"{GameDebuggerSession.SnapshotObservedAge:F2}s (revision을 처음 관측한 후)");
            var run=GameDebuggerSession.Current;
            if (run!=null)
            {
                EditorGUILayout.LabelField("Recorded events / samples", $"{run.Events.Count} / {run.Samples.Count} (partial={run.IsPartial})");
                EditorGUILayout.LabelField("Dropped E / S / runtime buffer", $"{run.DroppedEvents} / {run.DroppedSamples} / {run.RuntimeDroppedEvents}");
                var sample=run.Samples.LastOrDefault();
                if (sample!=null)
                {
                    EditorGUILayout.LabelField("Match alive / unassigned", $"{Unknown(sample.AliveEnemies)} / {Unknown(sample.UnattributedEnemies)}");
                    EditorGUILayout.LabelField("Power available / maximum", $"{Unknown(sample.AvailablePower)} / {Unknown(sample.MaximumPower)}");
                    EditorGUILayout.LabelField("Territory area / boundary length", $"{sample.TerritoryArea:F2} / {sample.FrontlineLength:F2} world units");
                }
            }
            EditorGUILayout.HelpBox("읽기 전용 관측 도구입니다. Despawn ≠ 처치. HP/전력은 Feature 조회값이며 전선 기하는 전술 우세도가 아닙니다. snapshot 발행 timestamp 계약이 없어 정확한 snapshot age는 N/A입니다.",MessageType.Info);
            foreach (string warning in GameDebuggerSession.Warnings) EditorGUILayout.HelpBox(warning,MessageType.Warning);
            DrawTurrets();
        }
        private static string Unknown(int value) => value<0 ? "N/A" : value.ToString();
        public static void DrawTurrets()
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Turrets · Current Scene / read-only",EditorStyles.boldLabel);
            if (!EditorApplication.isPlaying) { EditorGUILayout.HelpBox("Runtime registry는 Play Mode에서 조회됩니다. 배치 Preview는 Battlefield 패널을 사용하세요.",MessageType.None); return; }
            foreach (var turret in GameDebuggerSession.GetSceneTurrets())
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("#"+turret.InstanceId,GUILayout.Width(56)) && TurretInstanceRegistry.TryGet(turret.InstanceId,out var component))
                { Selection.activeGameObject=component.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
                EditorGUILayout.LabelField($"{turret.DefinitionId} | HP {turret.CurrentHealth}/{turret.MaxHealth} | power {turret.EffectivePower} | {(turret.IsDestroyed?"DESTROYED":turret.IsOperational?"OPERATIONAL":turret.IsActivated?"ACTIVATED / PAUSED":"OFF")} | ({turret.Position.x:F1}, {turret.Position.y:F1})",EditorStyles.wordWrappedLabel);
                EditorGUILayout.EndHorizontal();
            }
        }
        public static void DrawRequests(GameDebugRun run)
        {
            EditorGUILayout.Space(); EditorGUILayout.LabelField("Request results · latest 20",EditorStyles.boldLabel);
            if (run==null) return;
            foreach (var item in run.Events.Where(item=>item.Kind==GameDiagnosticKind.RequestCompleted || item.Kind==GameDiagnosticKind.RequestFailed).Reverse().Take(20))
                EditorGUILayout.LabelField($"#{item.RequestId} r{item.Revision} · {item.Source} · {item.SquadOrder} · planned {item.PlannedCount} / actual {item.ActualCount}\n{item.Message}",EditorStyles.wordWrappedLabel);
        }
        public static void DrawTimeline(GameDebugRun run, string search, string bookmark)
        {
            bool live=run!=null && ReferenceEquals(run,GameDebuggerSession.Current);
            using (new EditorGUI.DisabledGroupScope(!live))
            {
                GameDebuggerSession.PauseOnFailure=EditorGUILayout.Toggle("Pause on request failure",GameDebuggerSession.PauseOnFailure);
                GameDebuggerSession.PauseOnTurretDestroyed=EditorGUILayout.Toggle("Pause on turret destroyed",GameDebuggerSession.PauseOnTurretDestroyed);
                GameDebuggerSession.PauseOnOutOfBounds=EditorGUILayout.Toggle("Pause on Grid OOB",GameDebuggerSession.PauseOnOutOfBounds);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("Rearm conditions")) GameDebuggerSession.Rearm();
                if (GUILayout.Button("Add bookmark")) GameDebuggerSession.AddMarker("Bookmark: "+bookmark);
                EditorGUILayout.EndHorizontal();
            }
            if (!string.IsNullOrEmpty(GameDebuggerSession.LastPauseReason)) EditorGUILayout.HelpBox(GameDebuggerSession.LastPauseReason+" · Resume 후 재무장해야 다시 정지합니다.",MessageType.Warning);
            if (run==null) return;
            // 서로 다른 공급 경로라도 게임시간으로 하나의 타임라인에 정렬합니다.
            var entries=run.Events.Select(item=>(time:item.GameSeconds, text:$"[{item.Source}/{item.Kind}] request #{item.RequestId} r{item.Revision} {item.SquadOrder} {item.EnemyType} {item.SpawnPointId}: {item.Message}",turret:0))
                .Concat(run.Markers.Select(item=>(time:item.GameSeconds,text:"[Editor] "+item.Message,turret:item.TurretId)))
                .Where(item=>string.IsNullOrEmpty(search) || item.text.IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
                .OrderByDescending(item=>item.time).Take(200);
            foreach (var item in entries)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField($"{item.time:F2}s {item.text}",EditorStyles.wordWrappedLabel);
                if (live && item.turret>0 && GUILayout.Button("Select",GUILayout.Width(55)) && TurretInstanceRegistry.TryGet(item.turret,out var turret))
                { Selection.activeGameObject=turret.gameObject; SceneView.lastActiveSceneView?.FrameSelected(); }
                EditorGUILayout.EndHorizontal();
            }
        }
    }
}
