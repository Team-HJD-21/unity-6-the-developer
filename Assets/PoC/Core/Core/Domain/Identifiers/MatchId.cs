// Identifies one match session without depending on Unity or transport types.


using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct MatchId : IEquatable<MatchId>
    {
        public string Value { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public MatchId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(MatchId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is MatchId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(MatchId left, MatchId right) => left.Equals(right);
        public static bool operator !=(MatchId left, MatchId right) => !left.Equals(right);
    }
}
