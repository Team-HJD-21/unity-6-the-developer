// 현재 불변 Core snapshot의 기하 면적·외곽 길이·삼각형 연결군을 읽기 전용으로 요약합니다.
// triangulation을 다시 만들거나 스폰·전투 정책을 변경하지 않습니다.
using System.Collections.Generic;
using TeamHJD.Game.Domain;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Editor
{
    public sealed class FrontlineGeometryReport
    {
        private readonly float _originZ;
        public BattlefieldSpatialSnapshot Snapshot { get; }
        public IReadOnlyList<FrontlineEdgeInspection> Edges { get; }
        public double Area { get; }
        public double Perimeter { get; }
        public int ComponentCount { get; }

        public FrontlineGeometryReport(BattlefieldSpatialSnapshot snapshot, float originZ = 0)
        {
            _originZ = originZ;
            Snapshot = snapshot ?? throw new System.ArgumentNullException(nameof(snapshot));
            var positions = new Dictionary<EntityId, Vector3>();
            foreach (BattlefieldVertex vertex in snapshot.Vertices)
                positions.Add(vertex.EntityId, new Vector3((float)vertex.Position.X, (float)vertex.Position.Y, originZ));
            var groups = new int[snapshot.Triangles.Count];
            for (int i = 0; i < groups.Length; i++) groups[i] = i;
            foreach (BattlefieldEdgeAdjacency adjacency in snapshot.EdgeAdjacencies)
                if (!adjacency.IsBoundary)
                    groups[FindRoot(groups, adjacency.SecondTriangleIndex)] = FindRoot(groups, adjacency.FirstTriangleIndex);
            var componentIds = new Dictionary<int, int>();
            for (int i = 0; i < groups.Length; i++)
            {
                int root = FindRoot(groups, i);
                if (!componentIds.ContainsKey(root)) componentIds.Add(root, componentIds.Count);
                BattlefieldTriangle triangle = snapshot.Triangles[i];
                Vector3 a = positions[triangle.VertexA], b = positions[triangle.VertexB], c = positions[triangle.VertexC];
                Area += System.Math.Abs(((double)b.x - a.x) * ((double)c.y - a.y) -
                    ((double)b.y - a.y) * ((double)c.x - a.x)) * 0.5d;
            }
            ComponentCount = componentIds.Count;
            var edges = new List<FrontlineEdgeInspection>(snapshot.FrontlineEdges.Count);
            foreach (BattlefieldEdgeAdjacency adjacency in snapshot.EdgeAdjacencies)
            {
                if (!adjacency.IsBoundary) continue;
                BattlefieldTriangle triangle = snapshot.Triangles[adjacency.FirstTriangleIndex];
                Vector3 centroid = (positions[triangle.VertexA] + positions[triangle.VertexB] + positions[triangle.VertexC]) / 3f;
                var inspection = new FrontlineEdgeInspection(adjacency,
                    componentIds[FindRoot(groups, adjacency.FirstTriangleIndex)],
                    positions[adjacency.Edge.First], positions[adjacency.Edge.Second], centroid);
                edges.Add(inspection);
                Perimeter += inspection.Length;
            }
            Edges = edges.AsReadOnly();
        }

        private FrontlineGeometryReport(BattlefieldSpatialSnapshot snapshot, FrontlineGeometryReport previous)
        {
            Snapshot = snapshot;
            _originZ = previous._originZ;
            Edges = previous.Edges;
            Area = previous.Area;
            Perimeter = previous.Perimeter;
            ComponentCount = previous.ComponentCount;
        }

        public static FrontlineGeometryReport Create(BattlefieldSpatialSnapshot snapshot, float originZ,
            FrontlineGeometryReport previous = null)
        {
            if (snapshot == null) return null;
            // 동적 Grid만 바뀌면 Core는 동일 기하 컬렉션을 공유합니다. Editor도 파생 계산을 재사용합니다.
            if (previous != null && previous._originZ == originZ &&
                ReferenceEquals(previous.Snapshot.Vertices, snapshot.Vertices) &&
                ReferenceEquals(previous.Snapshot.Triangles, snapshot.Triangles) &&
                ReferenceEquals(previous.Snapshot.EdgeAdjacencies, snapshot.EdgeAdjacencies))
                return new FrontlineGeometryReport(snapshot, previous);
            return new FrontlineGeometryReport(snapshot, originZ);
        }

        public FrontlineEdgeInspection Find(BattlefieldEdge edge)
        {
            foreach (FrontlineEdgeInspection candidate in Edges)
                if (candidate.Edge == edge) return candidate;
            return null;
        }

        private static int FindRoot(int[] groups, int index)
        {
            while (groups[index] != index)
            {
                groups[index] = groups[groups[index]];
                index = groups[index];
            }
            return index;
        }
    }
}
