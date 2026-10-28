// 실제 참가자 집합의 준비·Host 명시 시작·종료를 조율합니다.
// 연결을 자동 개설하거나 Player/터렛을 생성하지 않으며 Feature 조립은 이벤트 소비자가 수행합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class NetworkMatchCoordinator : IDisposable
    {
        private readonly INetworkSession _network;
        private readonly IMatchLifecycleTransport _transport;
        private readonly ulong _serverConnectionId;
        private MatchReadyBarrier _barrier;
        private long _preparationSequence;
        private bool _localReady;
        private bool _disposed;
        public NetworkMatchPhase Phase { get; private set; }
        public NetworkMatchDescriptor Descriptor { get; private set; }
        public ulong AuthorityConnectionId => _serverConnectionId;
        public bool CanStart => !_disposed && _network.IsServer && Phase == NetworkMatchPhase.Preparing && _barrier != null && _barrier.CanStart;
        public event Action<NetworkMatchDescriptor> Preparing;
        public event Action<NetworkMatchDescriptor> Started;
        public event Action<MatchId> Ended;
        public NetworkMatchCoordinator(INetworkSession network, IMatchLifecycleTransport transport, ulong serverConnectionId = 0)
        {
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _serverConnectionId = serverConnectionId;
            _transport.DescriptorReceived += OnDescriptor;
            _transport.SignalReceived += OnSignal;
            _network.PeerDisconnected += OnPeerDisconnected;
            _network.StateChanged += OnConnectionState;
        }
        public bool TryPrepare(MatchConfig config, RegionGridDefinition grid, RegionPolicyMode mode,
            IEnumerable<MatchParticipantBinding> participants)
        {
            ThrowIfDisposed();
            if (!_network.IsServer || Phase == NetworkMatchPhase.Running) return false;
            var descriptor = new NetworkMatchDescriptor(Guid.NewGuid(), checked(_preparationSequence + 1),
                config, mode, grid, participants);
            bool localHost = false;
            foreach (MatchParticipantBinding binding in descriptor.Participants)
                if (binding.IsHost) localHost = binding.ConnectionId == _network.LocalConnectionId && binding.ConnectionId == _serverConnectionId;
            if (!localHost) return false;
            var barrier = new MatchReadyBarrier(config, descriptor.Participants, 1, descriptor.DescriptorId);
            if (Descriptor != null) TryEnd();
            if (_disposed || Descriptor != null) return false;
            _preparationSequence = descriptor.PreparationSequence;
            Descriptor = descriptor; _barrier = barrier; _localReady = false; Phase = NetworkMatchPhase.Preparing;
            foreach (MatchParticipantBinding binding in descriptor.Participants)
                if (binding.ConnectionId != _network.LocalConnectionId && !_transport.TrySendDescriptor(binding.ConnectionId, descriptor))
                { TryEnd(); return false; }
            Notify(Preparing, descriptor);
            return !_disposed && ReferenceEquals(Descriptor, descriptor);
        }
        public bool MarkLocalReady()
        {
            ThrowIfDisposed();
            if (Phase != NetworkMatchPhase.Preparing || Descriptor == null || _localReady) return false;
            if (!TryResolveParticipant(_network.LocalConnectionId, out _)) return false;
            var signal = new MatchLifecycleSignal(Descriptor.Config.MatchId, Descriptor.DescriptorId);
            if (_network.IsServer)
            {
                MatchReadinessStatus result = _barrier.MarkReady(_network.LocalConnectionId, signal.MatchId, signal.DescriptorId);
                if (result != MatchReadinessStatus.Accepted && result != MatchReadinessStatus.AlreadyReady) return false;
            }
            else if (!_transport.TrySendSignal(_serverConnectionId, MatchLifecycleSignalKind.Ready, signal)) return false;
            _localReady = true;
            return true;
        }
        public bool TryStart()
        {
            ThrowIfDisposed();
            if (!CanStart || !_barrier.TryStart(_network.LocalConnectionId)) return false;
            var current = Descriptor;
            var signal = new MatchLifecycleSignal(current.Config.MatchId, current.DescriptorId);
            foreach (MatchParticipantBinding binding in current.Participants)
                if (binding.ConnectionId != _network.LocalConnectionId &&
                    !_transport.TrySendSignal(binding.ConnectionId, MatchLifecycleSignalKind.Started, signal))
                { TryEnd(); return false; }
            if (_disposed || Descriptor != current || Phase != NetworkMatchPhase.Preparing) return false;
            Phase = NetworkMatchPhase.Running;
            Notify(Started, Descriptor);
            return true;
        }
        public bool TryEnd()
        {
            ThrowIfDisposed();
            if (!_network.IsServer || Descriptor == null) return false;
            EndAuthority();
            return true;
        }
        private void EndAuthority()
        {
            var current = Descriptor;
            var signal = new MatchLifecycleSignal(current.Config.MatchId, current.DescriptorId);
            // 먼저 로컬 소유권을 끊어 종료 알림의 재진입이 같은 Match를 다시 종료하지 않게 합니다.
            List<Exception> errors = null;
            try { EndLocal(); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            foreach (MatchParticipantBinding binding in current.Participants)
                if (binding.ConnectionId != _network.LocalConnectionId)
                    try { _transport.TrySendSignal(binding.ConnectionId, MatchLifecycleSignalKind.Ended, signal); }
                    catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            if (errors != null) throw new AggregateException("Match ended; some cleanup or notifications failed.", errors);
        }
        public bool TryResolveParticipant(ulong verifiedConnectionId, out PlayerId playerId)
        {
            if (!_disposed && Descriptor != null)
                foreach (MatchParticipantBinding binding in Descriptor.Participants)
                    if (binding.ConnectionId == verifiedConnectionId) { playerId = binding.PlayerId; return true; }
            playerId = default; return false;
        }
        private void OnDescriptor(ulong sender, NetworkMatchDescriptor descriptor)
        {
            if (_disposed || _network.IsServer || sender != _serverConnectionId ||
                _network.State != NetworkConnectionState.ConnectedClient || Phase == NetworkMatchPhase.Running ||
                descriptor == null || descriptor.PreparationSequence <= _preparationSequence) return;
            bool localBound = false, correctHost = false;
            foreach (MatchParticipantBinding binding in descriptor.Participants)
            {
                if (binding.ConnectionId == _network.LocalConnectionId && !binding.IsHost) localBound = true;
                if (binding.ConnectionId == _serverConnectionId && binding.IsHost) correctHost = true;
            }
            if (!localBound || !correctHost) return;
            Descriptor = descriptor; _preparationSequence = descriptor.PreparationSequence;
            _localReady = false; Phase = NetworkMatchPhase.Preparing;
            Notify(Preparing, descriptor);
        }
        private void OnSignal(ulong sender, MatchLifecycleSignalKind kind, MatchLifecycleSignal signal)
        {
            if (_disposed || Descriptor == null || signal.MatchId != Descriptor.Config.MatchId ||
                signal.DescriptorId != Descriptor.DescriptorId) return;
            if (_network.IsServer)
            {
                if (kind == MatchLifecycleSignalKind.Ready && Phase == NetworkMatchPhase.Preparing)
                    _barrier.MarkReady(sender, signal.MatchId, signal.DescriptorId);
                return;
            }
            if (sender != _serverConnectionId) return;
            if (kind == MatchLifecycleSignalKind.Ended) EndLocal();
            else if (kind == MatchLifecycleSignalKind.Started && _localReady && Phase == NetworkMatchPhase.Preparing)
            { Phase = NetworkMatchPhase.Running; Notify(Started, Descriptor); }
        }
        private void OnPeerDisconnected(ulong connection)
        {
            if (_disposed || Descriptor == null || !TryResolveParticipant(connection, out _)) return;
            if (_network.IsServer) TryEnd();
            else if (connection == _network.LocalConnectionId || connection == _serverConnectionId) EndLocal();
        }
        private void OnConnectionState(NetworkConnectionState state)
        {
            if (!_disposed && state == NetworkConnectionState.Disconnected && !_network.IsServer) _preparationSequence = 0;
            if (!_disposed && Descriptor != null &&
                (state == NetworkConnectionState.Disconnected || state == NetworkConnectionState.Stopping ||
                 state == NetworkConnectionState.Faulted || state == NetworkConnectionState.Disposed)) EndLocal();
        }
        private void EndLocal()
        {
            var current = Descriptor;
            Descriptor = null; _barrier = null; _localReady = false;
            Phase = NetworkMatchPhase.Ended;
            if (current != null) Notify(Ended, current.Config.MatchId);
        }
        private static void Notify<T>(Action<T> handlers, T value)
        {
            if (handlers == null) return;
            List<Exception> errors = null;
            foreach (Action<T> handler in handlers.GetInvocationList())
                try { handler(value); } catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
            if (errors != null) throw new AggregateException("Match transition committed; Feature observation failed.", errors);
        }
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            try
            {
                if (_network.IsServer && Descriptor != null) EndAuthority();
                else EndLocal();
            }
            finally
            {
                _transport.DescriptorReceived -= OnDescriptor; _transport.SignalReceived -= OnSignal;
                _network.PeerDisconnected -= OnPeerDisconnected; _network.StateChanged -= OnConnectionState;
                Preparing = null; Started = null; Ended = null; Phase = NetworkMatchPhase.Disposed;
            }
        }
        private void ThrowIfDisposed() { if (_disposed) throw new ObjectDisposedException(nameof(NetworkMatchCoordinator)); }
    }
}
