// Territory·Frontline·Grid를 한 Match revision으로 묶어 제공하는 읽기 전용 spatial 결과입니다.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldSpatialSnapshot
    {
        public MatchId MatchId { get; }
        public long Revision { get; }
        public IReadOnlyList<BattlefieldVertex> Vertices { get; }
        public IReadOnlyList<BattlefieldTriangle> Triangles { get; }
        public IReadOnlyList<BattlefieldEdgeAdjacency> EdgeAdjacencies { get; }
        public IReadOnlyList<BattlefieldEdge> FrontlineEdges { get; }
        public BattlefieldGridSnapshot Grid { get; }

        /// <summary>
        /// Match 범위의 새 불변 스냅샷을 조립합니다.
        /// 공간 기하 데이터는 기존 결과를 재사용하고, Match 식별자·revision·Grid를 결합합니다.
        /// </summary>
        public static BattlefieldSpatialSnapshot CreateMatchSnapshot(
            BattlefieldSpatialSnapshot geometry,
            MatchId matchId,
            long revision,
            BattlefieldGridSnapshot grid)
        {
            return new BattlefieldSpatialSnapshot(geometry, matchId, revision, grid);
        }

        internal BattlefieldSpatialSnapshot(
            IList<BattlefieldVertex> vertices,
            IList<BattlefieldTriangle> triangles,
            IList<BattlefieldEdgeAdjacency> edgeAdjacencies)
        {
            InitializeGeometry(vertices, triangles, edgeAdjacencies,
                out var vertexCopy, out var triangleCopy, out var adjacencyCopy, out var frontlineCopy);
            MatchId = new MatchId(string.Empty);
            Revision = 0;
            Grid = BattlefieldGridSnapshot.Empty;
            Vertices = vertexCopy;
            Triangles = triangleCopy;
            EdgeAdjacencies = adjacencyCopy;
            FrontlineEdges = frontlineCopy;
        }

        internal BattlefieldSpatialSnapshot(
            BattlefieldSpatialSnapshot geometry,
            MatchId matchId,
            long revision,
            BattlefieldGridSnapshot grid)
        {
            if (geometry == null) throw new System.ArgumentNullException(nameof(geometry));
            if (matchId.IsEmpty) throw new System.ArgumentException("A spatial snapshot requires a match ID.", nameof(matchId));
            if (revision < 0) throw new System.ArgumentOutOfRangeException(nameof(revision));
            if (grid == null) throw new System.ArgumentNullException(nameof(grid));

            MatchId = matchId;
            Revision = revision;
            Grid = grid;
            Vertices = geometry.Vertices;
            Triangles = geometry.Triangles;
            EdgeAdjacencies = geometry.EdgeAdjacencies;
            FrontlineEdges = geometry.FrontlineEdges;
        }

        private static void InitializeGeometry(
            IList<BattlefieldVertex> vertices,
            IList<BattlefieldTriangle> triangles,
            IList<BattlefieldEdgeAdjacency> edgeAdjacencies,
            out IReadOnlyList<BattlefieldVertex> vertexCopy,
            out IReadOnlyList<BattlefieldTriangle> triangleCopy,
            out IReadOnlyList<BattlefieldEdgeAdjacency> adjacencyCopy,
            out IReadOnlyList<BattlefieldEdge> frontlineCopy)
        {
            var copiedVertices = new List<BattlefieldVertex>(vertices);
            var copiedTriangles = new List<BattlefieldTriangle>(triangles);
            var copiedAdjacencies = new List<BattlefieldEdgeAdjacency>(edgeAdjacencies);
            var frontlineEdges = new List<BattlefieldEdge>();
            foreach (var adjacency in copiedAdjacencies)
                if (adjacency.IsBoundary) frontlineEdges.Add(adjacency.Edge);

            vertexCopy = new ReadOnlyCollection<BattlefieldVertex>(copiedVertices);
            triangleCopy = new ReadOnlyCollection<BattlefieldTriangle>(copiedTriangles);
            adjacencyCopy = new ReadOnlyCollection<BattlefieldEdgeAdjacency>(copiedAdjacencies);
            frontlineCopy = new ReadOnlyCollection<BattlefieldEdge>(frontlineEdges);
        }
    }
}
