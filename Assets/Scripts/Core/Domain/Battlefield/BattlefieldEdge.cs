// 터렛 ID 순서에 영향을 받지 않는 정규화된 Territory 변 식별자입니다.

using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct BattlefieldEdge : IEquatable<BattlefieldEdge>
    {
        public EntityId First { get; }
        public EntityId Second { get; }

        public BattlefieldEdge(EntityId first, EntityId second)
        {
            if (first.IsEmpty || second.IsEmpty) throw new ArgumentException("Edge vertices require valid IDs.");
            if (first == second) throw new ArgumentException("An edge requires two distinct vertices.");
            if (StringComparer.Ordinal.Compare(first.Value, second.Value) < 0)
            {
                First = first;
                Second = second;
            }
            else
            {
                First = second;
                Second = first;
            }
        }

        public bool Equals(BattlefieldEdge other) => First == other.First && Second == other.Second;
        public override bool Equals(object obj) => obj is BattlefieldEdge other && Equals(other);
        public override int GetHashCode() => unchecked((First.GetHashCode() * 397) ^ Second.GetHashCode());
        public static bool operator ==(BattlefieldEdge left, BattlefieldEdge right) => left.Equals(right);
        public static bool operator !=(BattlefieldEdge left, BattlefieldEdge right) => !left.Equals(right);
    }
}
