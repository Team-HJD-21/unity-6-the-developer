// 전장 topology 생성의 경계 입력과 결정론적 결과를 검증합니다.

using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class BattlefieldTopologyBuilderTests
    {
        private readonly BattlefieldTopologyBuilder _builder = new BattlefieldTopologyBuilder();

        [Test]
        public void Build_SquarePoints_ReturnsTwoTrianglesAndFourFrontlineEdges()
        {
            var snapshot = _builder.Build(CreateInput(
                Point("a", 0, 0), Point("b", 1, 0), Point("c", 1, 1), Point("d", 0, 1)));

            Assert.That(snapshot.Triangles, Has.Count.EqualTo(2));
            Assert.That(snapshot.EdgeAdjacencies, Has.Count.EqualTo(5));
            Assert.That(snapshot.FrontlineEdges, Has.Count.EqualTo(4));
            Assert.That(snapshot.EdgeAdjacencies.Count(edge => !edge.IsBoundary), Is.EqualTo(1));
        }

        [Test]
        public void Build_InputOrderChanges_ReturnsTheSameCanonicalTopology()
        {
            var forward = _builder.Build(CreateInput(
                Point("a", 0, 0), Point("b", 2, 0), Point("c", 2, 1), Point("d", 0, 1), Point("e", 1, 0.4)));
            var reverse = _builder.Build(CreateInput(
                Point("e", 1, 0.4), Point("d", 0, 1), Point("c", 2, 1), Point("b", 2, 0), Point("a", 0, 0)));

            Assert.That(forward.Triangles, Has.Count.EqualTo(reverse.Triangles.Count));
            for (var i = 0; i < forward.Triangles.Count; i++)
            {
                Assert.That(forward.Triangles[i].VertexA, Is.EqualTo(reverse.Triangles[i].VertexA));
                Assert.That(forward.Triangles[i].VertexB, Is.EqualTo(reverse.Triangles[i].VertexB));
                Assert.That(forward.Triangles[i].VertexC, Is.EqualTo(reverse.Triangles[i].VertexC));
            }
            Assert.That(forward.FrontlineEdges, Is.EqualTo(reverse.FrontlineEdges));
        }

        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void Build_FewerThanThreePoints_ReturnsEmptyTopology(int count)
        {
            var points = new TurretSpatialInput[count];
            for (var i = 0; i < count; i++) points[i] = Point($"t{i}", i, 0);

            var snapshot = _builder.Build(new BattlefieldSpatialInput(points));

            Assert.That(snapshot.Triangles, Is.Empty);
            Assert.That(snapshot.EdgeAdjacencies, Is.Empty);
            Assert.That(snapshot.FrontlineEdges, Is.Empty);
        }

        [Test]
        public void Build_CollinearPoints_ReturnsEmptyTopology()
        {
            var snapshot = _builder.Build(CreateInput(
                Point("a", -3, -6), Point("b", 0, 0), Point("c", 2, 4), Point("d", 5, 10)));

            Assert.That(snapshot.Triangles, Is.Empty);
            Assert.That(snapshot.FrontlineEdges, Is.Empty);
        }

        [Test]
        public void Build_CoincidentPositions_RejectsAmbiguousTopology()
        {
            var input = CreateInput(Point("a", 0, 0), Point("b", 0, 0), Point("c", 1, 1));

            Assert.Throws<ArgumentException>(() => _builder.Build(input));
        }

        private static BattlefieldSpatialInput CreateInput(params TurretSpatialInput[] turrets) =>
            new BattlefieldSpatialInput(turrets);

        private static TurretSpatialInput Point(string id, double x, double y) =>
            new TurretSpatialInput(new EntityId(id), new BattlefieldPoint(x, y));
    }
}
