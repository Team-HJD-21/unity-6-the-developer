// 원본 timestamp와 게임 시각의 anchor로 Match 시간을 계산합니다.
// 정지·배속·복원을 권위 소유자가 제어하며 프레임 delta를 누적하지 않습니다.
// 원본 준비 해제 중 시간은 진행하지 않고 시각 역행은 명시적 복원/재조립을 요구합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchClock : IMatchTime, IDisposable
    {
        private readonly ITimestampSource _source;
        private readonly MatchId _matchId;
        private Guid _epoch = Guid.NewGuid();
        private long _revision;
        private double _elapsed, _gameAnchor, _sourceAnchor, _lastSource, _rate = 1;
        private bool _paused, _available, _disposed, _requiresReset;
        private MatchTimeSnapshot _snapshot;
        public double ElapsedSeconds { get { ThrowIfDisposed(); return _elapsed; } }
        public bool IsAvailable => !_disposed && _available;
        public bool RequiresReset => _requiresReset;
        public MatchTimeSnapshot Snapshot { get { ThrowIfDisposed(); return _snapshot; } }

        public MatchClock(MatchId matchId, ITimestampSource source, double initialGameSeconds = 0)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match is required.", nameof(matchId));
            RequireValue(initialGameSeconds, nameof(initialGameSeconds));
            _matchId = matchId; _source = source ?? throw new ArgumentNullException(nameof(source));
            _elapsed = _gameAnchor = initialGameSeconds;
            PublishAnchor();
            Pump();
        }

        /// <summary>Match 소유자가 주 스레드에서 원본 시각을 샘플링합니다. 정지 중에도 원본 준비 상태를 확인합니다.</summary>
        /// <returns>유효한 시각이면 true입니다. 준비 해제·역행 시에는 경과 시간을 보존하고 false를 반환합니다.</returns>
        public bool Pump()
        {
            ThrowIfDisposed();
            if (_requiresReset) return false;
            if (!_source.TryReadSeconds(out double now)) { SetUnavailable(); return false; }
            if (!IsValue(now) || (_available && now < _lastSource))
            { _requiresReset = true; SetUnavailable(); return false; }
            if (!_available)
            {
                // 재접속/준비 해제 구간의 실제 시간을 게임 진행으로 따라잡지 않습니다.
                _available = true; _sourceAnchor = _lastSource = now; _gameAnchor = _elapsed;
                PublishAnchor(); return true;
            }
            double next = _paused ? _gameAnchor : _gameAnchor + (now - _sourceAnchor) * _rate;
            if (!IsValue(next)) { _requiresReset = true; SetUnavailable(); return false; }
            _lastSource = now; _elapsed = next;
            return true;
        }

        /// <summary>권위 시간 조율부가 필드 정지 조건을 반영합니다. 개인 메뉴는 이 메서드를 호출하지 않습니다.</summary>
        public void SetPaused(bool paused)
        {
            ThrowIfDisposed(); Pump();
            if (_paused == paused) return;
            _paused = paused; Reanchor();
        }

        /// <summary>권위 시간 조율부/개발 도구가 배속을 바꿉니다. 변경 직전까지의 진행을 먼저 확정합니다.</summary>
        public void SetRate(double rate)
        {
            ThrowIfDisposed(); RequireValue(rate, nameof(rate)); Pump();
            if (_rate == rate) return;
            _rate = rate; Reanchor();
        }

        /// <summary>저장 복원 조율부가 게임 초를 복원하고 과거 예약과 다른 새 실행 epoch를 만듭니다.</summary>
        /// <remarks>현재 원본 시각이 준비되어 있어야 합니다. 과거 SDK timestamp는 입력하지 않습니다.</remarks>
        public void Restore(double gameSeconds)
        {
            ThrowIfDisposed(); RequireValue(gameSeconds, nameof(gameSeconds));
            if (!_source.TryReadSeconds(out double now) || !IsValue(now))
                throw new InvalidOperationException("The timestamp source is not ready for restore.");
            _elapsed = _gameAnchor = gameSeconds; _sourceAnchor = _lastSource = now;
            _epoch = Guid.NewGuid(); _available = true; _requiresReset = false;
            PublishAnchor();
        }

        private void Reanchor()
        { _gameAnchor = _elapsed; _sourceAnchor = _lastSource; PublishAnchor(); }
        private void SetUnavailable()
        {
            if (!_available) return;
            _available = false; Reanchor();
        }
        private void PublishAnchor()
        {
            _snapshot = new MatchTimeSnapshot(_matchId, _epoch, checked(++_revision),
                _gameAnchor, _sourceAnchor, _rate, _paused, _available);
        }
        public void Dispose() { _disposed = true; _available = false; }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(MatchClock)); }
        private static bool IsValue(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0;
        private static void RequireValue(double value, string parameter)
        { if (!IsValue(value)) throw new ArgumentOutOfRangeException(parameter); }
    }
}
