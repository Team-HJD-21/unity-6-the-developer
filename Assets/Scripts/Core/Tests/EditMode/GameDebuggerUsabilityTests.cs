// 디버거의 조회 상태 보존·입력 검증·지도 제스처·Scene 이동·표 캐시를 검사합니다.
// 게임 Match를 만들거나 UI 검사를 실제 전투 완료로 간주하지 않습니다.
using System;
using System.Collections;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class GameDebuggerUsabilityTests
    {
        private static GameDebugRun Run()
        {
            var run=new GameDebugRun {MatchId="view-test"};
            run.SpawnDecisions.Add(new GameDebugSpawnDecision {MatchId=run.MatchId,RequestId=1,
                Grid=new GridMetadata(BattlefieldGridConfiguration.Default),
                Candidates={new GameDebugSpawnDecision.Candidate {Id="Left",Sets=1}},
                Plans={new GameDebugSpawnDecision.Plan {Point="Left",Enemy="Slime",Count=1}}});
            return run;
        }

        [Test]
        public void SpawnSelectionAndFilters_SurviveViewReconstructionAndSerialization()
        {
            var run=Run();
            var state=new GameDebuggerPaneState {IsSpawnInspector=true};
            var inspector=new GameDebugSpawnInspector(state.SpawnView);
            // UITK의 값 변경 event는 실제 panel에 연결된 element에서 발생합니다.
            var host=GameDebugStatisticsWindow.Open(new GameDebuggerPaneState());
            try
            {
                host.CreateGUI();host.rootVisualElement.Add(inspector);
                inspector.SetRun(run);
                Assert.That(inspector.SelectRequest(1),Is.True);
                inspector.Q<TextField>().value="Left";
                inspector.Query<DropdownField>().ToList()[1].value="Slime";
            }
            finally {host.Close();}
            state.SpawnView.Pan=new Vector2(20,-10);
            state.SpawnView.Zoom=3;
            var restored=JsonUtility.FromJson<GameDebuggerPaneState>(JsonUtility.ToJson(state));
            restored.Validate();
            new GameDebugSpawnInspector(restored.SpawnView).SetRun(run);
            Assert.That(restored.SpawnView.SelectedRequestId,Is.EqualTo(1));
            Assert.That(restored.SpawnView.IsFollowingLatest,Is.False);
            Assert.That(restored.SpawnView.Search,Is.EqualTo("Left"));
            Assert.That(restored.SpawnView.MonsterFilter,Is.EqualTo("Slime"));
            Assert.That(restored.SpawnView.Pan,Is.EqualTo(new Vector2(20,-10)));
            Assert.That(restored.SpawnView.Zoom,Is.EqualTo(3));
        }

        [Test]
        public void PinnedRequestEviction_DoesNotSilentlySelectAnotherSnapshot()
        {
            var run=Run();var state=new GameDebugSpawnViewState();
            var inspector=new GameDebugSpawnInspector(state);inspector.SetRun(run);inspector.SelectRequest(1);
            run.SpawnDecisions[0]=new GameDebugSpawnDecision {MatchId=run.MatchId,RequestId=2,Sequence=2,
                Grid=new GridMetadata(BattlefieldGridConfiguration.Default)};
            inspector.SetRun(run);
            Assert.That(state.SelectedRequestId,Is.EqualTo(1));
            Assert.That(inspector.Query<DropdownField>().ToList()[0].value,Does.Contain("no longer retained"));
            Assert.That(inspector.SelectRequest(2),Is.True);
            Assert.That(state.SelectedRequestId,Is.EqualTo(2));
        }

        [Test]
        public void MatchChange_DoesNotReusePreviousRequestIdentity()
        {
            var run=Run();var state=new GameDebugSpawnViewState();
            var inspector=new GameDebugSpawnInspector(state);inspector.SetRun(run);inspector.SelectRequest(1);
            var next=Run();next.MatchId="other-match";next.SpawnDecisions[0].MatchId=next.MatchId;
            next.SpawnDecisions[0].RequestId=2;
            inspector.SetRun(next);
            Assert.That(state.MatchId,Is.EqualTo("other-match"));
            Assert.That(state.SelectedRequestId,Is.EqualTo(2));
        }

        [Test]
        public void MapClickThreshold_SeparatesDragAndSelection()
        {
            Assert.That(GameDebugSpawnMap.IsClickGesture(Vector2.zero,new Vector2(2,1)),Is.True);
            Assert.That(GameDebugSpawnMap.IsClickGesture(Vector2.zero,new Vector2(30,10)),Is.False);
            var view=new GameDebugSpawnViewState {Zoom=float.NaN,Pan=new Vector2(float.PositiveInfinity,0)};
            view.Validate();Assert.That(view.Zoom,Is.EqualTo(1));Assert.That(view.Pan,Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void ClientWithoutServerObservations_DoesNotInventZeroSpawnOrDeath()
        {
            var run=new GameDebugRun {IsPartial=false,HasDeathEvents=true};
            run.Events.Add(new GameDebugEventEntry {Kind=GameDiagnosticKind.RequestStarted,GameSeconds=1});
            run.Samples.Add(new GameDebugSample {GameSeconds=2,AliveEnemies=-1});
            var bucket=GameDebugStatistics.Build(run,5,false)[0];
            Assert.That(GameDebugChart.Value(bucket,GameDebugMetric.Requests),Is.EqualTo(1));
            Assert.That(GameDebugChart.Value(bucket,GameDebugMetric.Spawned),Is.EqualTo(-1));
            Assert.That(GameDebugChart.Value(bucket,GameDebugMetric.Died),Is.EqualTo(-1));
            Assert.That(GameDebugChart.Value(bucket,GameDebugMetric.Removed),Is.EqualTo(-1));
            run.Events.Add(new GameDebugEventEntry {Kind=GameDiagnosticKind.EnemySpawned,GameSeconds=1,ActualCount=3});
            bucket=GameDebugStatistics.Build(run,5,false)[0];
            Assert.That(GameDebugChart.Value(bucket,GameDebugMetric.Spawned),Is.EqualTo(3),"실제 수신 사실까지 버리지 않습니다.");
        }

        [Test]
        public void BinInput_ShowsTheEffectiveClampedValue()
        {
            var window=GameDebugStatisticsWindow.Open(new GameDebuggerPaneState {Page=3});
            try
            {
                window.CreateGUI();
                var pane=window.rootVisualElement.Q<GameDebuggerPane>();
                var state=pane.State;
                var bin=pane.Q<FloatField>();bin.value=-1;
                Assert.That(state.BinSeconds,Is.EqualTo(.1f));Assert.That(bin.value,Is.EqualTo(.1f));
                bin.value=float.NaN;
                Assert.That(bin.value,Is.EqualTo(state.BinSeconds));
            }
            finally {window.Close();}
        }

        [Test]
        public void ListCache_ReusesResultsAndDetectsSameCountBufferReplacement()
        {
            var run=new GameDebugRun();
            run.Events.Add(new GameDebugEventEntry {Kind=GameDiagnosticKind.RequestCompleted,GameSeconds=2,Sequence=1});
            var cache=new GameDebugListCache();
            var requests=cache.Requests(run);Assert.That(cache.Requests(run),Is.SameAs(requests));
            var timeline=cache.Timeline(run,"");Assert.That(cache.Timeline(run,""),Is.SameAs(timeline));
            run.Events[0]=new GameDebugEventEntry {Kind=GameDiagnosticKind.RequestFailed,GameSeconds=3,Sequence=2};
            Assert.That(cache.Requests(run),Is.Not.SameAs(requests));
            run.Markers.Add(new GameDebugMarker {GameSeconds=4,Message="Target lost"});
            Assert.That(cache.Timeline(run,"")[0].Marker.Message,Is.EqualTo("Target lost"));
            Assert.That(cache.Timeline(run,"target").Count,Is.EqualTo(1));
        }

        [Test]
        public void SceneFrameBounds_RespectCenteredXYGridAndCellIndices()
        {
            var grid=new BattlefieldGridConfiguration(new BattlefieldPoint(7,-3),2,32,16,4,2);
            Bounds map=GameDebugSceneNavigation.GridBounds(grid);
            Assert.That(map.center,Is.EqualTo(new Vector3(7,-3,2)));
            Assert.That(map.size.x,Is.EqualTo(32));
            Assert.That(GameDebugSceneNavigation.TryGetCellBounds(grid,0,out Bounds cell),Is.True);
            Assert.That(cell.center,Is.EqualTo(new Vector3(-5,-7,2)));
            Assert.That(GameDebugSceneNavigation.TryGetCellBounds(grid,8,out _),Is.False);
            Assert.That(GameDebugSceneNavigation.TryGetCellBounds(null,0,out _),Is.False);
        }

        [Test]
        public void CellEvidence_IncludesOutOfBoundsIdsWithoutATableDisplayLimit()
        {
            var input=new BattlefieldDynamicSpatialInput(Array.Empty<PlayerSpatialInput>(),
                new[]{new EnemySpatialInput(new EntityId("enemy-oob"),new BattlefieldPoint(10000,0))});
            var grid=new BattlefieldGridBuilder().Build(BattlefieldGridConfiguration.Default,BattlefieldSpatialInput.Empty,input);
            Assert.That(GameDebugSceneNavigation.CellEvidence(grid,-1),Does.Contain("enemy-oob"));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SeriesContrast_IsReadableOnDarkAndLightGraphBackgrounds(bool dark)
        {
            double background=Luminance(dark?new Color(.13f,.13f,.13f):new Color(.92f,.92f,.92f));
            foreach(var metric in new[]{GameDebugMetric.Requests,GameDebugMetric.Spawned,GameDebugMetric.Died,GameDebugMetric.Removed})
            {
                double line=Luminance(GameDebugVisualStyle.Series(metric,dark));
                Assert.That((Math.Max(line,background)+.05)/(Math.Min(line,background)+.05),Is.GreaterThanOrEqualTo(3),metric.ToString());
            }
        }

        [UnityTest]
        public IEnumerator SnapshotAndNativeTables_RenderWithoutErrorsInStatisticsWindow()
        {
            var run=Run();
            run.Events.Add(new GameDebugEventEntry {Kind=GameDiagnosticKind.RequestCompleted,Sequence=1,Message="Spawn completed"});
            var importedBefore=GameDebuggerSession.Imported;
            GameDebuggerSession.SetImported(run);
            var window=GameDebugStatisticsWindow.Open(new GameDebuggerPaneState {Page=3,IsSpawnInspector=true,SelectedRun="IMPORTED"});
            try
            {
                window.CreateGUI();
                var inspector=window.rootVisualElement.Q<GameDebugSpawnInspector>();inspector.SetRun(run);
                var monitor=new GameDebugMonitorState();
                window.rootVisualElement.Add(new IMGUIContainer(()=>{
                    GameDebugInspectionPanels.DrawRequests(run,monitor);
                    GameDebugInspectionPanels.DrawTimeline(run,"","",monitor);
                }));
                for(int frame=0;frame<12;frame++){window.Repaint();yield return null;}
                Assert.That(inspector.Q<GameDebugSpawnMap>().resolvedStyle.height,Is.GreaterThanOrEqualTo(220));
            }
            finally {window.Close();GameDebuggerSession.SetImported(importedBefore);}
        }

        private static double Luminance(Color color) => .2126*Linear(color.r)+.7152*Linear(color.g)+.0722*Linear(color.b);
        private static double Linear(double value) => value<=.04045 ? value/12.92 : Math.Pow((value+.055)/1.055,2.4);
    }
}
