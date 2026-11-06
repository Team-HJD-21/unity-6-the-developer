// 한 Match의 Host 환경 projection을 소유합니다. 실제 날씨 결정은 World source가 수행합니다.
// 값/예고가 바뀔 때만 revision을 올리고, 완료/폐기 뒤의 변경을 거부합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class MatchEnvironmentRuntime : IMatchEnvironmentSource, IDisposable
    {
        private readonly Func<bool> _mayWrite;
        private readonly IMatchClock _clock;
        private MatchEnvironmentSnapshot _snapshot;
        private bool _disposed;
        private Guid _epoch;
        public MatchEnvironmentSnapshot Snapshot => _disposed ? null : _snapshot;
        public event Action<MatchEnvironmentSnapshot> Changed;
        public MatchEnvironmentRuntime(MatchId match, IMatchClock clock, Func<bool> mayWrite)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _mayWrite = mayWrite ?? throw new ArgumentNullException(nameof(mayWrite));
            _epoch = (clock as IMatchTime)?.Snapshot.Epoch ?? Guid.Empty;
            _snapshot = new MatchEnvironmentSnapshot(match, 1, clock.ElapsedSeconds, default, default, epoch: _epoch);
        }
        public bool TryUpdate(DefinitionId weather, DefinitionId timeOfDay, IEnumerable<DefinitionId> forecast)
        {
            if (_disposed || !_mayWrite()) return false;
            Guid epoch = (_clock as IMatchTime)?.Snapshot.Epoch ?? Guid.Empty;
            var next = new MatchEnvironmentSnapshot(_snapshot.MatchId, checked(_snapshot.Revision + 1),
                _clock.ElapsedSeconds, weather, timeOfDay, forecast, epoch);
            bool restored = epoch != _epoch;
            if (_snapshot.HasSameValues(next) && !restored) return false;
            if (!restored && next.GameSeconds < _snapshot.GameSeconds) throw new InvalidOperationException("Match clock cannot move backwards inside one execution epoch.");
            _epoch = epoch;
            _snapshot = next;
            var errors = new List<Exception>();
            if (Changed != null) foreach (Action<MatchEnvironmentSnapshot> observer in Changed.GetInvocationList())
                try { observer(next); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException("Environment committed; observers failed.", errors);
            return true;
        }
        public void Dispose() { if (_disposed) return; _disposed = true; Changed = null; }
    }
}
