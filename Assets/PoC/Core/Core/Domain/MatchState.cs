// Runtime source of truth for one match, mutated only inside the Domain assembly.


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchState
    {
        private readonly List<PlayerState> _players;
        private readonly List<TurretState> _turrets;
        private readonly List<EnemyState> _enemies;
        private readonly List<SectorState> _sectors;

        public MatchId MatchId { get; }
        public MatchPhase Phase { get; private set; }
        public ControlUnitState ControlUnit { get; private set; }
        public WaveState Wave { get; private set; }
        public MatchResult Result { get; private set; }
        public IReadOnlyList<PlayerState> Players { get; }
        public IReadOnlyList<TurretState> Turrets { get; }
        public IReadOnlyList<EnemyState> Enemies { get; }
        public IReadOnlyList<SectorState> Sectors { get; }

        public MatchState(
            MatchId matchId,
            MatchPhase phase,
            IEnumerable<PlayerState> players,
            ControlUnitState controlUnit,
            WaveState wave,
            IEnumerable<TurretState> turrets,
            IEnumerable<EnemyState> enemies,
            IEnumerable<SectorState> sectors,
            MatchResult result = null)
        {
            MatchId = matchId;
            Phase = phase;
            _players = new List<PlayerState>(players ?? new PlayerState[0]);
            _turrets = new List<TurretState>(turrets ?? new TurretState[0]);
            _enemies = new List<EnemyState>(enemies ?? new EnemyState[0]);
            _sectors = new List<SectorState>(sectors ?? new SectorState[0]);
            Players = new ReadOnlyCollection<PlayerState>(_players);
            Turrets = new ReadOnlyCollection<TurretState>(_turrets);
            Enemies = new ReadOnlyCollection<EnemyState>(_enemies);
            Sectors = new ReadOnlyCollection<SectorState>(_sectors);
            ControlUnit = controlUnit;
            Wave = wave;
            Result = result;
        }

        internal void SetPhase(MatchPhase phase) => Phase = phase;
        internal void SetControlUnit(ControlUnitState state) => ControlUnit = state ?? throw new ArgumentNullException(nameof(state));
        internal void SetWave(WaveState state) => Wave = state ?? throw new ArgumentNullException(nameof(state));
        internal void SetResult(MatchResult result) => Result = result;

        public MatchSnapshot CreateSnapshot(long sequence) => new MatchSnapshot(
            MatchId, sequence, Phase, _players, ControlUnit, Wave, _turrets, _enemies, _sectors, Result);

        internal void ReplacePlayer(PlayerState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var index = _players.FindIndex(player => player.PlayerId == state.PlayerId);
            if (index < 0) throw new InvalidOperationException($"Player '{state.PlayerId}' does not belong to match '{MatchId}'.");
            _players[index] = state;
        }

        internal void ReplaceTurret(TurretState state) => ReplaceById(_turrets, state, turret => turret.EntityId);
        internal void ReplaceEnemy(EnemyState state) => ReplaceById(_enemies, state, enemy => enemy.EntityId);

        private static void ReplaceById<T>(List<T> values, T value, Func<T, EntityId> getId)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            var id = getId(value);
            var index = values.FindIndex(item => getId(item) == id);
            if (index < 0) throw new InvalidOperationException($"Entity '{id}' is not part of this match state.");
            values[index] = value;
        }
    }
}
