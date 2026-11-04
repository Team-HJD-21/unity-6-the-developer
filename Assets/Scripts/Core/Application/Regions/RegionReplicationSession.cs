// Match 준비 세대에 지역 복제 수명을 묶고 Host의 확정 결과만 전송합니다.
// Client는 읽기 모델만 소유하고, 미접수 전송은 최신 전체 상태로 합쳐 다시 시도합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class RegionReplicationSession : IDisposable
    {
        private readonly INetworkSession _network;
        private readonly NetworkMatchCoordinator _matches;
        private readonly IRegionSnapshotTransport _transport;
        private readonly Dictionary<ulong, RegionReplicationFrame> _pending = new Dictionary<ulong, RegionReplicationFrame>();
        private NetworkMatchDescriptor _descriptor;
        private IRegionGridSource _hostSource;
        private RegionGridReplica _replica;
        private bool _disposed;
        public RegionReplicationFrame LastPublishedFrame { get; private set; }
        public RegionReplicationStatus? LastReceiveStatus { get; private set; }
        public IRegionGridSource Source => _disposed ? null : _hostSource ??
            (_replica != null && _replica.Snapshot != null ? _replica : null);
        public event Action<IRegionGridSource> SourceChanged;
        public RegionReplicationSession(INetworkSession network, NetworkMatchCoordinator matches, IRegionSnapshotTransport transport)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _matches = matches ?? throw new ArgumentNullException(nameof(matches));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            matches.Preparing += OnPreparing; matches.Ended += OnEnded; transport.FrameReceived += OnFrame;
            if (matches.Descriptor != null) OnPreparing(matches.Descriptor);
        }
        public void BindHostSource(IRegionGridSource source)
        {
            ThrowIfDisposed();
            if (!_network.IsServer || _matches.Phase != NetworkMatchPhase.Running || _descriptor == null ||
                _matches.Descriptor != _descriptor) throw new InvalidOperationException("An authoritative running Match is required.");
            if (_hostSource != null) throw new InvalidOperationException("A region source is already bound.");
            if (source == null) throw new ArgumentNullException(nameof(source));
            var snapshot = source.Snapshot;
            if (snapshot == null || snapshot.MatchId != _descriptor.Config.MatchId ||
                snapshot.Mode != _descriptor.RegionMode || !snapshot.Definition.HasSameDefinition(_descriptor.RegionGrid))
                throw new ArgumentException("The initial region source must match the Host descriptor.", nameof(source));
            _hostSource = source;
            _hostSource.Changed += Publish;
            Publish(snapshot);
            NotifySource(source);
        }
        private void OnPreparing(NetworkMatchDescriptor descriptor)
        {
            ClearBinding();
            _descriptor = descriptor;
            if (!_network.IsServer) _replica = new RegionGridReplica(descriptor.Config.MatchId, descriptor.DescriptorId);
        }
        private void OnEnded(MatchId matchId) => ClearBinding();
        private void Publish(RegionGridSnapshot snapshot)
        {
            if (_disposed || !_network.IsServer || _descriptor == null || snapshot.MatchId != _descriptor.Config.MatchId) return;
            long generation = LastPublishedFrame == null ? 1 : LastPublishedFrame.GridGeneration;
            if (LastPublishedFrame != null)
            {
                var previous = LastPublishedFrame.Snapshot;
                if (previous.GridId != snapshot.GridId) generation = checked(generation + 1);
                else if (snapshot.Revision == previous.Revision) return;
                else if (snapshot.Revision < previous.Revision) throw new InvalidOperationException("Host region revision moved backwards.");
            }
            LastPublishedFrame = new RegionReplicationFrame(_descriptor.DescriptorId, generation, snapshot);
            foreach (var participant in _descriptor.Participants)
                if (participant.ConnectionId != _network.LocalConnectionId) _pending[participant.ConnectionId] = LastPublishedFrame;
        }
        /// <summary>App의 전송 Pump 전에 호출합니다. 큐가 가득 차도 새 전체 상태를 보존합니다.</summary>
        public void FlushPending()
        {
            ThrowIfDisposed();
            if (!_network.IsServer || _descriptor == null) return;
            var connections = new List<ulong>(_pending.Keys);
            foreach (ulong connection in connections)
                if (_pending.TryGetValue(connection, out RegionReplicationFrame frame) && _transport.TrySendFrame(connection, frame))
                    _pending.Remove(connection);
        }
        private void OnFrame(ulong sender, RegionReplicationFrame frame)
        {
            if (_disposed || _network.IsServer || _replica == null || _descriptor == null ||
                (_matches.Phase != NetworkMatchPhase.Preparing && _matches.Phase != NetworkMatchPhase.Running)) return;
            if (frame == null) return;
            if (sender != _matches.AuthorityConnectionId) { LastReceiveStatus = RegionReplicationStatus.Unauthorized; return; }
            if (frame.MatchDescriptorId == _descriptor.DescriptorId && frame.GridGeneration == 1 &&
                (frame.Snapshot.Mode != _descriptor.RegionMode || !frame.Snapshot.Definition.HasSameDefinition(_descriptor.RegionGrid)))
            { LastReceiveStatus = RegionReplicationStatus.ConflictingGrid; return; }
            bool first = _replica.Snapshot == null;
            LastReceiveStatus = _replica.Apply(frame, true);
            if (first && LastReceiveStatus == RegionReplicationStatus.Applied) NotifySource(_replica);
        }
        private void ClearBinding()
        {
            if (_hostSource != null) _hostSource.Changed -= Publish;
            _hostSource = null;
            var previous = _replica; _replica = null;
            _descriptor = null; LastPublishedFrame = null; LastReceiveStatus = null; _pending.Clear();
            try { NotifySource(null); }
            finally { previous?.Dispose(); }
        }
        private void NotifySource(IRegionGridSource source)
        {
            if (SourceChanged == null) return;
            List<Exception> errors = null;
            foreach (Action<IRegionGridSource> observer in SourceChanged.GetInvocationList())
                try { observer(source); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            if (errors != null) throw new AggregateException("Region binding changed; some Feature observers failed.", errors);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _matches.Preparing -= OnPreparing; _matches.Ended -= OnEnded; _transport.FrameReceived -= OnFrame;
            try { ClearBinding(); } finally { SourceChanged = null; }
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(RegionReplicationSession)); }
    }
}
