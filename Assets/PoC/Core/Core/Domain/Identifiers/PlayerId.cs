// Identifies a player independently of platform-specific account identifiers.


using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct PlayerId : IEquatable<PlayerId>
    {
        public string Value { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public PlayerId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(PlayerId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is PlayerId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(PlayerId left, PlayerId right) => left.Equals(right);
        public static bool operator !=(PlayerId left, PlayerId right) => !left.Equals(right);
    }
}
