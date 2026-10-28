// 준비 세대에 환경 복제 수명을 묶고 Host의 최신 전체 상태만 전송합니다.
// 초기 수신/변경·backpressure 재시도와 Match 종료 때 구독/replica 정리를 담당합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class EnvironmentReplicationSession : IDisposable
    {
        private readonly INetworkSession _network;
        private readonly NetworkMatchCoordinator _matches;
        private readonly IMatchEnvironmentTransport _transport;
        private readonly Dictionary<ulong, EnvironmentReplicationFrame> _pending = new Dictionary<ulong, EnvironmentReplicationFrame>();
        private NetworkMatchDescriptor _descriptor;
        private IMatchEnvironmentSource _host;
        private MatchEnvironmentReplica _replica;
        private bool _disposed;
        public IMatchEnvironmentSource Source => _disposed ? null : _host ?? (_replica?.Snapshot == null ? null : _replica);
        public EnvironmentApplyStatus LastReceiveStatus { get; private set; }
        public EnvironmentReplicationFrame LastPublishedFrame { get; private set; }
        public event Action<IMatchEnvironmentSource> SourceChanged;
        public EnvironmentReplicationSession(INetworkSession network, NetworkMatchCoordinator matches, IMatchEnvironmentTransport transport)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _matches = matches ?? throw new ArgumentNullException(nameof(matches));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            matches.Preparing += OnPreparing; matches.Ended += OnEnded; transport.FrameReceived += OnFrame;
            if (matches.Descriptor != null) OnPreparing(matches.Descriptor);
        }
        public void BindHostSource(IMatchEnvironmentSource source)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(EnvironmentReplicationSession));
            if (!_network.IsServer || _matches.Phase != NetworkMatchPhase.Running || _descriptor == null ||
                !ReferenceEquals(_matches.Descriptor, _descriptor) || _host != null)
                throw new InvalidOperationException("Bind one source to the current Host Match.");
            if (source?.Snapshot == null || source.Snapshot.MatchId != _descriptor.Config.MatchId)
                throw new ArgumentException("Provide the current Match environment.", nameof(source));
            _host = source; _host.Changed += Publish; Publish(source.Snapshot); NotifySource(source);
        }
        private void OnPreparing(NetworkMatchDescriptor descriptor)
        {
            Clear(); _descriptor = descriptor;
            if (!_network.IsServer) _replica = new MatchEnvironmentReplica(descriptor.Config.MatchId, descriptor.DescriptorId);
        }
        private void OnEnded(MatchId match) => Clear();
        private void Publish(MatchEnvironmentSnapshot snapshot)
        {
            if (_disposed || !_network.IsServer || _descriptor == null || snapshot.MatchId != _descriptor.Config.MatchId) return;
            if (LastPublishedFrame != null && snapshot.Revision <= LastPublishedFrame.Snapshot.Revision) return;
            LastPublishedFrame = new EnvironmentReplicationFrame(_descriptor.DescriptorId, snapshot);
            foreach (var participant in _descriptor.Participants)
                if (participant.ConnectionId != _network.LocalConnectionId) _pending[participant.ConnectionId] = LastPublishedFrame;
        }
        public void FlushPending()
        {
            if (_disposed || !_network.IsServer || _descriptor == null) return;
            foreach (ulong connection in new List<ulong>(_pending.Keys))
                if (_pending.TryGetValue(connection, out var frame) && _transport.TrySendFrame(connection, frame)) _pending.Remove(connection);
        }
        private void OnFrame(ulong sender, EnvironmentReplicationFrame frame)
        {
            if (_disposed || _network.IsServer || _replica == null || _descriptor == null ||
                (_matches.Phase != NetworkMatchPhase.Preparing && _matches.Phase != NetworkMatchPhase.Running)) return;
            bool first = _replica.Snapshot == null;
            LastReceiveStatus = _replica.Apply(frame, sender == _matches.AuthorityConnectionId);
            if (first && LastReceiveStatus == EnvironmentApplyStatus.Applied) NotifySource(_replica);
        }
        private void NotifySource(IMatchEnvironmentSource source)
        {
            var errors = new List<Exception>();
            if (SourceChanged != null) foreach (Action<IMatchEnvironmentSource> observer in SourceChanged.GetInvocationList())
                try { observer(source); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException("Environment binding changed; observers failed.", errors);
        }
        private void Clear()
        {
            if (_host != null) _host.Changed -= Publish;
            _host = null; _descriptor = null; LastPublishedFrame = null; LastReceiveStatus = EnvironmentApplyStatus.None; _pending.Clear();
            var previous = _replica; _replica = null;
            try { NotifySource(null); } finally { previous?.Dispose(); }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _matches.Preparing -= OnPreparing; _matches.Ended -= OnEnded; _transport.FrameReceived -= OnFrame;
            try { Clear(); } finally { SourceChanged = null; }
        }
    }
}
