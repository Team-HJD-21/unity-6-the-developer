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
            PlayerId = playerId;
            Revision = revision;
            Currencies = new ReadOnlyCollection<CurrencyBalance>(new List<CurrencyBalance>(currencies ?? new CurrencyBalance[0]));
            UnlockedDefinitions = new ReadOnlyCollection<DefinitionId>(new List<DefinitionId>(unlockedDefinitions ?? new DefinitionId[0]));
        }
    }
}
