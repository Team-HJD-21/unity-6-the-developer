// MatchCoordinator가 소유하는 Host 구조/전멸 관찰자입니다. HP 원본과 Scene 전환은 소유하지 않습니다.
// 실제 위치·생존·입력 자격을 재검증하고 공용 게임 시간으로 구조 유지와 팀 대기를 잽니다.
// 실패는 한 번만 알리며 실제 복귀와 기술 실패 재시도는 Scene/App 조율부가 수행합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class MatchRecovery : IPlayerRescueService, IDisposable
    {
        private readonly MissionRecoveryLedger _ledger;
        private readonly IMatchClock _clock;
        private readonly Func<bool> _available;
        private readonly HashSet<PlayerId> _roster;
        private readonly Dictionary<PlayerId, IPlayerRescueActor> _actors = new();
        private readonly Dictionary<PlayerId, Hold> _holds = new();
        private readonly double _holdSeconds, _healthRatio, _distanceSquared, _inputExpirySeconds;
        private double _lastTime;
        private bool _waiting, _disposed, _failed;
        public event Action<Guid, bool, string> Failure;
        public event Action<PlayerId, PlayerId> Rescued;
        public bool IsRecoveryPending => _failed;
        public bool IsReady => !_disposed && !_failed && _available() && !_ledger.IsEnded && _actors.Count == _roster.Count;
        public int RegisteredPlayers => _actors.Count;
        public int RequiredPlayers => _roster.Count;
        public MissionRecoveryLedger Ledger => _ledger;

        public MatchRecovery(MissionRecoveryLedger ledger, IMatchClock clock, Func<bool> available,
            IEnumerable<PlayerId> roster, MissionRecoverySettings settings)
        {
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _available = available ?? throw new ArgumentNullException(nameof(available));
            _roster = new HashSet<PlayerId>(roster ?? throw new ArgumentNullException(nameof(roster)));
            if (_roster.Count == 0 || _roster.Any(id => id.IsEmpty)) throw new ArgumentException("A valid participant roster is required.", nameof(roster));
            if (settings == null || !settings.IsValid) throw new ArgumentException("Invalid rescue settings.", nameof(settings));
            _holdSeconds = settings.holdSeconds; _healthRatio = settings.healthRatio;
            _distanceSquared = settings.distance * settings.distance; _inputExpirySeconds = settings.inputExpirySeconds;
            _lastTime = clock.ElapsedSeconds;
        }

        /// <summary>Host의 실제 actor 수명 등록입니다. 접속자가 제출한 좌표/HP 자료를 등록하지 않습니다.</summary>
        public void Register(PlayerId player, IPlayerRescueActor actor)
        {
            if (_disposed || !_roster.Contains(player) || actor == null || !_actors.TryAdd(player, actor))
                throw new InvalidOperationException("Register one actual authority actor per roster Player.");
        }
        public void Unregister(PlayerId player)
        {
            _actors.Remove(player);
            _holds.Remove(player);
        }

        public bool TryFindTarget(PlayerId rescuer, out PlayerId target)
        {
            target = default;
            if (!IsReady || !Read(rescuer, out var source) || !source.IsAlive || !source.CanInteract) return false;
            double nearest = _distanceSquared;
            foreach (var pair in _actors)
            {
                if (pair.Key == rescuer || !Read(pair.Key, out var other) || other.IsAlive) continue;
                double distance = DistanceSquared(source.Position, other.Position);
                if (distance > nearest) continue;
                nearest = distance;
                target = pair.Key;
            }
            return !target.IsEmpty;
        }

        /// <summary>Host gateway가 검증한 참가자의 유지/해제를 전달합니다. 유지 패킷이 끊기면 자동 취소합니다.</summary>
        public bool SetRescueHeld(PlayerId rescuer, PlayerId target, bool held)
        {
            if (!held) { _holds.Remove(rescuer); return true; }
            if (!IsReady || !Eligible(rescuer, target)) { _holds.Remove(rescuer); return false; }
            double now = _clock.ElapsedSeconds;
            if (!_holds.TryGetValue(rescuer, out var hold) || hold.Target != target)
                _holds[rescuer] = hold = new Hold(target, now);
            hold.LastInput = now;
            return true;
        }
        public double HoldProgress(PlayerId rescuer) => _holds.TryGetValue(rescuer, out var hold)
            ? Math.Max(0, Math.Min(1, (_clock.ElapsedSeconds - hold.Started) / _holdSeconds)) : 0;

        /// <summary>소유자가 CU 패배를 먼저 평가한 뒤 호출합니다. 실제 Player source가 모두 준비되기 전에는 판정하지 않습니다.</summary>
        public void Pump()
        {
            if (_disposed) return;
            double now = _clock.ElapsedSeconds;
            double delta = Math.Max(0, now - _lastTime);
            if (now < _lastTime) _holds.Clear();
            _lastTime = now;
            if (!IsReady) { _holds.Clear(); _waiting = false; return; }
            var observed = new List<PlayerInteractionSnapshot>();
            foreach (var id in _roster)
            {
                if (!Read(id, out var state)) { _holds.Clear(); _waiting = false; return; }
                observed.Add(state);
            }
            int down = observed.Count(state => !state.IsAlive);
            if (down == observed.Count) { Fail("AllPlayersDown"); return; }
            if (down > 0 && _waiting) _ledger.SpendRescueTime(delta);
            else if (down == 0 && _waiting) _ledger.FlushChanges();
            _waiting = down > 0;
            if (_waiting && _ledger.RescueSeconds <= 0) { Fail("RescueBudgetExhausted"); return; }
            AdvanceHolds(now);
        }

        private void AdvanceHolds(double now)
        {
            foreach (var pair in _holds.ToArray())
            {
                var hold = pair.Value;
                if (now - hold.LastInput > _inputExpirySeconds || !Eligible(pair.Key, hold.Target))
                {
                    _holds.Remove(pair.Key);
                    continue;
                }
                if (now - hold.Started < _holdSeconds) continue;
                if (!_actors[hold.Target].TryRevive(_healthRatio)) continue;
                _holds.Remove(pair.Key);
                _ledger.FlushChanges();
                Rescued?.Invoke(pair.Key, hold.Target);
            }
        }
        private bool Read(PlayerId id, out PlayerInteractionSnapshot snapshot)
        {
            snapshot = default;
            return _actors.TryGetValue(id, out var actor) && actor.TryCaptureRescue(out snapshot);
        }
        private bool Eligible(PlayerId rescuer, PlayerId target) => rescuer != target &&
            Read(rescuer, out var source) && source.IsAlive && source.CanInteract &&
            Read(target, out var destination) && !destination.IsAlive &&
            DistanceSquared(source.Position, destination.Position) <= _distanceSquared;
        private static double DistanceSquared(BattlefieldPoint a, BattlefieldPoint b)
            => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
        private void Fail(string reason)
        {
            if (_failed) return;
            _failed = true;
            _holds.Clear();
            Guid failure = _ledger.PendingFailure != Guid.Empty ? _ledger.PendingFailure : Guid.NewGuid();
            bool allowed = _ledger.TryBeginFailure(failure);
            Failure?.Invoke(failure, allowed, reason);
        }

        /// <summary>실제 월드 복귀 성공 뒤에만 호출합니다. 기술 실패에는 같은 차감을 유지합니다.</summary>
        public void CompleteRecovery(Guid failure)
        {
            if (_disposed || !_failed || !_available()) throw new InvalidOperationException("No current recovery is pending.");
            _ledger.CompleteRecovery(failure);
            _failed = false; _waiting = false; _lastTime = _clock.ElapsedSeconds;
            _holds.Clear();
        }
        public void Dispose()
        {
            if (_disposed) return;
            if (_waiting) _ledger.FlushChanges();
            _disposed = true;
            _holds.Clear(); _actors.Clear(); Failure = null; Rescued = null;
        }
        private sealed class Hold
        {
            public readonly PlayerId Target;
            public readonly double Started;
            public double LastInput;
            public Hold(PlayerId target, double now) { Target = target; Started = LastInput = now; }
        }
    }
}
