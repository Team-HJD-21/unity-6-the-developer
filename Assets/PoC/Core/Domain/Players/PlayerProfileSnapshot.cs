// 플레이어 장비 구성을 결정할 때 사용하는 로컬 또는 Backend 프로필 불변 데이터입니다.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class PlayerProfileSnapshot
    {
        public PlayerId PlayerId { get; }
        public long Revision { get; }
        public IReadOnlyList<CurrencyBalance> Currencies { get; }
        public IReadOnlyList<DefinitionId> UnlockedDefinitions { get; }

        public PlayerProfileSnapshot(
            PlayerId playerId,
            long revision,
            IEnumerable<CurrencyBalance> currencies,
            IEnumerable<DefinitionId> unlockedDefinitions)
        {
            if (playerId.IsEmpty) throw new System.ArgumentException("Profile requires a valid player ID.", nameof(playerId));
            if (revision < 0) throw new System.ArgumentOutOfRangeException(nameof(revision));
            PlayerId = playerId;
            Revision = revision;
            Currencies = new ReadOnlyCollection<CurrencyBalance>(CollectionCopy.CopyNonNull(currencies, nameof(currencies)));
            UnlockedDefinitions = new ReadOnlyCollection<DefinitionId>(new List<DefinitionId>(unlockedDefinitions ?? new DefinitionId[0]));
        }
    }
}
