// Match 안에서 실행 중인 개체를 식별합니다.


using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct EntityId : IEquatable<EntityId>
    {
        public string Value { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public EntityId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(EntityId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is EntityId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(EntityId left, EntityId right) => left.Equals(right);
        public static bool operator !=(EntityId left, EntityId right) => !left.Equals(right);
    }
}
