// Match의 단계·확정 결과와 초기 Domain 자료를 보관합니다.
// 실제 Player/CU HP·터렛 전력·Enemy 원본은 각 Feature가 소유하며 여기서 수정하지 않습니다.


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

        /// <summary>MatchSession이 준비를 마친 뒤 호출합니다. 같은 상태를 두 번 시작하지 않습니다.</summary>
        public void Start()
        {
            if (Phase != MatchPhase.Preparing) throw new InvalidOperationException("Only a preparing Match can start.");
            Phase = MatchPhase.Running;
        }

        /// <summary>MatchSession이 모드 규칙으로 확정한 같은 Match의 결과를 적용합니다.</summary>
        public void Complete(MatchResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.MatchId != MatchId) throw new ArgumentException("The result belongs to another Match.", nameof(result));
            if (Phase != MatchPhase.Running) throw new InvalidOperationException("Only a running Match can complete.");
            Result = result;
            Phase = MatchPhase.Completed;
        }

        /// <summary>Session 정리 끝에 단계만 닫습니다. Unity actor/Feature의 폐기는 해당 소유자가 담당합니다.</summary>
        public void MarkDisposed() => Phase = MatchPhase.Disposed;

        /// <summary>초기 Domain 자료와 Match 단계를 복사합니다. 실제 Feature HP/전력·공간은 각각의 읽기 계약을 사용합니다.</summary>
        public MatchSnapshot CreateSnapshot(long sequence) => new MatchSnapshot(
            MatchId, sequence, Phase, _players, ControlUnit, Wave, _turrets, _enemies, _sectors, Result);

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
