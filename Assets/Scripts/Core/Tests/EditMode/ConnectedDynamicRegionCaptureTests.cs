// CU 시작 셀·삼각형 투영·4방향 연결과 점유 회수를 함께 검증합니다.
// 멀리 떨어진 셀, Grid 경계, 잘못된 시작 영역이 점유를 위조하지 않는지 확인합니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class ConnectedDynamicRegionCaptureTests
    {
        private static RegionGridDefinition Definition() => new RegionGridDefinition(
            new BattlefieldGridConfiguration(new BattlefieldPoint(2, 2), 0, 4, 4, 4, 4));
        private static RegionTurretInput Turret(string id, double x, double y, bool active = true)
            => new RegionTurretInput(new EntityId(id), new BattlefieldPoint(x, y), active, false);

        [Test]
        public void EmptyLayoutKeepsOneCuCellWithoutInventingTriangle()
        {
            var result = new DynamicRegionCapturePolicy().EvaluateConnected(Definition(),
                Array.Empty<RegionTurretInput>(), new BattlefieldPoint(.5, .5));
            CollectionAssert.AreEqual(new[] { 0 }, result.CellIndices);
            Assert.That(result.InitialCellCount, Is.EqualTo(1));
            Assert.That(result.Geometry.Vertices.Count, Is.EqualTo(1));
            Assert.That(result.Geometry.Triangles, Is.Empty);
        }

        [Test]
        public void CuAndTwoActiveTurretsExpandAndOffReturnsOnlyTheSeed()
        {
            var policy = new DynamicRegionCapturePolicy();
            var active = new[] { Turret("a", 3.5, .5), Turret("b", .5, 3.5) };
            var result = policy.EvaluateConnected(Definition(), active, new BattlefieldPoint(.5, .5));
            Assert.That(result.Geometry.Triangles.Count, Is.EqualTo(1));
            Assert.That(result.CellIndices.Count, Is.GreaterThan(1));
            var next = policy.EvaluateConnected(Definition(),
                new[] { active[0], Turret("b", .5, 3.5, false) }, new BattlefieldPoint(.5, .5));
            CollectionAssert.AreEqual(new[] { 0 }, next.CellIndices);
            Assert.That(active.All(t => t.IsActivated), Is.True);
        }

        [Test]
        public void FourWayConnectivityExcludesDiagonalAndDoesNotWrapRows()
        {
            CollectionAssert.AreEqual(new[] { 0 }, RegionGridConnectivity.RetainConnected(Definition(), new[] { 0, 5 }, 0));
            CollectionAssert.AreEqual(new[] { 3 }, RegionGridConnectivity.RetainConnected(Definition(), new[] { 3, 4 }, 3));
            CollectionAssert.AreEqual(new[] { 0, 1, 5, 6 },
                RegionGridConnectivity.RetainConnected(Definition(), new[] { 6, 5, 0, 1, 15 }, 0));
        }

        [Test]
        public void ExplicitStartAreaMustIncludeCuAndBeConnected()
        {
            var policy = new DynamicRegionCapturePolicy();
            var result = policy.EvaluateConnected(Definition(), Array.Empty<RegionTurretInput>(),
                new BattlefieldPoint(.5, .5), new[] { 4, 0, 1 });
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, result.CellIndices);
            Assert.Throws<ArgumentException>(() => policy.EvaluateConnected(Definition(),
                Array.Empty<RegionTurretInput>(), new BattlefieldPoint(.5, .5), new[] { 1 }));
            Assert.Throws<ArgumentException>(() => policy.EvaluateConnected(Definition(),
                Array.Empty<RegionTurretInput>(), new BattlefieldPoint(.5, .5), new[] { 0, 5 }));
        }

        [Test]
        public void InvalidCuAndStartCellAreRejectedBeforeAnyResult()
        {
            var policy = new DynamicRegionCapturePolicy();
            Assert.Throws<ArgumentException>(() => policy.EvaluateConnected(Definition(),
                Array.Empty<RegionTurretInput>(), new BattlefieldPoint(-.01, 0)));
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.EvaluateConnected(Definition(),
                Array.Empty<RegionTurretInput>(), new BattlefieldPoint(0, 0), new[] { 0, 16 }));
        }

        [Test]
        public void MaximumGridEdgeUsesLastCellAndInputOrderDoesNotChangeResult()
        {
            var policy = new DynamicRegionCapturePolicy();
            var nodes = new[] { Turret("a", .5, 3.5), Turret("b", 3.5, .5) };
            var first = policy.EvaluateConnected(Definition(), nodes, new BattlefieldPoint(4, 4));
            var second = policy.EvaluateConnected(Definition(), nodes.Reverse(), new BattlefieldPoint(4, 4));
            Assert.That(first.CellIndices, Does.Contain(15));
            CollectionAssert.AreEqual(first.CellIndices, second.CellIndices);
        }

        [Test]
        public void ReservedCuIdentityCannotCollideWithTurretInput()
        {
            Assert.Throws<ArgumentException>(() => new DynamicRegionCapturePolicy().EvaluateConnected(Definition(),
                new[] { Turret(DynamicRegionCapturePolicy.ControlUnitNodeId, 1, 1) }, new BattlefieldPoint(0, 0)));
        }
    }
}
