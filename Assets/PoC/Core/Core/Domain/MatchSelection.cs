// Carries user-selected match inputs before content resolution produces MatchConfig.

using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchSelection
    {
        public MatchId MatchId { get; }
        public MatchMode Mode { get; }
        public StageId StageId { get; }
        public DefinitionId DifficultyProfileId { get; }
        public int RandomSeed { get; }
        public IReadOnlyList<PlayerLoadoutSnapshot> Players { get; }

        public MatchSelection(
            MatchId matchId,
            MatchMode mode,
            StageId stageId,
            DefinitionId difficultyProfileId,
            int randomSeed,
            IEnumerable<PlayerLoadoutSnapshot> players)
        {
            MatchId = matchId;
            Mode = mode;
            StageId = stageId;
            DifficultyProfileId = difficultyProfileId;
            RandomSeed = randomSeed;
            Players = new ReadOnlyCollection<PlayerLoadoutSnapshot>(new List<PlayerLoadoutSnapshot>(players ?? new PlayerLoadoutSnapshot[0]));
        }
    }
}
