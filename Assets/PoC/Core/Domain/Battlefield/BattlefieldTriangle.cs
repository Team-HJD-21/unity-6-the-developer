// 세 개의 터렛 ID로 정의되는 방향 비의존 Territory 삼각형입니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class BattlefieldTriangle
    {
        public EntityId VertexA { get; }
        public EntityId VertexB { get; }
        public EntityId VertexC { get; }

        internal BattlefieldTriangle(EntityId first, EntityId second, EntityId third)
        {
            if (first.IsEmpty || second.IsEmpty || third.IsEmpty) throw new ArgumentException("Triangle vertices require valid IDs.");
            if (first == second || second == third || first == third) throw new ArgumentException("Triangle vertices must be unique.");
            var ids = new[] { first, second, third };
            Array.Sort(ids, (left, right) => StringComparer.Ordinal.Compare(left.Value, right.Value));
            VertexA = ids[0];
            VertexB = ids[1];
            VertexC = ids[2];
        }
    }
}
