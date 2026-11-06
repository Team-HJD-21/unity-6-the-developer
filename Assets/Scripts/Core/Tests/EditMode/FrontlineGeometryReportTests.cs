// 읽기 전용 전선 진단의 면적·외곽 길이·연결군·안팎 법선과 선택 모드를 검증합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
using TeamHJD.Game.Infrastructure.Fakes;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class FrontlineGeometryReportTests
    {
        [Test]
        public void Square_ReportMeasuresGeometry_WithoutChangingSnapshot()
        {
            BattlefieldSpatialSnapshot snapshot = Square();
            var report = new FrontlineGeometryReport(snapshot, 3);
            Assert.That(report.Snapshot, Is.SameAs(snapshot));
            Assert.That(report.Area, Is.EqualTo(4).Within(0.00001));
            Assert.That(report.Perimeter, Is.EqualTo(8).Within(0.00001));
            Assert.That(report.ComponentCount, Is.EqualTo(1));
            Assert.That(report.Edges.Count, Is.EqualTo(4));
            Assert.That(snapshot.Triangles.Count, Is.EqualTo(2));
            foreach (FrontlineEdgeInspection edge in report.Edges)
            {
                Assert.That(edge.FirstPosition.z, Is.EqualTo(3));
                Assert.That(Vector3.Dot(edge.OutwardNormal, edge.Midpoint - new Vector3(1, 1, 3)), Is.GreaterThan(0));
                Assert.That(edge.InwardNormal, Is.EqualTo(-edge.OutwardNormal));
            }
        }

        [Test]
        public void EdgeIdentity_IsCanonical_AndMissingBoundaryIsNotResolved()
        {
            var report = new FrontlineGeometryReport(Square());
            FrontlineEdgeInspection selected = report.Find(new BattlefieldEdge(new EntityId("b"), new EntityId("a")));
            Assert.That(selected, Is.Not.Null);
            Assert.That(selected.Length, Is.EqualTo(2));
            Assert.That(report.Find(new BattlefieldEdge(new EntityId("a"), new EntityId("missing"))), Is.Null);
            var rebuilt = new FrontlineGeometryReport(Square());
            Assert.That(rebuilt.Find(selected.Edge), Is.Not.Null);
        }

        [Test]
        public void CollinearTurrets_ReportHasNoTerritoryOrFrontline()
        {
            var snapshot = new BattlefieldTopologyBuilder().Build(new BattlefieldSpatialInput(new[]
                { Point("a", 0, 0), Point("b", 2, 0), Point("c", 4, 0) }));
            var report = new FrontlineGeometryReport(snapshot);
            Assert.That(report.Area, Is.Zero);
            Assert.That(report.Perimeter, Is.Zero);
            Assert.That(report.ComponentCount, Is.Zero);
            Assert.That(report.Edges, Is.Empty);
        }

        [Test]
        public void SharedGeometryRevision_ReusesDerivedEdges_ButPublishesNewSnapshotIdentity()
        {
            BattlefieldSpatialSnapshot geometry = Square();
            var grid = new BattlefieldGridBuilder().Build(BattlefieldGridConfiguration.Default,
                BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);
            var first = BattlefieldSpatialSnapshot.CreateMatchSnapshot(geometry, new MatchId("test"), 1, grid);
            var next = BattlefieldSpatialSnapshot.CreateMatchSnapshot(geometry, new MatchId("test"), 2, grid);
            var original = FrontlineGeometryReport.Create(first, 0);
            var updated = FrontlineGeometryReport.Create(next, 0, original);
            Assert.That(updated.Snapshot, Is.SameAs(next));
            Assert.That(updated.Edges, Is.SameAs(original.Edges));
            Assert.That(updated.Snapshot.Revision, Is.EqualTo(2));
        }

        [Test]
        public void Selection_RematchesSameIdsOnRevision_AndClearsMissingEdgeAndEndedMatch()
        {
            var gameObject = new GameObject("Frontline selection test");
            var root = gameObject.AddComponent<AppRoot>();
            try
            {
                root.Initialize(new AppServices(new FakeProfileService(), new FakeContentCatalog(),
                    new FakePlatformService(new PlatformUser(new PlayerId("test"), "Test")),
                    new FakeSceneFlow()));
                var id = new MatchId("frontline-test");
                root.StartMatch(new MatchConfig(id, MatchMode.Story, new StageId("test"),
                    new DefinitionId("test"), "test", 1, Array.Empty<PlayerLoadoutSnapshot>()),
                    new MatchState(id, MatchPhase.Preparing, Array.Empty<PlayerState>(),
                        new ControlUnitState(1, 1, 1, 1), new WaveState(0, 1, 0, 0),
                        Array.Empty<TurretState>(), Array.Empty<EnemyState>(), Array.Empty<SectorState>()), new TestModeRules(),
                    new BattlefieldSpatialInput(new[] { Point("a", 0, 0), Point("b", 2, 0), Point("c", 0, 2) }));
                BattlefieldDebugSession.Bind(root);
                FrontlineInspectionSession.Select(new BattlefieldEdge(new EntityId("a"), new EntityId("b")));
                Assert.That(FrontlineInspectionSession.Selected, Is.Not.Null);
                root.UpdateBattlefieldTurretLayout(new BattlefieldSpatialInput(
                    new[] { Point("a", 0, 0), Point("b", 4, 0), Point("c", 0, 2) }));
                Assert.That(FrontlineInspectionSession.Selected.Length, Is.EqualTo(4));
                root.UpdateBattlefieldTurretLayout(new BattlefieldSpatialInput(
                    new[] { Point("a", 0, 0), Point("c", 0, 2), Point("d", 4, 0) }));
                Assert.That(FrontlineInspectionSession.Selected, Is.Null);
                FrontlineInspectionSession.Select(new BattlefieldEdge(new EntityId("a"), new EntityId("d")));
                Assert.That(FrontlineInspectionSession.Selected, Is.Not.Null);
                root.EndCurrentMatch();
                Assert.That(FrontlineInspectionSession.Selected, Is.Null);
                Assert.That(FrontlineInspectionSession.Report, Is.Null);
            }
            finally
            {
                BattlefieldDebugSession.Bind(null);
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void EdgePicking_RestoresPreviousCellMode_WithoutChangingSnapshot()
        {
            string before = JsonUtility.ToJson(BattlefieldDebugSession.Display);
            BattlefieldSpatialSnapshot snapshot = BattlefieldDebugSession.Snapshot;
            try
            {
                FrontlineInspectionSession.SetPickingEnabled(false);
                BattlefieldDebugSession.Display.IsCellPickingEnabled = true;
                FrontlineInspectionSession.SetPickingEnabled(true);
                Assert.That(BattlefieldDebugSession.Display.IsCellPickingEnabled, Is.False);
                FrontlineInspectionSession.SetPickingEnabled(false);
                Assert.That(BattlefieldDebugSession.Display.IsCellPickingEnabled, Is.True);
                Assert.That(BattlefieldDebugSession.Snapshot, Is.SameAs(snapshot));
            }
            finally
            {
                FrontlineInspectionSession.SetPickingEnabled(false);
                JsonUtility.FromJsonOverwrite(before, BattlefieldDebugSession.Display);
                BattlefieldDebugSession.NotifyDisplayChanged();
            }
        }

        [Test]
        public void CellModeEnabledAfterEdgeMode_YieldsWithoutOverwritingNewChoice()
        {
            string before = JsonUtility.ToJson(BattlefieldDebugSession.Display);
            try
            {
                FrontlineInspectionSession.SetPickingEnabled(false);
                BattlefieldDebugSession.Display.IsCellPickingEnabled = false;
                FrontlineInspectionSession.SetPickingEnabled(true);
                BattlefieldDebugSession.Display.IsCellPickingEnabled = true;
                FrontlineInspectionSession.YieldToCellPicking();
                Assert.That(FrontlineInspectionSession.IsPickingEnabled, Is.False);
                Assert.That(BattlefieldDebugSession.Display.IsCellPickingEnabled, Is.True);
            }
            finally
            {
                FrontlineInspectionSession.SetPickingEnabled(false);
                JsonUtility.FromJsonOverwrite(before, BattlefieldDebugSession.Display);
                BattlefieldDebugSession.NotifyDisplayChanged();
            }
        }

        private static BattlefieldSpatialSnapshot Square() => new BattlefieldTopologyBuilder().Build(
            new BattlefieldSpatialInput(new[] { Point("a", 0, 0), Point("b", 2, 0), Point("c", 2, 2), Point("d", 0, 2) }));
        private static TurretSpatialInput Point(string id, double x, double y) =>
            new TurretSpatialInput(new EntityId(id), new BattlefieldPoint(x, y));

        private sealed class TestModeRules : IModeRules
        {
            public bool IsVictory(MatchState state) => false;
            public bool IsDefeat(MatchState state) => false;
            public MatchResult CreateResult(MatchState state, MatchOutcome outcome) =>
                new MatchResult(state.MatchId, outcome, 0, DateTimeOffset.UtcNow);
            public RewardReceipt CreateRewardReceipt(MatchResult result) =>
                new RewardReceipt(result.MatchId, result.Outcome, Array.Empty<CurrencyDelta>());
        }
    }
}
