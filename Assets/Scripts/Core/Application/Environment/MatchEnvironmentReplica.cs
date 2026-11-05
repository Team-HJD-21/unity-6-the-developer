// 서버가 확정한 환경만 읽기 모델에 적용합니다. Client는 환경을 재계산하지 않습니다.
// Match/준비 세대·revision·시계 역행과 같은 revision의 내용 충돌을 거부합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class MatchEnvironmentReplica : IMatchEnvironmentSource, IDisposable
    {
        private readonly MatchId _match;
        private readonly Guid _descriptor;
        private bool _disposed;
        public MatchEnvironmentSnapshot Snapshot { get; private set; }
        public event Action<MatchEnvironmentSnapshot> Changed;
        public MatchEnvironmentReplica(MatchId match, Guid descriptor)
        {
            if (match.IsEmpty || descriptor == Guid.Empty) throw new ArgumentException("A Match and preparation are required.");
            _match = match; _descriptor = descriptor;
        }
        public EnvironmentApplyStatus Apply(EnvironmentReplicationFrame frame, bool verifiedServer)
        {
            if (_disposed) return EnvironmentApplyStatus.Disposed;
            if (!verifiedServer) return EnvironmentApplyStatus.Unauthorized;
            if (frame == null || frame.DescriptorId != _descriptor) return EnvironmentApplyStatus.DifferentDescriptor;
            if (frame.Snapshot.MatchId != _match) return EnvironmentApplyStatus.DifferentMatch;
            var next = frame.Snapshot;
            if (Snapshot != null)
            {
                if (next.Revision < Snapshot.Revision) return EnvironmentApplyStatus.Stale;
                if (next.Revision == Snapshot.Revision)
                    return next.HasSameValues(Snapshot) && next.Epoch == Snapshot.Epoch && next.GameSeconds == Snapshot.GameSeconds ? EnvironmentApplyStatus.Unchanged : EnvironmentApplyStatus.ConflictingRevision;
                if (next.Epoch == Snapshot.Epoch && next.GameSeconds < Snapshot.GameSeconds) return EnvironmentApplyStatus.ClockReversed;
            }
            Snapshot = next;
            var errors = new List<Exception>();
            if (Changed != null) foreach (Action<MatchEnvironmentSnapshot> observer in Changed.GetInvocationList())
                try { observer(next); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException("Environment replica committed; observers failed.", errors);
            return EnvironmentApplyStatus.Applied;
        }
        public void Dispose() { if (_disposed) return; _disposed = true; Snapshot = null; Changed = null; }
    }
}
