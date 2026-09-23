// Immutable, resolved input captured when a match is created.


using System;
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
            if (matchId.IsEmpty) throw new ArgumentException("Match config requires a valid match ID.", nameof(matchId));
            if (!Enum.IsDefined(typeof(MatchMode), mode)) throw new ArgumentOutOfRangeException(nameof(mode));
            if (stageId.IsEmpty) throw new ArgumentException("Match config requires a valid stage ID.", nameof(stageId));
            if (difficultyProfileId.IsEmpty) throw new ArgumentException("Match config requires a valid difficulty profile ID.", nameof(difficultyProfileId));
            MatchId = matchId;
            Mode = mode;
            StageId = stageId;
            DifficultyProfileId = difficultyProfileId;
            ContentVersion = contentVersion ?? string.Empty;
            RandomSeed = randomSeed;
            Players = new ReadOnlyCollection<PlayerLoadoutSnapshot>(CollectionCopy.CopyNonNull(players, nameof(players)));
        }
    }
}
