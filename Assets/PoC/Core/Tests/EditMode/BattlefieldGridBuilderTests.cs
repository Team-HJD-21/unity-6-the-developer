// XY Grid의 셀 매핑, 점유 집계, 경계 규칙과 불변성을 검증합니다.

using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class BattlefieldGridBuilderTests
    {
        private readonly BattlefieldGridBuilder _builder = new BattlefieldGridBuilder();

        [Test]
        public void Build_UsesIndependentAxisCountsAndStableRowMajorCellIds()
        {
            var configuration = Config(10d, 20d, 8d, 4d, 4, 2);

            var snapshot = _builder.Build(configuration, BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);

            Assert.That(snapshot.Cells, Has.Count.EqualTo(8));
            Assert.That(snapshot.Cells[0].CellId, Is.EqualTo(0));
            Assert.That(snapshot.Cells[5].X, Is.EqualTo(1));
            Assert.That(snapshot.Cells[5].Y, Is.EqualTo(1));
            Assert.That(configuration.CellWidth, Is.EqualTo(2d));
            Assert.That(configuration.CellHeight, Is.EqualTo(2d));
        }

        [Test]
        public void TryGetCellAt_AssignsInternalBoundaryToPositiveSideAndIncludesMaximumEdge()
        {
            var snapshot = _builder.Build(Config(10d, 20d, 8d, 4d, 4, 2),
                BattlefieldSpatialInput.Empty, BattlefieldDynamicSpatialInput.Empty);

            Assert.That(snapshot.TryGetCellAt(new BattlefieldPoint(8d, 20d), out var boundaryCell), Is.True);
            Assert.That(boundaryCell.CellId, Is.EqualTo(5));
            Assert.That(snapshot.TryGetCellAt(new BattlefieldPoint(14d, 22d), out var maximumCell), Is.True);
            Assert.That(maximumCell.CellId, Is.EqualTo(7));
            Assert.That(snapshot.TryGetCellAt(new BattlefieldPoint(10d, 20d), out var centerCell), Is.True);
            Assert.That(centerCell.CellId, Is.EqualTo(6));
        }

        [Test]
        public void Build_CollectsRawOccupancyAndTracksOutOfBoundsEntities()
        {
            var staticInput = new BattlefieldSpatialInput(new[]
            {
                Turret("turret-b", 11d, 21d),
                Turret("turret-a", 11d, 21d),
                Turret("outside-turret", 30d, 30d)
            });
            var dynamicInput = new BattlefieldDynamicSpatialInput(
                new[] { Player("player-1", 11d, 21d), Player("outside-player", 5d, 20d) },
                new[] { Enemy("enemy-1", 11d, 21d) });

            var snapshot = _builder.Build(Config(10d, 20d, 8d, 4d, 4, 2), staticInput, dynamicInput);
            var cell = snapshot.Cells[6];

            Assert.That(cell.CellId, Is.EqualTo(6));
            Assert.That(cell.TurretCount, Is.EqualTo(2));
            Assert.That(cell.TurretIds[0], Is.EqualTo(new EntityId("turret-a")));
            Assert.That(cell.PlayerCount, Is.EqualTo(1));
            Assert.That(cell.EnemyCount, Is.EqualTo(1));
            Assert.That(snapshot.OccupiedCells, Has.Count.EqualTo(1));
            Assert.That(snapshot.OccupiedCells[0].CellId, Is.EqualTo(cell.CellId));
            Assert.That(snapshot.OutOfBoundsTurretIds, Is.EqualTo(new[] { new EntityId("outside-turret") }));
            Assert.That(snapshot.OutOfBoundsPlayerIds, Is.EqualTo(new[] { new EntityId("outside-player") }));
            Assert.That(snapshot.OutOfBoundsEnemyIds, Is.Empty);
        }

        [Test]
        public void Build_ReturnsReadOnlyCellAndOccupantCollections()
        {
            var snapshot = _builder.Build(Config(0d, 0d, 2d, 2d, 1, 1),
                new BattlefieldSpatialInput(new[] { Turret("turret", 1d, 1d) }),
                BattlefieldDynamicSpatialInput.Empty);

            Assert.Throws<NotSupportedException>(() =>
                ((IList<BattlefieldGridCellSnapshot>)snapshot.Cells).Add(snapshot.Cells[0]));
            Assert.Throws<NotSupportedException>(() =>
                ((IList<EntityId>)snapshot.Cells[0].TurretIds).Clear());
        }

        [Test]
        public void Configuration_RejectsInvalidDimensionsAndExcessiveCellCount()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => Config(0d, 0d, 0d, 2d, 1, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Config(0d, 0d, 2d, 2d, 0, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => Config(0d, 0d, 2d, 2d, 513, 513));
        }

        [Test]
        public void DynamicInput_RejectsDuplicateIdsWithinParticipantCategory()
        {
            var duplicatePlayers = new[]
            {
                Player("same-player", 0d, 0d),
                Player("same-player", 1d, 1d)
            };

            Assert.Throws<ArgumentException>(() => new BattlefieldDynamicSpatialInput(
                duplicatePlayers, Array.Empty<EnemySpatialInput>()));
        }

        [Test]
        public void Runtime_ReconfigurationAndParticipantUpdatesPublishNewMatchRevision()
        {
            var runtime = new BattlefieldSpatialRuntime(
                new MatchId("match-grid"),
                BattlefieldSpatialInput.Empty,
                BattlefieldDynamicSpatialInput.Empty,
                Config(0d, 0d, 4d, 4d, 2, 2));
            var initial = runtime.Snapshot;

            runtime.ReconfigureGrid(Config(-2d, -2d, 8d, 8d, 4, 4));
            var reconfigured = runtime.Snapshot;
            runtime.UpdateDynamicInput(new BattlefieldDynamicSpatialInput(
                new[] { Player("player", 0d, 0d) }, Array.Empty<EnemySpatialInput>()));
            var updated = runtime.Snapshot;

            Assert.That(initial.MatchId, Is.EqualTo(new MatchId("match-grid")));
            Assert.That(initial.Revision, Is.EqualTo(1));
            Assert.That(initial.Grid.OccupiedCells, Is.Empty);
            Assert.That(reconfigured.Revision, Is.EqualTo(2));
            Assert.That(reconfigured.Grid.OccupiedCells, Is.Empty);
            Assert.That(reconfigured.Grid.Cells, Has.Count.EqualTo(16));
            Assert.That(updated.Revision, Is.EqualTo(3));
            Assert.That(updated.Grid.TryGetCellAt(new BattlefieldPoint(0d, 0d), out var cell), Is.True);
            Assert.That(cell.PlayerCount, Is.EqualTo(1));

            runtime.Dispose();
            Assert.Throws<ObjectDisposedException>(() => _ = runtime.Snapshot);
        }

        private static BattlefieldGridConfiguration Config(
            double x, double y, double width, double height, int cellsX, int cellsY) =>
            new BattlefieldGridConfiguration(new BattlefieldPoint(x, y), 0d, width, height, cellsX, cellsY);

        private static TurretSpatialInput Turret(string id, double x, double y) =>
            new TurretSpatialInput(new EntityId(id), new BattlefieldPoint(x, y));

        private static PlayerSpatialInput Player(string id, double x, double y) =>
            new PlayerSpatialInput(new EntityId(id), new BattlefieldPoint(x, y));

        private static EnemySpatialInput Enemy(string id, double x, double y) =>
            new EnemySpatialInput(new EntityId(id), new BattlefieldPoint(x, y));
    }
}
