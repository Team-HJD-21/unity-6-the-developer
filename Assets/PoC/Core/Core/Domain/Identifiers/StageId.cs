// Identifies authored stage content without exposing a Unity asset reference.


using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct StageId : IEquatable<StageId>
    {
        public string Value { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public StageId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(StageId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is StageId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(StageId left, StageId right) => left.Equals(right);
        public static bool operator !=(StageId left, StageId right) => !left.Equals(right);
    }
}
