// Match 수명에 맞춰 Host 표시 source와 실제 전송/Client replica를 연결합니다.
// 미접수 최신 Frame은 보존하고 이전 세대·잘못된 역할의 수신을 적용하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class TurretPresentationReplication : INetworkMessagePump, IDisposable
    {
        private readonly INetworkSession _network;
        private readonly NetworkMatchCoordinator _matches;
        private readonly ITurretPresentationTransport _transport;
        private readonly ITurretPresentationSource _hostSource;
        private readonly TurretPresentationReplica _replica;
        private readonly Guid _descriptorId;
        private readonly MatchId _matchId;
        private readonly bool _isAuthority;
        private readonly Dictionary<ulong, TurretPresentationFrame> _pending = new Dictionary<ulong, TurretPresentationFrame>();
        private bool _disposed;
        public long RejectedFrames { get; private set; }
        private bool IsCurrent => !_disposed && _network.IsServer == _isAuthority &&
            _network.State == (_isAuthority ? NetworkConnectionState.Hosting : NetworkConnectionState.ConnectedClient) &&
            _matches.Phase == NetworkMatchPhase.Running && _matches.Descriptor?.DescriptorId == _descriptorId;
        public TurretPresentationReplication(INetworkSession network, NetworkMatchCoordinator matches, ITurretPresentationTransport transport,
            ITurretPresentationSource hostSource, TurretPresentationReplica replica)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _matches = matches ?? throw new ArgumentNullException(nameof(matches));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            if (matches.Phase != NetworkMatchPhase.Running || matches.Descriptor == null) throw new InvalidOperationException("Bind the running preparation.");
            if (network.IsServer ? hostSource == null || replica != null : hostSource != null || replica == null)
                throw new ArgumentException("Bind an authority source or a Client replica, not both.");
            _descriptorId = matches.Descriptor.DescriptorId; _matchId = matches.Descriptor.Config.MatchId;
            _isAuthority = network.IsServer;
            _hostSource = hostSource; _replica = replica; _transport.FrameReceived += OnFrame;
            if (_hostSource != null) { _hostSource.Changed += OnSource; OnSource(_hostSource.Snapshot); }
        }
        private void OnSource(TurretPresentationFrame frame)
        {
            if (!IsCurrent || !_network.IsServer || frame == null || frame.DescriptorId != _descriptorId || frame.MatchId != _matchId) return;
            foreach (var participant in _matches.Descriptor.Participants)
                if (participant.ConnectionId != _network.LocalConnectionId) _pending[participant.ConnectionId] = frame;
        }
        private void OnFrame(ulong sender, TurretPresentationFrame frame)
        {
            if (!IsCurrent || _network.IsServer || frame == null ||
                !_matches.Descriptor.Participants.Any(item => item.IsHost && item.ConnectionId == sender) || !_replica.TryApply(frame))
                RejectedFrames++;
        }
        public void Pump()
        {
            if (_disposed) return;
            if (!IsCurrent || !_network.IsServer) { _pending.Clear(); return; }
            foreach (var pair in _pending.ToArray())
            {
                if (!_transport.TrySendFrame(pair.Key, pair.Value)) continue;
                if (_disposed) return;
                if (_pending.TryGetValue(pair.Key, out var current) && ReferenceEquals(current, pair.Value)) _pending.Remove(pair.Key);
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _transport.FrameReceived -= OnFrame;
            if (_hostSource != null) _hostSource.Changed -= OnSource;
            _pending.Clear();
        }
    }
}
