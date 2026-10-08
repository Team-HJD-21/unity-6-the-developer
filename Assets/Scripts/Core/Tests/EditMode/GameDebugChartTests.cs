// 선 그래프의 시간 추적·자동 축·수동 탐색과 생성/사망 사실 분리를 검증합니다.
using NUnit.Framework;
using System.Collections;
using TeamHJD.Game.Contracts;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class GameDebugChartTests
    {
        [Test]
        public void LatestFollow_StopsOnPan_AndResumesExplicitly()
        {
            var view=new Editor.GameDebugChartViewport{SpanSeconds=30};
            view.UpdateDuration(120); Assert.That(view.StartSeconds,Is.EqualTo(90));
            view.PanTime(20,120); view.UpdateDuration(180);
            Assert.That(view.StartSeconds,Is.EqualTo(20)); Assert.That(view.IsFollowingLatest,Is.False);
            view.IsFollowingLatest=true; view.UpdateDuration(180); Assert.That(view.StartSeconds,Is.EqualTo(150));
            view.ZoomTime(.5,180); Assert.That(view.SpanSeconds,Is.EqualTo(15)); Assert.That(view.StartSeconds,Is.EqualTo(165));
        }
        [Test]
        public void AutoY_AddsHeadroom_ManualZoomAndPanArePreserved()
        {
            var view=new Editor.GameDebugChartViewport();
            view.FitY(10); Assert.That(view.MaximumY,Is.GreaterThan(10));
            double original=view.MaximumY;
            view.ZoomY(.5); view.FitY(1000);
            Assert.That(view.MaximumY,Is.EqualTo(original*.5)); Assert.That(view.IsAutoY,Is.False);
            double span=view.SpanY; view.PanY(12);
            Assert.That(view.MinimumY,Is.EqualTo(12)); Assert.That(view.SpanY,Is.EqualTo(span));
            view.IsAutoY=true; view.FitY(2); Assert.That(view.MaximumY,Is.LessThan(original)); Assert.That(view.MinimumY,Is.Zero);
        }
        [Test]
        public void OldRecords_DoNotInventDeaths_AndRemovalIsNotDeath()
        {
            var old=new Editor.GameDebugBucket{HasFullEventCoverage=true,Removed=4};
            Assert.That(Editor.GameDebugChart.Value(old,Editor.GameDebugMetric.Died),Is.EqualTo(-1));
            Assert.That(Editor.GameDebugChart.Value(old,Editor.GameDebugMetric.Removed),Is.EqualTo(4));
            old.HasDeathEvents=true;
            Assert.That(Editor.GameDebugChart.Value(old,Editor.GameDebugMetric.Died),Is.Zero);
        }
        [Test]
        public void RequestSpawnDeathAndRemoval_AreIndependentBinSeries()
        {
            var run=new Editor.GameDebugRun{IsPartial=false,HasDeathEvents=true};
            run.Events.Add(new Editor.GameDebugEventEntry{Kind=GameDiagnosticKind.RequestStarted,GameSeconds=1});
            run.Events.Add(new Editor.GameDebugEventEntry{Kind=GameDiagnosticKind.EnemySpawned,GameSeconds=2,ActualCount=10,EnemyType="Slime"});
            run.Events.Add(new Editor.GameDebugEventEntry{Kind=GameDiagnosticKind.EnemyDied,GameSeconds=3,ActualCount=1,EnemyType="Slime"});
            run.Events.Add(new Editor.GameDebugEventEntry{Kind=GameDiagnosticKind.EnemyRemoved,GameSeconds=4,ActualCount=2,EnemyType="Slime"});
            var bucket=Editor.GameDebugStatistics.Build(run,5,false)[0];
            Assert.That(bucket.Requests,Is.EqualTo(1)); Assert.That(bucket.Spawned,Is.EqualTo(10));
            Assert.That(bucket.Died,Is.EqualTo(1)); Assert.That(bucket.Removed,Is.EqualTo(2));
            var filtered=Editor.GameDebugStatistics.Build(run,5,false,"Slime")[0];
            Assert.That(filtered.Requests,Is.EqualTo(-1)); Assert.That(filtered.Spawned,Is.EqualTo(10));
        }
        [Test]
        public void PaneState_PersistsChartPreferences_AndNormalizesBadNumbers()
        {
            var pane=new Editor.GameDebuggerPaneState();
            pane.ChartViewport.PanTime(25,100);
            pane.ChartViewport.SpanSeconds=20; pane.ChartViewport.IsRemovalsVisible=true;
            var restored=JsonUtility.FromJson<Editor.GameDebuggerPaneState>(JsonUtility.ToJson(pane)); restored.Validate();
            Assert.That(restored.ChartViewport.StartSeconds,Is.EqualTo(25)); Assert.That(restored.ChartViewport.IsFollowingLatest,Is.False);
            Assert.That(restored.ChartViewport.IsRemovalsVisible,Is.True);
            restored.ChartViewport.SpanSeconds=double.NaN; restored.ChartViewport.MinimumY=double.NegativeInfinity;
            restored.Validate(); Assert.That(restored.ChartViewport.SpanSeconds,Is.EqualTo(60)); Assert.That(restored.ChartViewport.MinimumY,Is.Zero);
        }
        [Test]
        public void StatisticsPane_BuildsTimeSeriesAndSpawnInspector_WithoutCreatingMatch()
        {
            var source=Editor.GameDebuggerSession.Source; var run=Editor.GameDebuggerSession.Current;
            var state=new Editor.GameDebuggerPaneState{Page=3};
            var pane=new Editor.GameDebuggerPane(0,state,()=>{},_=>{},_=>{});
            try
            {
                Assert.That(pane.Q<Editor.GameDebugChart>(),Is.Not.Null);
                state.IsSpawnInspector=true; pane.SelectPage(3);
                Assert.That(pane.Q<Editor.GameDebugSpawnInspector>(),Is.Not.Null);
                Assert.That(Editor.GameDebuggerSession.Source,Is.SameAs(source));
                Assert.That(Editor.GameDebuggerSession.Current,Is.SameAs(run));
            }
            finally { pane.Dispose(); }
        }

        [UnityTest]
        public IEnumerator StatisticsWindow_RendersChartWithScrollableAxes_WithoutDuplicatingCollector()
        {
            var source=Editor.GameDebuggerSession.Source;
            var state=new Editor.GameDebuggerPaneState{Page=3};
            state.ChartViewport.IsAutoY=false; state.ChartViewport.MaximumY=50;
            var window=Editor.GameDebugStatisticsWindow.Open(state);
            try
            {
                window.position=new Rect(100,100,1000,720);
                window.CreateGUI();
                var chart=window.rootVisualElement.Q<Editor.GameDebugChart>();
                chart.SetData(new[]{new Editor.GameDebugBucket{Start=0,Width=5,HasFullEventCoverage=true,HasDeathEvents=true,
                    Spawned=10,Died=2,Requests=1}},null,Editor.GameDebugMetric.Spawned);
                for(int frame=0;frame<12;frame++){window.Repaint();yield return null;}
                Assert.That(chart.resolvedStyle.width,Is.GreaterThan(200));
                Assert.That(chart.resolvedStyle.height,Is.GreaterThanOrEqualTo(280));
                Assert.That(chart.Viewport.MaximumY,Is.GreaterThan(10));
                Assert.That(Editor.GameDebuggerSession.Source,Is.SameAs(source));
            }
            finally { window.Close(); }
        }
    }
}
