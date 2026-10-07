// 집계·중복·크기 제한·clock·내보내기·시간 복원과 창 구성을 검증합니다.
// 실제 Scene 스폰 및 시각 검사는 별도 통합 검증입니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
using UnityEngine;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class GameDebuggerTests
    {
        private static GameDiagnosticEvent Event(long sequence, GameDiagnosticKind kind, string match="A", double game=0, double real=0,
            int count=0, string enemy="Slime", string point="Left", EncounterRequestSource source=EncounterRequestSource.DebugWindow) =>
            new GameDiagnosticEvent(sequence,new MatchId(match),2,1,source,kind,game,real,enemy,point,"entity",count,count,"fact","Normal");
        private static GameDebugRun Add(GameDebugRecorder recorder,GameDiagnosticEvent item) => recorder.Accept(item,()=>new GameDebugRun());

        [Test]
        public void Replay_IsDeduplicated_AndMatchesAreSeparated()
        {
            var recorder=new GameDebugRecorder();
            var run=Add(recorder,Event(1,GameDiagnosticKind.MatchStarted));
            Add(recorder,Event(2,GameDiagnosticKind.EnemySpawned,count:1));
            Add(recorder,Event(2,GameDiagnosticKind.EnemySpawned,count:1));
            Add(recorder,Event(1,GameDiagnosticKind.MatchStarted,match:"B"));
            Assert.That(run.Events.Count,Is.EqualTo(2)); Assert.That(recorder.Runs.Count,Is.EqualTo(2)); Assert.That(run.IsPartial,Is.False);
            Add(recorder,Event(4,GameDiagnosticKind.MatchEnded));
            Assert.That(run.IsPartial,Is.True); Assert.That(run.IsComplete,Is.True);
        }
        [Test]
        public void Overflow_IsBounded_AndMarkedPartial()
        {
            var recorder=new GameDebugRecorder(); var run=Add(recorder,Event(1,GameDiagnosticKind.MatchStarted));
            for (int index=2;index<=GameDebugRecorder.MaximumEvents+2;index++) Add(recorder,Event(index,GameDiagnosticKind.EnemySpawned,count:1));
            Assert.That(run.Events.Count,Is.EqualTo(GameDebugRecorder.MaximumEvents)); Assert.That(run.DroppedEvents,Is.EqualTo(2)); Assert.That(run.IsPartial,Is.True);
        }
        [Test]
        public void SpawnAndRemoval_SameBin_AreNotCancelled_AndClockChangesBuckets()
        {
            var run=new GameDebugRun();
            run.Events.Add(GameDebugEventEntry.Copy(Event(1,GameDiagnosticKind.EnemySpawned,game:6,real:2,count:1)));
            run.Events.Add(GameDebugEventEntry.Copy(Event(2,GameDiagnosticKind.EnemyRemoved,game:7,real:3,count:1)));
            var game=GameDebugStatistics.Build(run,5,false); var real=GameDebugStatistics.Build(run,5,true);
            Assert.That(game[1].Spawned,Is.EqualTo(1)); Assert.That(game[1].Removed,Is.EqualTo(1)); Assert.That(real[0].Spawned,Is.EqualTo(1));
            Assert.That(game[0].Alive,Is.EqualTo(-1));
        }
        [Test]
        public void Filters_UseStructuredFields_AndSamplesRemainWholeMatch()
        {
            var run=new GameDebugRun();
            run.Events.Add(GameDebugEventEntry.Copy(Event(1,GameDiagnosticKind.EnemySpawned,count:3)));
            run.Events.Add(GameDebugEventEntry.Copy(Event(2,GameDiagnosticKind.EnemySpawned,count:2,enemy:"Boss",source:EncounterRequestSource.SceneCollision)));
            run.Samples.Add(new GameDebugSample{AliveEnemies=5,AvailablePower=12,TurretCount=4,OperationalTurrets=3});
            var filtered=GameDebugStatistics.Build(run,5,false,"Slime","Left","Normal",EncounterRequestSource.DebugWindow);
            Assert.That(filtered[0].Spawned,Is.EqualTo(3)); Assert.That(filtered[0].Alive,Is.EqualTo(5)); Assert.That(filtered[0].OperationalRatio,Is.EqualTo(0.75));
        }
        [Test]
        public void Bins_AreBounded_AndRejectInvalidWidth()
        {
            var run=new GameDebugRun(); run.Events.Add(GameDebugEventEntry.Copy(Event(1,GameDiagnosticKind.EnemySpawned,game:1e6,count:1)));
            Assert.That(GameDebugStatistics.Build(run,0.1,false).Count,Is.LessThanOrEqualTo(2048));
            Assert.Throws<ArgumentOutOfRangeException>(()=>GameDebugStatistics.Build(run,double.NaN,false));
        }
        [Test]
        public void PartialRecord_EmptyEventBin_IsUnknown_NotMeasuredZero()
        {
            var run=new GameDebugRun{IsPartial=true};
            Assert.That(GameDebugChart.Value(GameDebugStatistics.Build(run,5,false)[0],GameDebugMetric.Spawned),Is.EqualTo(-1));
            run.IsPartial=false;
            Assert.That(GameDebugChart.Value(GameDebugStatistics.Build(run,5,false)[0],GameDebugMetric.Spawned),Is.EqualTo(0));
        }
        [Test]
        public void ArchivedRuns_RestoreWithoutMixingLiveIdentity()
        {
            var recorder=new GameDebugRecorder();
            recorder.RestoreArchives(new[]{new GameDebugRun{MatchId="previous",IsComplete=true}});
            var active=Add(recorder,Event(1,GameDiagnosticKind.MatchStarted,match:"current"));
            Assert.That(recorder.Runs.Count,Is.EqualTo(2)); Assert.That(active.MatchId,Is.EqualTo("current"));
            Assert.That(recorder.Find("previous").IsComplete,Is.True);
        }
        [Test]
        public void TimeScale_RestoresOnlyOwnedValue_AndIsReusable()
        {
            float scale=1; var lease=new TimeScaleLease(()=>scale,value=>scale=value);
            lease.Apply(0.5f); lease.Apply(4); lease.Dispose(); Assert.That(scale,Is.EqualTo(1));
            lease.Apply(2); scale=3; lease.Dispose(); Assert.That(scale,Is.EqualTo(3));
            Assert.Throws<ArgumentOutOfRangeException>(()=>lease.Apply(float.NaN));
        }
        [Test]
        public void JsonRoundTrip_PreservesMetadata_AndCsvEscapesFormula()
        {
            var recorder=new GameDebugRecorder();var run=Add(recorder,Event(1,GameDiagnosticKind.MatchStarted)); run.Scene="test";run.HasSeed=true;run.RandomSeed=17;
            Assert.That(GameDebugExport.FromJson(GameDebugExport.ToJson(run)).RandomSeed,Is.EqualTo(17));
            Assert.That(GameDebugExport.CsvCell("=SUM(A1)"),Is.EqualTo("\"'=SUM(A1)\""));
            Assert.That(GameDebugExport.CsvCell("a,\"b\""),Is.EqualTo("\"a,\"\"b\"\"\""));
            run.Events[0].GameSeconds=double.PositiveInfinity;
            Assert.Throws<ArgumentException>(()=>GameDebugExport.FromJson(GameDebugExport.ToJson(run)));
        }
        [Test]
        public void Window_BuildsAllPages_AndClosesWithoutChangingTime()
        {
            float scale=Time.timeScale;
            string previousDisplay=JsonUtility.ToJson(BattlefieldDebugSession.Display);
            var window=ScriptableObject.CreateInstance<GameDebuggerWindow>();
            try
            {
                window.CreateGUI();
                Assert.That(window.rootVisualElement.childCount,Is.GreaterThan(0));
                var field=typeof(GameDebuggerWindow).GetField("_page",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                var build=typeof(GameDebuggerWindow).GetMethod("BuildPage",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
                for (int index=0;index<6;index++) { field.SetValue(window,index); Assert.DoesNotThrow(()=>build.Invoke(window,null)); }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
                JsonUtility.FromJsonOverwrite(previousDisplay,BattlefieldDebugSession.Display);
                BattlefieldDebugSession.NotifyDisplayChanged();
            }
            Assert.That(Time.timeScale,Is.EqualTo(scale));
        }
    }
}
