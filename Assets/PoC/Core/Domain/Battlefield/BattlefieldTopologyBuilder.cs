// 터렛 점 집합에서 결정론적 Delaunay 삼각형과 외곽 변을 계산합니다.

using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldTopologyBuilder
    {
        private const double GeometryEpsilon = 1e-12;

        public BattlefieldSpatialSnapshot Build(BattlefieldSpatialInput input)
        {
            if (input == null) throw new ArgumentNullException(nameof(input));
            var turrets = new List<TurretSpatialInput>(input.Turrets);
            turrets.Sort(CompareTurrets);
            EnsureDistinctPositions(turrets);
            var points = Normalize(turrets);
            if (turrets.Count < 3 || AreCollinear(points)) return EmptySnapshot();
            var triangulated = Triangulate(points);
            var triangles = CreateStableTriangles(triangulated, turrets.Count, turrets);
            return CreateSnapshot(triangles);
        }

        private static int CompareTurrets(TurretSpatialInput left, TurretSpatialInput right)
        {
            var result = left.Position.X.CompareTo(right.Position.X);
            if (result != 0) return result;
            result = left.Position.Y.CompareTo(right.Position.Y);
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.EntityId.Value, right.EntityId.Value);
        }

        private static void EnsureDistinctPositions(IReadOnlyList<TurretSpatialInput> turrets)
        {
            for (var i = 1; i < turrets.Count; i++)
                if (turrets[i - 1].Position == turrets[i].Position)
                    throw new ArgumentException($"Turrets '{turrets[i - 1].EntityId}' and '{turrets[i].EntityId}' share a position.", nameof(turrets));
        }

        private static bool AreCollinear(IReadOnlyList<Point> points)
        {
            if (points.Count < 3) return true;
            var origin = points[0];
            var second = points[1];
            for (var i = 2; i < points.Count; i++)
            {
                if (Math.Abs(Cross(origin, second, points[i])) > GeometryEpsilon) return false;
            }
            return true;
        }

        private static List<Point> Normalize(IReadOnlyList<TurretSpatialInput> turrets)
        {
            var coordinateScale = 0d;
            foreach (var turret in turrets)
                coordinateScale = Math.Max(coordinateScale, Math.Max(Math.Abs(turret.Position.X), Math.Abs(turret.Position.Y)));
            if (coordinateScale == 0d) return new List<Point>();

            var scaled = new List<Point>(turrets.Count);
            foreach (var turret in turrets)
                scaled.Add(new Point(turret.Position.X / coordinateScale, turret.Position.Y / coordinateScale));

            var minX = scaled[0].X;
            var minY = scaled[0].Y;
            var maxX = minX;
            var maxY = minY;
            foreach (var point in scaled)
            {
                minX = Math.Min(minX, point.X);
                minY = Math.Min(minY, point.Y);
                maxX = Math.Max(maxX, point.X);
                maxY = Math.Max(maxY, point.Y);
            }
            var scale = Math.Max(maxX - minX, maxY - minY);
            var points = new List<Point>(turrets.Count + 3);
            foreach (var point in scaled)
                points.Add(new Point((point.X - minX) / scale, (point.Y - minY) / scale));
            return points;
        }

        private static List<IndexTriangle> Triangulate(List<Point> points)
        {
            var count = points.Count;
            points.Add(new Point(-32d, -16d));
            points.Add(new Point(32d, -16d));
            points.Add(new Point(0d, 32d));
            var triangles = new List<IndexTriangle> { CreateCounterClockwise(count, count + 1, count + 2, points) };

            for (var pointIndex = 0; pointIndex < count; pointIndex++)
            {
                var badTriangles = new List<IndexTriangle>();
                foreach (var triangle in triangles)
                    if (CircumcircleContains(triangle, points[pointIndex], points)) badTriangles.Add(triangle);

                var boundary = new Dictionary<IndexEdge, int>();
                foreach (var triangle in badTriangles)
                {
                    CountEdge(boundary, new IndexEdge(triangle.A, triangle.B));
                    CountEdge(boundary, new IndexEdge(triangle.B, triangle.C));
                    CountEdge(boundary, new IndexEdge(triangle.C, triangle.A));
                }
                foreach (var triangle in badTriangles) triangles.Remove(triangle);

                var boundaryEdges = new List<IndexEdge>();
                foreach (var pair in boundary)
                    if (pair.Value == 1) boundaryEdges.Add(pair.Key);
                boundaryEdges.Sort();
                foreach (var edge in boundaryEdges)
                {
                    if (Math.Abs(Cross(points[edge.A], points[edge.B], points[pointIndex])) <= GeometryEpsilon) continue;
                    triangles.Add(CreateCounterClockwise(edge.A, edge.B, pointIndex, points));
                }
            }

            triangles.RemoveAll(triangle => triangle.A >= count || triangle.B >= count || triangle.C >= count);
            return triangles;
        }

        private static bool CircumcircleContains(IndexTriangle triangle, Point point, IReadOnlyList<Point> points)
        {
            var a = points[triangle.A];
            var b = points[triangle.B];
            var c = points[triangle.C];
            var denominator = 2d * ((a.X * (b.Y - c.Y)) + (b.X * (c.Y - a.Y)) + (c.X * (a.Y - b.Y)));
            if (Math.Abs(denominator) <= GeometryEpsilon) return false;
            var aLength = (a.X * a.X) + (a.Y * a.Y);
            var bLength = (b.X * b.X) + (b.Y * b.Y);
            var cLength = (c.X * c.X) + (c.Y * c.Y);
            var centerX = ((aLength * (b.Y - c.Y)) + (bLength * (c.Y - a.Y)) + (cLength * (a.Y - b.Y))) / denominator;
            var centerY = ((aLength * (c.X - b.X)) + (bLength * (a.X - c.X)) + (cLength * (b.X - a.X))) / denominator;
            var radiusSquared = ((centerX - a.X) * (centerX - a.X)) + ((centerY - a.Y) * (centerY - a.Y));
            var distanceSquared = ((centerX - point.X) * (centerX - point.X)) + ((centerY - point.Y) * (centerY - point.Y));
            return distanceSquared <= radiusSquared + GeometryEpsilon;
        }

        private static List<BattlefieldTriangle> CreateStableTriangles(
            IReadOnlyList<IndexTriangle> source, int pointCount, IReadOnlyList<TurretSpatialInput> turrets)
        {
            var triangles = new List<BattlefieldTriangle>();
            foreach (var triangle in source)
            {
                if (triangle.A >= pointCount || triangle.B >= pointCount || triangle.C >= pointCount) continue;
                triangles.Add(new BattlefieldTriangle(
                    turrets[triangle.A].EntityId, turrets[triangle.B].EntityId, turrets[triangle.C].EntityId));
            }
            triangles.Sort(CompareTriangles);
            return triangles;
        }

        private static int CompareTriangles(BattlefieldTriangle left, BattlefieldTriangle right)
        {
            var result = StringComparer.Ordinal.Compare(left.VertexA.Value, right.VertexA.Value);
            if (result != 0) return result;
            result = StringComparer.Ordinal.Compare(left.VertexB.Value, right.VertexB.Value);
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.VertexC.Value, right.VertexC.Value);
        }

        private static BattlefieldSpatialSnapshot CreateSnapshot(IList<BattlefieldTriangle> triangles)
        {
            var edgeOwners = new Dictionary<BattlefieldEdge, List<int>>();
            for (var i = 0; i < triangles.Count; i++)
            {
                var triangle = triangles[i];
                AddOwner(edgeOwners, new BattlefieldEdge(triangle.VertexA, triangle.VertexB), i);
                AddOwner(edgeOwners, new BattlefieldEdge(triangle.VertexB, triangle.VertexC), i);
                AddOwner(edgeOwners, new BattlefieldEdge(triangle.VertexC, triangle.VertexA), i);
            }

            var edges = new List<BattlefieldEdge>(edgeOwners.Keys);
            edges.Sort(CompareEdges);
            var adjacencies = new List<BattlefieldEdgeAdjacency>(edges.Count);
            foreach (var edge in edges)
            {
                var owners = edgeOwners[edge];
                if (owners.Count > 2) throw new InvalidOperationException("A planar edge cannot border more than two triangles.");
                adjacencies.Add(new BattlefieldEdgeAdjacency(edge, owners[0], owners.Count == 1 ? -1 : owners[1]));
            }
            return new BattlefieldSpatialSnapshot(triangles, adjacencies);
        }

        private static int CompareEdges(BattlefieldEdge left, BattlefieldEdge right)
        {
            var result = StringComparer.Ordinal.Compare(left.First.Value, right.First.Value);
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.Second.Value, right.Second.Value);
        }

        private static void AddOwner(IDictionary<BattlefieldEdge, List<int>> owners, BattlefieldEdge edge, int triangleIndex)
        {
            if (!owners.TryGetValue(edge, out var list))
            {
                list = new List<int>(2);
                owners.Add(edge, list);
            }
            list.Add(triangleIndex);
        }

        private static BattlefieldSpatialSnapshot EmptySnapshot() =>
            new BattlefieldSpatialSnapshot(new List<BattlefieldTriangle>(), new List<BattlefieldEdgeAdjacency>());

        private static void CountEdge(IDictionary<IndexEdge, int> counts, IndexEdge edge)
        {
            counts.TryGetValue(edge, out var count);
            counts[edge] = count + 1;
        }

        private static IndexTriangle CreateCounterClockwise(int a, int b, int c, IReadOnlyList<Point> points) =>
            Cross(points[a], points[b], points[c]) >= 0d ? new IndexTriangle(a, b, c) : new IndexTriangle(b, a, c);

        private static double Cross(Point a, Point b, Point c) =>
            ((b.X - a.X) * (c.Y - a.Y)) - ((b.Y - a.Y) * (c.X - a.X));

        private readonly struct Point
        {
            public double X { get; }
            public double Y { get; }
            public Point(double x, double y) { X = x; Y = y; }
        }

        private readonly struct IndexTriangle : IEquatable<IndexTriangle>
        {
            public int A { get; }
            public int B { get; }
            public int C { get; }
            public IndexTriangle(int a, int b, int c) { A = a; B = b; C = c; }
            public bool Equals(IndexTriangle other) => A == other.A && B == other.B && C == other.C;
            public override bool Equals(object obj) => obj is IndexTriangle other && Equals(other);
            public override int GetHashCode() => unchecked(((A * 397) ^ B) * 397 ^ C);
        }

        private readonly struct IndexEdge : IEquatable<IndexEdge>, IComparable<IndexEdge>
        {
            public int A { get; }
            public int B { get; }
            public IndexEdge(int a, int b) { A = Math.Min(a, b); B = Math.Max(a, b); }
            public int CompareTo(IndexEdge other) { var result = A.CompareTo(other.A); return result != 0 ? result : B.CompareTo(other.B); }
            public bool Equals(IndexEdge other) => A == other.A && B == other.B;
            public override bool Equals(object obj) => obj is IndexEdge other && Equals(other);
            public override int GetHashCode() => unchecked((A * 397) ^ B);
        }
    }
}
