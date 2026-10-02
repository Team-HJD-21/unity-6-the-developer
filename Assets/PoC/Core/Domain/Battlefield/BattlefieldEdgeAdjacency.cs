// 한 변에 맞닿는 Territory 삼각형 인덱스를 보관하는 불변 값입니다.

using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct BattlefieldEdgeAdjacency
    {
        public BattlefieldEdge Edge { get; }
        public int FirstTriangleIndex { get; }
        public int SecondTriangleIndex { get; }
        public bool IsBoundary => SecondTriangleIndex < 0;

        internal BattlefieldEdgeAdjacency(BattlefieldEdge edge, int firstTriangleIndex, int secondTriangleIndex)
        {
            if (firstTriangleIndex < 0) throw new ArgumentOutOfRangeException(nameof(firstTriangleIndex));
            if (secondTriangleIndex < -1) throw new ArgumentOutOfRangeException(nameof(secondTriangleIndex));
            Edge = edge;
            FirstTriangleIndex = firstTriangleIndex;
            SecondTriangleIndex = secondTriangleIndex;
        }
    }
}
