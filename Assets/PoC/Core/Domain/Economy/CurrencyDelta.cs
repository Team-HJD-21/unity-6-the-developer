// 재화 식별자와 연결된 증감량을 나타냅니다.


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
