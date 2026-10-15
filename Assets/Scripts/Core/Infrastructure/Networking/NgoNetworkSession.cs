// 명시적으로 전달받은 NGO 관리자를 공용 연결 Port에 연결합니다.
// 실제 전송 설정·객체 복제는 NGO가 담당하고 이 어댑터는 Match나 Enemy를 생성하지 않습니다.
// 자신이 시작한 연결만 종료하며 외부 테스트 Host의 소유권을 암묵적으로 가져오지 않습니다.
using System;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
using UnityEngine;

namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoNetworkSession : INetworkSession
    {
        private readonly NetworkManager _manager;
        private bool _ownsConnection;
        private bool _isDisposed;
        /// <summary>SDK가 필요한 조립부에만 제공하며 Feature의 전역 조회 경로로 쓰지 않습니다.</summary>
        public NetworkManager Manager => _manager;
        public NetworkConnectionState State { get; private set; }
        public bool IsServer => !_isDisposed && _manager != null && _manager.IsServer && _manager.IsListening;
        public ulong LocalConnectionId => !_isDisposed && _manager != null ? _manager.LocalClientId : 0;
        public event Action<NetworkConnectionState> StateChanged;
        public event Action<ulong> PeerConnected;
        public event Action<ulong> PeerDisconnected;

        public NgoNetworkSession(NetworkManager manager)
        {
            _manager = manager != null ? manager : throw new ArgumentNullException(nameof(manager));
            State = ReadState();
            _manager.OnServerStarted += OnServerStarted;
            _manager.OnClientConnectedCallback += OnPeerConnected;
            _manager.OnClientDisconnectCallback += OnPeerDisconnected;
            _manager.OnServerStopped += OnStopped;
            _manager.OnClientStopped += OnStopped;
            _manager.OnTransportFailure += OnTransportFailure;
        }

        public bool TryStartHost() => Start(true);
        public bool TryStartClient() => Start(false);

        private bool Start(bool host)
        {
            ThrowIfUnavailable();
            if (_manager.IsListening || _manager.ShutdownInProgress ||
                State == NetworkConnectionState.StartingHost || State == NetworkConnectionState.StartingClient)
                return false;
            SetState(host ? NetworkConnectionState.StartingHost : NetworkConnectionState.StartingClient);
            if (_isDisposed) return false;
            // SDK 콜백은 Start 호출 중에도 발생합니다. 소유권은 호출 전에 설정합니다.
            _ownsConnection = true;
            try
            {
                bool accepted = host ? _manager.StartHost() : _manager.StartClient();
                if (_isDisposed)
                {
                    if (_manager != null && _manager.IsListening && !_manager.ShutdownInProgress) _manager.Shutdown();
                    return false;
                }
                if (!accepted)
                {
                    _ownsConnection = false;
                    SetState(NetworkConnectionState.Faulted);
                    return false;
                }
                SetState(ReadState());
                return true;
            }
            catch
            {
                SetState(NetworkConnectionState.Faulted);
                // 일부 초기화 후 실패한 연결도 같은 소유자가 SDK 종료 경로로 정리합니다.
                if (_manager != null && _manager.IsListening) _manager.Shutdown();
                _ownsConnection = false;
                throw;
            }
        }

        public void Shutdown()
        {
            ThrowIfUnavailable();
            if (!_ownsConnection) return;
            if (_manager.ShutdownInProgress) { SetState(NetworkConnectionState.Stopping); return; }
            if (!_manager.IsListening)
            {
                _ownsConnection = false;
                SetState(NetworkConnectionState.Disconnected);
                return;
            }
            SetState(NetworkConnectionState.Stopping);
            _manager.Shutdown();
        }

        private NetworkConnectionState ReadState()
        {
            if (_manager == null) return NetworkConnectionState.Disconnected;
            if (_manager.ShutdownInProgress) return NetworkConnectionState.Stopping;
            if (_manager.IsListening && _manager.IsHost) return NetworkConnectionState.Hosting;
            if (_manager.IsConnectedClient) return NetworkConnectionState.ConnectedClient;
            return _manager.IsListening ? NetworkConnectionState.StartingClient : NetworkConnectionState.Disconnected;
        }

        private void OnServerStarted() => SetState(ReadState());
        private void OnPeerConnected(ulong id)
        {
            SetState(ReadState());
            Notify(PeerConnected, id);
        }
        private void OnPeerDisconnected(ulong id)
        {
            Notify(PeerDisconnected, id);
            SetState(ReadState());
        }
        private void OnStopped(bool wasHost)
        {
            if (_isDisposed || _manager == null) return;
            SetState(ReadState());
            if (State == NetworkConnectionState.Disconnected) _ownsConnection = false;
        }
        private void OnTransportFailure()
        {
            if (!_isDisposed) SetState(NetworkConnectionState.Faulted);
        }

        private void SetState(NetworkConnectionState state)
        {
            if (_isDisposed || State == state) return;
            State = state;
            Notify(StateChanged, state);
        }

        private static void Notify<T>(Action<T> observers, T value)
        {
            if (observers == null) return;
            foreach (Action<T> observer in observers.GetInvocationList())
                try { observer(value); }
                catch (Exception exception) { Debug.LogException(exception); }
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            State = NetworkConnectionState.Disposed;
            Notify(StateChanged, State);
            if (_manager != null)
            {
                _manager.OnServerStarted -= OnServerStarted;
                _manager.OnClientConnectedCallback -= OnPeerConnected;
                _manager.OnClientDisconnectCallback -= OnPeerDisconnected;
                _manager.OnServerStopped -= OnStopped;
                _manager.OnClientStopped -= OnStopped;
                _manager.OnTransportFailure -= OnTransportFailure;
                if (_ownsConnection && _manager.IsListening && !_manager.ShutdownInProgress) _manager.Shutdown();
            }
            _ownsConnection = false;
            StateChanged = null;
            PeerConnected = null;
            PeerDisconnected = null;
        }

        private void ThrowIfUnavailable()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(NgoNetworkSession));
            if (_manager == null) throw new InvalidOperationException("The configured NGO manager has been destroyed.");
        }
    }
}
