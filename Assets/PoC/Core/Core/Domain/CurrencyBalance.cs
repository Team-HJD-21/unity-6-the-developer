// Represents one currency amount stored in a player profile snapshot.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class CurrencyBalance
    {
        public CurrencyId CurrencyId { get; }
        public long Amount { get; }

        public CurrencyBalance(CurrencyId currencyId, long amount)
        {
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            CurrencyId = currencyId;
            Amount = amount;
        }
    }
}
