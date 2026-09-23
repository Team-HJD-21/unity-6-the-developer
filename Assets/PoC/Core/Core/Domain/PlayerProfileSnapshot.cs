// Immutable local or backend profile data used to resolve a player's loadout.

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
