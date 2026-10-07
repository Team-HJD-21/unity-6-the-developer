// Core 경계 변에서 계산한 Editor 전용 길이·중점·안팎 법선입니다.
// 방향은 기하 정보이며 전투 우세나 공격 방향을 의미하지 않습니다.
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public sealed class FrontlineEdgeInspection
    {
        public BattlefieldEdge Edge { get; }
        public int TriangleIndex { get; }
        public int ComponentIndex { get; }
        public Vector3 FirstPosition { get; }
        public Vector3 SecondPosition { get; }
        public Vector3 Midpoint => (FirstPosition + SecondPosition) * 0.5f;
        public Vector3 OutwardNormal { get; }
        public Vector3 InwardNormal => -OutwardNormal;
        public double Length { get; }

        internal FrontlineEdgeInspection(BattlefieldEdgeAdjacency adjacency, int component,
            Vector3 first, Vector3 second, Vector3 centroid)
        {
            Edge = adjacency.Edge;
            TriangleIndex = adjacency.FirstTriangleIndex;
            ComponentIndex = component;
            FirstPosition = first;
            SecondPosition = second;
            double dx = (double)second.x - first.x;
            double dy = (double)second.y - first.y;
            Length = System.Math.Sqrt(dx * dx + dy * dy);
            var normal = new Vector3(-(second.y - first.y), second.x - first.x, 0).normalized;
            OutwardNormal = Vector3.Dot(normal, Midpoint - centroid) < 0 ? -normal : normal;
        }
    }
}
