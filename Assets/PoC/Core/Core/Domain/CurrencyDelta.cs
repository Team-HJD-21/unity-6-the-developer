// Represents a signed change for a typed currency identifier.


namespace TeamHJD.Game.Domain
{
    public sealed class CurrencyDelta
    {
        public CurrencyId CurrencyId { get; }
        public long Amount { get; }

        public CurrencyDelta(CurrencyId currencyId, long amount)
        {
            CurrencyId = currencyId;
            Amount = amount;
        }
    }
}
