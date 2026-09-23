// Identifies reusable authored gameplay or content data.


using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct DefinitionId : IEquatable<DefinitionId>
    {
        public string Value { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public DefinitionId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(DefinitionId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is DefinitionId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(DefinitionId left, DefinitionId right) => left.Equals(right);
        public static bool operator !=(DefinitionId left, DefinitionId right) => !left.Equals(right);
    }
}
