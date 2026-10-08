// 요청별 판단 기록의 보존·복사·JSON 검증과 Inspector 선택을 검사합니다.
// 실제 서버 생성과 사망 관측은 Scene 통합 테스트에서 별도로 확인합니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
using UnityEngine.UIElements;
namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class GameDebugSpawnInspectionTests
    {
        private static SpawnDecisionSnapshot Snapshot()
        {
            var spatial=BattlefieldSpatialInput.Empty;
            var grid=new BattlefieldGridBuilder().Build(BattlefieldGridConfiguration.Default,spatial,BattlefieldDynamicSpatialInput.Empty);
            var battlefield=BattlefieldSpatialSnapshot.CreateMatchSnapshot(new BattlefieldTopologyBuilder().Build(spatial),new MatchId("inspect"),1,grid);
            return new SpawnDecisionSnapshot(battlefield,
                new[]{new SpawnDecisionSnapshot.Candidate("Left",new BattlefieldPoint(-3,0),5,8,2,1,0,false,"Allocated by cell service",20,10,5,27,true)},
                new[]{new SpawnDecisionSnapshot.Plan("Left","Slime",1)},
                new[]{new SpawnDecisionSnapshot.Actual("Left","Slime","42",new BattlefieldPoint(-2.5,0.1))},"Spawned");
        }
        private static GameDiagnosticEvent Event(long sequence,SpawnDecisionSnapshot snapshot,bool death=true)
            =>new GameDiagnosticEvent(sequence,new MatchId("inspect"),1,sequence,EncounterRequestSource.DebugWindow,
                GameDiagnosticKind.RequestCompleted,sequence,sequence,plannedCount:1,actualCount:1,squadOrder:"Normal",
                spawnDecision:snapshot,hasDeathEvents:death);
        [Test]
        public void Recorder_CopiesDecision_AndBoundsArchiveWithoutDuplicatingReplay()
        {
            var recorder=new GameDebugRecorder();GameDebugRun run=null;var snapshot=Snapshot();
            for(int i=1;i<=40;i++) run=recorder.Accept(Event(i,snapshot),()=>new GameDebugRun());
            Assert.That(run.SpawnDecisions,Has.Count.EqualTo(GameDebugSpawnDecision.MaximumRecords));
            Assert.That(run.DroppedSpawnDecisions,Is.EqualTo(8));
            recorder.Accept(Event(40,snapshot),()=>new GameDebugRun());
            Assert.That(run.DroppedSpawnDecisions,Is.EqualTo(8));
            run.SpawnDecisions[0].Candidates[0].Id="edited archive";
            Assert.That(snapshot.Candidates[0].Id,Is.EqualTo("Left"));
        }
        [Test]
        public void Json_RoundTripsSnapshots_AndRejectsInvalidCoordinates()
        {
            var recorder=new GameDebugRecorder();var run=recorder.Accept(Event(1,Snapshot()),()=>new GameDebugRun());
            var copied=GameDebugExport.FromJson(GameDebugExport.ToJson(run));
            Assert.That(copied.SpawnDecisions[0].Actuals[0].X,Is.EqualTo(-2.5));
            Assert.That(copied.SpawnDecisions[0].Grid.CellsX,Is.EqualTo(16));
            var candidate=copied.SpawnDecisions[0].Candidates[0];
            Assert.That(candidate.HasDistributionMetrics,Is.True);
            Assert.That(candidate.CellService,Is.EqualTo(20));Assert.That(candidate.PointService,Is.EqualTo(10));
            Assert.That(candidate.CellPendingEnemies,Is.EqualTo(5));Assert.That(candidate.CellScore,Is.EqualTo(27));
            copied.SpawnDecisions[0].Candidates[0].X=double.NaN;
            Assert.Throws<ArgumentException>(()=>GameDebugSpawnValidation.Validate(copied));
        }
        [Test]
        public void OldCandidate_LeavesDistributionMetricsUnavailable_AndNegativeServiceIsRejected()
        {
            var recorder=new GameDebugRecorder();var run=recorder.Accept(Event(1,Snapshot()),()=>new GameDebugRun());
            var candidate=run.SpawnDecisions[0].Candidates[0];
            candidate.HasDistributionMetrics=false;candidate.CellService=0;candidate.PointService=0;candidate.CellPendingEnemies=0;candidate.CellScore=0;
            var read=GameDebugExport.FromJson(GameDebugExport.ToJson(run));
            Assert.That(read.SpawnDecisions[0].Candidates[0].HasDistributionMetrics,Is.False);
            read.SpawnDecisions[0].Candidates[0].CellService=-1;
            Assert.Throws<ArgumentException>(()=>GameDebugSpawnValidation.Validate(read));
        }
        [Test]
        public void OldArchive_UsesEmptySnapshotsAndDoesNotClaimDeathCoverage()
        {
            var run=new GameDebugRun{MatchId="inspect",SpawnDecisions=null};
            var read=GameDebugExport.FromJson(GameDebugExport.ToJson(run));
            Assert.That(read.SpawnDecisions,Is.Empty);Assert.That(read.HasDeathEvents,Is.False);
        }
        [Test]
        public void RuntimeSnapshot_TruncatesWithExplicitDroppedCount()
        {
            var original=Snapshot();var spatial=BattlefieldSpatialInput.Empty;
            var grid=new BattlefieldGridBuilder().Build(BattlefieldGridConfiguration.Default,spatial,BattlefieldDynamicSpatialInput.Empty);
            var field=BattlefieldSpatialSnapshot.CreateMatchSnapshot(new BattlefieldTopologyBuilder().Build(spatial),new MatchId("inspect"),1,grid);
            var snapshot=new SpawnDecisionSnapshot(field,Enumerable.Repeat(original.Candidates[0],300),
                original.Plans,original.Actuals,"Spawned");
            Assert.That(snapshot.Candidates,Has.Count.EqualTo(256));Assert.That(snapshot.DroppedItems,Is.EqualTo(44));
        }
        [Test]
        public void Inspector_SelectsCapturedRequest_AndRejectsUnknownRequest()
        {
            var recorder=new GameDebugRecorder();var run=recorder.Accept(Event(1,Snapshot()),()=>new GameDebugRun());
            var inspector=new GameDebugSpawnInspector();inspector.SetRun(run);
            Assert.That(inspector.SelectRequest(1),Is.True);Assert.That(inspector.SelectRequest(42),Is.False);
            inspector.SetRun(null);Assert.That(inspector.SelectRequest(1),Is.False);
        }
        [Test]
        public void SourceOptIn_SeparatesActualDeathsFromLegacyRemovalOnlyCoverage()
        {
            var recorder=new GameDebugRecorder();var run=recorder.Accept(Event(1,null,false),()=>new GameDebugRun());
            Assert.That(run.HasDeathEvents,Is.False);
            recorder.Accept(Event(2,null,true),()=>new GameDebugRun());
            Assert.That(run.HasDeathEvents,Is.True);
        }
        [Test]
        public void Inspector_GroupsRepeatedMonsterEntriesAtSamePoint()
        {
            var recorder=new GameDebugRecorder();var run=recorder.Accept(Event(1,Snapshot()),()=>new GameDebugRun());
            run.SpawnDecisions[0].Plans.Add(new GameDebugSpawnDecision.Plan{Point="Left",Enemy="Slime",Count=2});
            var inspector=new GameDebugSpawnInspector();inspector.SetRun(run);
            Assert.That(inspector.Q("spawn-composition").Q<ListView>().itemsSource.Count,Is.EqualTo(1));
        }
    }
}
