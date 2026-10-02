// Territory 삼각형과 변 인접·외곽 변을 복사해 제공하는 읽기 전용 결과입니다.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldSpatialSnapshot
    {
        public IReadOnlyList<BattlefieldVertex> Vertices { get; }
        public IReadOnlyList<BattlefieldTriangle> Triangles { get; }
        public IReadOnlyList<BattlefieldEdgeAdjacency> EdgeAdjacencies { get; }
        public IReadOnlyList<BattlefieldEdge> FrontlineEdges { get; }

        internal BattlefieldSpatialSnapshot(
            IList<BattlefieldVertex> vertices,
            IList<BattlefieldTriangle> triangles,
            IList<BattlefieldEdgeAdjacency> edgeAdjacencies)
        {
            var vertexCopy = new List<BattlefieldVertex>(vertices);
            var triangleCopy = new List<BattlefieldTriangle>(triangles);
            var adjacencyCopy = new List<BattlefieldEdgeAdjacency>(edgeAdjacencies);
            var frontlineCopy = new List<BattlefieldEdge>();
            foreach (var adjacency in adjacencyCopy)
                if (adjacency.IsBoundary) frontlineCopy.Add(adjacency.Edge);

            Vertices = new ReadOnlyCollection<BattlefieldVertex>(vertexCopy);
            Triangles = new ReadOnlyCollection<BattlefieldTriangle>(triangleCopy);
            EdgeAdjacencies = new ReadOnlyCollection<BattlefieldEdgeAdjacency>(adjacencyCopy);
            FrontlineEdges = new ReadOnlyCollection<BattlefieldEdge>(frontlineCopy);
        }
    }
}
