// Immutable, resolved input captured when a match is created.


using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchConfig
    {
        public MatchId MatchId { get; }
        public MatchMode Mode { get; }
        public StageId StageId { get; }
        public DefinitionId DifficultyProfileId { get; }
        public string ContentVersion { get; }
        public int RandomSeed { get; }
        public IReadOnlyList<PlayerLoadoutSnapshot> Players { get; }

        public MatchConfig(
            MatchId matchId,
            MatchMode mode,
            StageId stageId,
            DefinitionId difficultyProfileId,
            string contentVersion,
            int randomSeed,
            IEnumerable<PlayerLoadoutSnapshot> players)
        {
            MatchId = matchId;
            Mode = mode;
            StageId = stageId;
            DifficultyProfileId = difficultyProfileId;
            ContentVersion = contentVersion ?? string.Empty;
            RandomSeed = randomSeed;
            Players = new ReadOnlyCollection<PlayerLoadoutSnapshot>(new List<PlayerLoadoutSnapshot>(players ?? new PlayerLoadoutSnapshot[0]));
        }
    }
}
