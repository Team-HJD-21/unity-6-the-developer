// Immutable per-player loadout input resolved before a match starts.


using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class PlayerLoadoutSnapshot
    {
        public PlayerId PlayerId { get; }
        public IReadOnlyDictionary<DefinitionId, int> UpgradeLevels { get; }

        public PlayerLoadoutSnapshot(PlayerId playerId, IDictionary<DefinitionId, int> upgradeLevels)
        {
            PlayerId = playerId;
            UpgradeLevels = new ReadOnlyDictionary<DefinitionId, int>(
                new Dictionary<DefinitionId, int>(upgradeLevels ?? new Dictionary<DefinitionId, int>()));
        }
    }
}
