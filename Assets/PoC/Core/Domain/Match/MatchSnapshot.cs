// 저장·복구 경계에서 사용할 특정 순번의 분리된 불변 Match 상태입니다.


using System;
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
            if (matchId.IsEmpty) throw new ArgumentException("Snapshot requires a valid match ID.", nameof(matchId));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!Enum.IsDefined(typeof(MatchPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            MatchId = matchId;
            Sequence = sequence;
            Phase = phase;
            Players = Copy(players);
            ControlUnit = controlUnit;
            Wave = wave;
            if (result != null && result.MatchId != matchId)
                throw new ArgumentException("Snapshot result must belong to the snapshot match.", nameof(result));
            Turrets = Copy(turrets);
            Enemies = Copy(enemies);
            Sectors = Copy(sectors);
            Result = result;
        }

        private static IReadOnlyList<T> Copy<T>(IEnumerable<T> values) where T : class =>
            new ReadOnlyCollection<T>(CollectionCopy.CopyNonNull(values, nameof(values)));
    }
}
