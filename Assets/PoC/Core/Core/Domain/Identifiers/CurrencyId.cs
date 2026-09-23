// Identifies a currency type used by profile and reward contracts.


using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct CurrencyId : IEquatable<CurrencyId>
    {
        public string Value { get; }
        public bool IsEmpty => string.IsNullOrWhiteSpace(Value);

        public CurrencyId(string value) => Value = value ?? throw new ArgumentNullException(nameof(value));
        public bool Equals(CurrencyId other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is CurrencyId other && Equals(other);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
        public static bool operator ==(CurrencyId left, CurrencyId right) => left.Equals(right);
        public static bool operator !=(CurrencyId left, CurrencyId right) => !left.Equals(right);
    }
}
