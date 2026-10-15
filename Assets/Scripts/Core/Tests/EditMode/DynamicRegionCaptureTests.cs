// 삼각형→셀 투영과 활성 상태/점령 회수의 분리를 검증합니다.
// 경계 접촉·중복 위치·공선·외부 입력에서도 최종 Grid만 점령 원본인지 확인합니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class DynamicRegionCaptureTests
    {
        private static RegionGridDefinition Definition() => new RegionGridDefinition(
            new BattlefieldGridConfiguration(new BattlefieldPoint(2, 2), 0, 4, 4, 4, 4));

        private static RegionTurretInput Turret(string id, double x, double y, bool active = true, bool destroyed = false)
            => new RegionTurretInput(new EntityId(id), new BattlefieldPoint(x, y), active, destroyed);

        [Test]
        public void Rasterizer_OnlyPositiveAreaCells_NoLineOrPointContacts()
        {
            var cells = new TriangleGridRasterizer().Rasterize(new BattlefieldPoint(0, 0),
                new BattlefieldPoint(2, 0), new BattlefieldPoint(0, 2), Definition().Geometry);
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, cells);
            CollectionAssert.DoesNotContain(cells, 5);
        }

        [Test]
        public void Rasterizer_ClipsOutsideTriangleAndHandlesZeroArea()
        {
            var rasterizer = new TriangleGridRasterizer();
            var grid = Definition().Geometry;
            CollectionAssert.IsEmpty(rasterizer.Rasterize(new BattlefieldPoint(0, 0),
                new BattlefieldPoint(1, 1), new BattlefieldPoint(2, 2), grid));
            CollectionAssert.IsEmpty(rasterizer.Rasterize(new BattlefieldPoint(-2, -2),
                new BattlefieldPoint(0, -2), new BattlefieldPoint(-2, 0), grid));
            Assert.That(rasterizer.Rasterize(new BattlefieldPoint(-1, -1),
                new BattlefieldPoint(5, -1), new BattlefieldPoint(-1, 5), grid), Does.Contain(0));
        }

        [Test]
        public void Rasterizer_CenteredNonSquareCellsAreMappedInWorldXY()
        {
            var grid = new BattlefieldGridConfiguration(new BattlefieldPoint(10, 20), 100, 8, 4, 4, 4);
            var cells = new TriangleGridRasterizer().Rasterize(new BattlefieldPoint(6, 18),
                new BattlefieldPoint(10, 18), new BattlefieldPoint(6, 20), grid);
            CollectionAssert.AreEqual(new[] { 0, 1, 4 }, cells);
        }

        [Test]
        public void Policy_OffAndDestroyedInputsDoNotCreateArea()
        {
            var inputs = new[] { Turret("a", 0, 0), Turret("b", 2, 0), Turret("off", 0, 2, false), Turret("dead", 0, 3, true, true) };
            RegionCaptureEvaluation result = new DynamicRegionCapturePolicy().Evaluate(Definition(), inputs);
            Assert.That(result.Geometry.Triangles.Count, Is.Zero);
            Assert.That(result.CellIndices, Is.Empty);
            Assert.That(inputs[0].IsActivated, Is.True);
            Assert.That(inputs[1].IsActivated, Is.True);
        }

        [Test]
        public void Policy_CollinearAndDuplicatePositionsAreSafeAndDeterministic()
        {
            var policy = new DynamicRegionCapturePolicy();
            var collinear = new[] { Turret("a", 0, 0), Turret("b", 1, 1), Turret("c", 2, 2) };
            Assert.That(policy.Evaluate(Definition(), collinear).CellIndices, Is.Empty);
            var inputs = new[] { Turret("duplicate", 0, 0), Turret("a", 0, 0), Turret("b", 2, 0), Turret("c", 0, 2) };
            var first = policy.Evaluate(Definition(), inputs);
            var reversed = policy.Evaluate(Definition(), inputs.Reverse());
            Assert.That(first.DuplicatePositionCount, Is.EqualTo(1));
            Assert.That(first.Geometry.Triangles.Count, Is.EqualTo(1));
            CollectionAssert.AreEqual(first.CellIndices, reversed.CellIndices);
            Assert.Throws<ArgumentException>(() => policy.Evaluate(Definition(),
                new[] { Turret("same", 0, 0), Turret("same", 2, 2) }));
        }

        [Test]
        public void Policy_FourCornersCoverGridWithoutDoubleCountingSharedTriangleEdges()
        {
            var inputs = new[] { Turret("a", 0, 0), Turret("b", 4, 0), Turret("c", 0, 4), Turret("d", 4, 4) };
            var result = new DynamicRegionCapturePolicy().Evaluate(Definition(), inputs);
            Assert.That(result.Geometry.Triangles.Count, Is.EqualTo(2));
            Assert.That(result.CellIndices.Count, Is.EqualTo(16));
            Assert.That(result.CellIndices.Distinct().Count(), Is.EqualTo(16));
        }

        [Test]
        public void Recapture_ReleasesGridButDoesNotDeactivateRemainingTurrets()
        {
            var policy = new DynamicRegionCapturePolicy();
            var inputs = new[] { Turret("a", 0, 0), Turret("b", 2, 0), Turret("c", 0, 2) };
            using var runtime = new RegionRuntime(new MatchId("dynamic"), Definition(), RegionPolicyMode.Dynamic);
            var evaluation = policy.Evaluate(runtime.Snapshot.Definition, inputs);
            runtime.TryApply(runtime.Snapshot, evaluation.CellIndices, RegionCaptureUpdateMode.Replace);
            Assert.That(runtime.Snapshot.CapturedCellCount, Is.EqualTo(3));
            var afterOff = new[] { inputs[0], inputs[1], Turret("c", 0, 2, false) };
            var next = policy.Evaluate(runtime.Snapshot.Definition, afterOff);
            runtime.TryApply(runtime.Snapshot, next.CellIndices, RegionCaptureUpdateMode.Replace);
            Assert.That(runtime.Snapshot.CapturedCellCount, Is.Zero);
            Assert.That(afterOff[0].IsActivated, Is.True);
            Assert.That(afterOff[1].IsActivated, Is.True);
        }
    }
}
