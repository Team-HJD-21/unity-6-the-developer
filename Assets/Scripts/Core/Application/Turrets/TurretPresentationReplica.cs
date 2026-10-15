// 현재 준비 세대의 Host 확정 HUD 투영만 보관하는 Client 읽기 모델입니다.
// revision/확정 명령 순번의 역행과 같은 revision의 다른 내용을 거부합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class TurretPresentationReplica : ITurretPresentationSource, IDisposable
    {
        private readonly MatchId _matchId;
        private readonly Guid _descriptorId;
        private bool _disposed;
        public TurretPresentationFrame Snapshot { get; private set; }
        public event Action<TurretPresentationFrame> Changed;
        public TurretPresentationReplica(MatchId matchId, Guid descriptorId)
        {
            if (matchId.IsEmpty || descriptorId == Guid.Empty) throw new ArgumentException("Provide the current preparation.");
            _matchId = matchId; _descriptorId = descriptorId;
        }
        public bool TryApply(TurretPresentationFrame frame)
        {
            if (_disposed || frame == null || frame.MatchId != _matchId || frame.DescriptorId != _descriptorId) return false;
            if (Snapshot != null)
            {
                if (frame.Revision < Snapshot.Revision || frame.ConfirmedCommandSequence < Snapshot.ConfirmedCommandSequence) return false;
                if (frame.Revision == Snapshot.Revision) return Snapshot.HasSameContent(frame);
            }
            Snapshot = frame; Changed?.Invoke(frame); return true;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; Snapshot = null;
            try { Changed?.Invoke(null); } finally { Changed = null; }
        }
    }
}
