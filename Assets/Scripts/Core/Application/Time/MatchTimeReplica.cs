// Client가 Host의 시간 anchor를 읽고 같은 서버 timestamp에서 표시 시간을 계산합니다.
// 정지·배속·epoch를 만들거나 권위 예약을 실행하지 않으며 잘못된 세대/과거 상태를 거부합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchTimeReplica : IMatchTime, IDisposable
    {
        private readonly MatchId _match;
        private readonly Guid _descriptor;
        private readonly ITimestampSource _source;
        private MatchTimeSnapshot _snapshot;
        private double _elapsed;
        private bool _available, _disposed;
        public bool HasSnapshot => !_disposed && _snapshot != null;
        public bool IsAvailable => !_disposed && _available;
        public double ElapsedSeconds { get { RequireAlive(); return _elapsed; } }
        /// <summary>처음 수신하기 전에는 사용할 수 없습니다. 조립부는 HasSnapshot 이후에만 읽기 시계를 공개합니다.</summary>
        public MatchTimeSnapshot Snapshot { get { RequireAlive(); return _snapshot ?? throw new InvalidOperationException("No authority time anchor has arrived."); } }
        public MatchTimeReplica(MatchId match, Guid descriptor, ITimestampSource source)
        {
            if (match.IsEmpty) throw new ArgumentException("A Match is required.", nameof(match));
            if (descriptor == Guid.Empty) throw new ArgumentException("A preparation generation is required.", nameof(descriptor));
            _match = match; _descriptor = descriptor; _source = source ?? throw new ArgumentNullException(nameof(source));
        }
        /// <summary>서버 소유 전송 어댑터가 현재 준비 세대의 새 anchor를 전달합니다. 과거/다른 세대는 변경 없이 거부합니다.</summary>
        /// <remarks>새 권위 anchor는 정지·복원 보정을 위해 표시 시각을 되돌릴 수 있습니다. 같은 anchor의 timestamp 역행은 동결합니다.</remarks>
        public bool TryApply(Guid descriptor, MatchTimeSnapshot snapshot, bool fromAuthority)
        {
            RequireAlive();
            if (!fromAuthority || descriptor != _descriptor || snapshot == null || snapshot.MatchId != _match ||
                (_snapshot != null && snapshot.Revision <= _snapshot.Revision)) return false;
            _snapshot = snapshot; _elapsed = snapshot.GameSecondsAtAnchor; _available = false;
            Pump(); return true;
        }
        /// <summary>Scene의 Client 어댑터가 프레임마다 읽기 시각을 갱신합니다. anchor가 사라지면 가용성을 닫고 마지막 값을 보존합니다.</summary>
        public void Pump(bool sourcePresent = true)
        {
            RequireAlive();
            if (!sourcePresent || _snapshot == null || !_snapshot.IsAvailable || !_source.TryReadSeconds(out double now) ||
                double.IsNaN(now) || double.IsInfinity(now) || now < 0)
            { _available = false; return; }
            double projected;
            try { projected = _snapshot.EstimateElapsedSeconds(now); }
            catch (ArgumentOutOfRangeException) { _available = false; return; }
            _elapsed = Math.Max(_elapsed, projected); _available = true;
        }
        public void Dispose() { _disposed = true; _available = false; _snapshot = null; }
        private void RequireAlive() { if (_disposed) throw new ObjectDisposedException(nameof(MatchTimeReplica)); }
    }
}
