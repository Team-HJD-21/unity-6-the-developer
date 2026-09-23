// Represents a signed change for a typed currency identifier.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class CurrencyDelta
    {
        public CurrencyId CurrencyId { get; }
        public long Amount { get; }

        public CurrencyDelta(CurrencyId currencyId, long amount)
        {
            if (currencyId.IsEmpty) throw new ArgumentException("Currency delta requires a valid currency ID.", nameof(currencyId));
            CurrencyId = currencyId;
            Amount = amount;
        }
    }
}
