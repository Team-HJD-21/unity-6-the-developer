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
            if (matchId.IsEmpty) throw new ArgumentException("Match state requires a valid match ID.", nameof(matchId));
            if (!Enum.IsDefined(typeof(MatchPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            MatchId = matchId;
            Phase = phase;
            _players = CollectionCopy.CopyNonNull(players, nameof(players));
            _turrets = CollectionCopy.CopyNonNull(turrets, nameof(turrets));
            _enemies = CollectionCopy.CopyNonNull(enemies, nameof(enemies));
            _sectors = CollectionCopy.CopyNonNull(sectors, nameof(sectors));
            EnsureUnique(_players, player => player.PlayerId, id => id.IsEmpty, "player");
            EnsureUnique(_turrets, turret => turret.EntityId, id => id.IsEmpty, "turret");
            EnsureUnique(_enemies, enemy => enemy.EntityId, id => id.IsEmpty, "enemy");
            EnsureUnique(_sectors, sector => sector.SectorDefinitionId, id => id.IsEmpty, "sector");
            Players = new ReadOnlyCollection<PlayerState>(_players);
            Turrets = new ReadOnlyCollection<TurretState>(_turrets);
            Enemies = new ReadOnlyCollection<EnemyState>(_enemies);
            Sectors = new ReadOnlyCollection<SectorState>(_sectors);
            ControlUnit = controlUnit ?? throw new ArgumentNullException(nameof(controlUnit));
            Wave = wave ?? throw new ArgumentNullException(nameof(wave));
            if (result != null && result.MatchId != matchId)
                throw new ArgumentException("Initial result must belong to this match state.", nameof(result));
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

        private static void EnsureUnique<TValue, TId>(
            IEnumerable<TValue> values,
            Func<TValue, TId> getId,
            Func<TId, bool> isEmpty,
            string category)
            where TValue : class
        {
            var ids = new HashSet<TId>();
            foreach (var value in values)
            {
                var id = getId(value);
                if (isEmpty(id)) throw new ArgumentException($"A {category} state has no ID.", nameof(values));
                if (!ids.Add(id)) throw new ArgumentException($"Duplicate {category} ID '{id}'.", nameof(values));
            }
        }
    }
}
