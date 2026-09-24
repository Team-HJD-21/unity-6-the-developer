// 플레이어 프로필 Snapshot에 저장되는 재화 수량을 나타냅니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class CurrencyBalance
    {
        public CurrencyId CurrencyId { get; }
        public long Amount { get; }

        public CurrencyBalance(CurrencyId currencyId, long amount)
        {
            if (currencyId.IsEmpty) throw new ArgumentException("Currency balance requires a valid currency ID.", nameof(currencyId));
            if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
            CurrencyId = currencyId;
            Amount = amount;
        }
    }
}
