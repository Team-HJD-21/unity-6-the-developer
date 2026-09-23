// Carries user-selected match inputs before content resolution produces MatchConfig.

using System;
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
            if (matchId.IsEmpty) throw new ArgumentException("Match selection requires a valid match ID.", nameof(matchId));
            if (!Enum.IsDefined(typeof(MatchMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (stageId.IsEmpty) throw new ArgumentException("Match selection requires a valid stage ID.", nameof(stageId));
            if (difficultyProfileId.IsEmpty) throw new ArgumentException("Match selection requires a valid difficulty profile ID.", nameof(difficultyProfileId));
            MatchId = matchId;
            Mode = mode;
            StageId = stageId;
            DifficultyProfileId = difficultyProfileId;
            RandomSeed = randomSeed;
            Players = new ReadOnlyCollection<PlayerLoadoutSnapshot>(CollectionCopy.CopyNonNull(players, nameof(players)));
        }
    }
}
