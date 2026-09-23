// Detached, immutable match state at one sequence for persistence or recovery boundaries.


using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchSnapshot
    {
        public MatchId MatchId { get; }
        public long Sequence { get; }
        public MatchPhase Phase { get; }
        public IReadOnlyList<PlayerState> Players { get; }
        public ControlUnitState ControlUnit { get; }
        public WaveState Wave { get; }
        public IReadOnlyList<TurretState> Turrets { get; }
        public IReadOnlyList<EnemyState> Enemies { get; }
        public IReadOnlyList<SectorState> Sectors { get; }
        public MatchResult Result { get; }

        internal MatchSnapshot(
            MatchId matchId,
            long sequence,
            MatchPhase phase,
            IEnumerable<PlayerState> players,
            ControlUnitState controlUnit,
            WaveState wave,
            IEnumerable<TurretState> turrets,
            IEnumerable<EnemyState> enemies,
            IEnumerable<SectorState> sectors,
            MatchResult result)
        {
            MatchId = matchId;
            Sequence = sequence;
            Phase = phase;
            Players = Copy(players);
            ControlUnit = controlUnit;
            Wave = wave;
            Turrets = Copy(turrets);
            Enemies = Copy(enemies);
            Sectors = Copy(sectors);
            Result = result;
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) =>
            new ReadOnlyCollection<T>(new List<T>(values ?? new T[0]));
    }
}
