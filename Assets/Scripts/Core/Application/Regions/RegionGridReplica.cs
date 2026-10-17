// Client가 Host의 확정 Grid만 표시·조회하는 읽기 모델입니다.
// 삼각형을 다시 계산하거나 D 터렛 상태를 변경하지 않고 과거 세대/위조 갱신을 거부합니다.
using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class RegionGridReplica : IRegionGridSource, IDisposable
    {
        private readonly MatchId _matchId;
        private readonly Guid _descriptorId;
        private RegionGridSnapshot _snapshot;
        private bool _disposed;
        private bool _notifying;
        public long GridGeneration { get; private set; }
        public RegionGridSnapshot Snapshot { get { ThrowIfDisposed(); return _snapshot; } }
        public event Action<RegionGridSnapshot> Changed;
        public RegionGridReplica(MatchId matchId, Guid descriptorId)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A match id is required.", nameof(matchId));
            if (descriptorId == Guid.Empty) throw new ArgumentException("A descriptor id is required.", nameof(descriptorId));
            _matchId = matchId; _descriptorId = descriptorId;
        }
        public RegionReplicationStatus Apply(RegionReplicationFrame frame, bool senderIsVerifiedServer)
        {
            ThrowIfDisposed();
            if (_notifying) throw new InvalidOperationException("Do not apply state from a change observer.");
            if (frame == null) throw new ArgumentNullException(nameof(frame));
            if (!senderIsVerifiedServer) return RegionReplicationStatus.Unauthorized;
            if (frame.MatchDescriptorId != _descriptorId) return RegionReplicationStatus.DifferentDescriptor;
            RegionGridSnapshot next = frame.Snapshot;
            if (next.MatchId != _matchId) return RegionReplicationStatus.DifferentMatch;
            if (_snapshot != null)
            {
                if (frame.GridGeneration < GridGeneration) return RegionReplicationStatus.StaleGrid;
                if (frame.GridGeneration == GridGeneration)
                {
                    if (next.GridId != _snapshot.GridId || next.Mode != _snapshot.Mode ||
                        !SameDefinition(next.Definition, _snapshot.Definition)) return RegionReplicationStatus.ConflictingGrid;
                    if (next.Revision < _snapshot.Revision) return RegionReplicationStatus.StaleRevision;
                    if (next.Revision == _snapshot.Revision)
                    {
                        for (int i = 0; i < next.CapturedCells.Count; i++)
                            if (next.CapturedCells[i] != _snapshot.CapturedCells[i]) return RegionReplicationStatus.ConflictingRevision;
                        return RegionReplicationStatus.Unchanged;
                    }
                }
                else if (next.GridId == _snapshot.GridId) return RegionReplicationStatus.ConflictingGrid;
            }
            _snapshot = next; GridGeneration = frame.GridGeneration;
            _notifying = true;
            System.Collections.Generic.List<Exception> errors = null;
            try
            {
                if (Changed != null)
                    foreach (Action<RegionGridSnapshot> observer in Changed.GetInvocationList())
                        try { observer(next); }
                        catch (Exception error) { (errors ??= new System.Collections.Generic.List<Exception>()).Add(error); }
            }
            finally { _notifying = false; }
            if (errors != null) throw new AggregateException("Replica state committed; observation failed.", errors);
            return RegionReplicationStatus.Applied;
        }
        private static bool SameDefinition(RegionGridDefinition a, RegionGridDefinition b)
        {
            if (ReferenceEquals(a, b)) return true;
            var x = a.Geometry; var y = b.Geometry;
            if (x.Origin != y.Origin || x.OriginZ != y.OriginZ || x.MapWidth != y.MapWidth ||
                x.MapHeight != y.MapHeight || x.CellsX != y.CellsX || x.CellsY != y.CellsY) return false;
            for (int i = 0; i < a.RegionIds.Count; i++) if (a.RegionIds[i] != b.RegionIds[i]) return false;
            return true;
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _snapshot = null; Changed = null; }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(RegionGridReplica)); }
    }
}
