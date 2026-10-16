// Match 한 세대의 로컬/원격 의도를 같은 서버 서비스로 보내고 확정 결과를 상관시킵니다.
// 원격 발신자를 연결 매핑으로 검증하며 응답 미도착을 실행 실패로 단정하지 않습니다.
// 미확정 요청은 제한된 개수로 보존하고 같은 GUID로만 명시적 재전송합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class TurretCommandGateway : ITurretCommandRequester, INetworkMessagePump, IDisposable
    {
        private readonly MatchId _matchId;
        private readonly Guid _descriptorId;
        private readonly PlayerId _localPlayer;
        private readonly ulong _serverConnection;
        private readonly INetworkSession _network;
        private readonly ITurretCommandTransport _transport;
        private readonly ITurretCommandService _service;
        private readonly Func<bool> _isRunning;
        private readonly Func<ulong, PlayerId?> _resolveParticipant;
        private readonly Dictionary<Guid, TurretCommandRequest> _pending = new Dictionary<Guid, TurretCommandRequest>();
        private readonly Queue<(ulong connection, TurretCommandResult result)> _replies = new Queue<(ulong, TurretCommandResult)>();
        private readonly int _capacity;
        private readonly bool _isAuthority;
        private bool CanOperate
        {
            get
            {
                if (_disposed || _network.IsServer != _isAuthority ||
                    _network.State != (_isAuthority ? NetworkConnectionState.Hosting : NetworkConnectionState.ConnectedClient)) return false;
                bool running = _isRunning();
                return running && !_disposed && _network.IsServer == _isAuthority;
            }
        }
        private bool _disposed;
        public int PendingCount => _pending.Count;
        public long RejectedMessages { get; private set; }
        public event Action<TurretCommandResult> ResultReceived;
        public TurretCommandGateway(MatchId matchId, Guid descriptorId, PlayerId localPlayer,
            ulong serverConnection, INetworkSession network, ITurretCommandTransport transport,
            Func<bool> isRunning, Func<ulong, PlayerId?> resolveParticipant,
            ITurretCommandService authorityService = null, int capacity = 64)
        {
            if (matchId.IsEmpty || descriptorId == Guid.Empty || localPlayer.IsEmpty) throw new ArgumentException("A ready Match and player are required.");
            if (capacity < 1 || capacity > 4096) throw new ArgumentOutOfRangeException(nameof(capacity));
            _network = network ?? throw new ArgumentNullException(nameof(network));
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _isRunning = isRunning ?? throw new ArgumentNullException(nameof(isRunning));
            _resolveParticipant = resolveParticipant ?? throw new ArgumentNullException(nameof(resolveParticipant));
            if (network.IsServer && authorityService == null) throw new ArgumentNullException(nameof(authorityService));
            if (!network.IsServer && authorityService != null) throw new ArgumentException("A Client must not own the authority service.");
            _matchId = matchId; _descriptorId = descriptorId; _localPlayer = localPlayer;
            _serverConnection = serverConnection; _service = authorityService; _capacity = capacity;
            _isAuthority = network.IsServer;
            _transport.RequestReceived += OnRequest; _transport.ResultReceived += OnResult;
        }
        public bool TrySubmit(TurretCommandRequest request)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (!CanSubmit(request)) return false;
            if (_network.IsServer)
            {
                TurretCommandResult result = _service.Submit(request, _localPlayer);
                if (!_disposed) ResultReceived?.Invoke(result);
                return true;
            }
            if (_pending.TryGetValue(request.Context.RequestId, out var previous))
                return previous.GetPayloadKey() == request.GetPayloadKey() && TryRetry(request.Context.RequestId);
            if (_pending.Count >= _capacity) return false;
            _pending.Add(request.Context.RequestId, request);
            if (_transport.TrySendRequest(_serverConnection, _descriptorId, request)) return true;
            _pending.Remove(request.Context.RequestId); return false;
        }
        private bool CanSubmit(TurretCommandRequest request) => CanOperate &&
            request.Context.MatchId == _matchId && request.Context.IssuerId == _localPlayer &&
            _resolveParticipant(_network.LocalConnectionId) == _localPlayer;
        public bool TryRetry(Guid requestId) => !_disposed && _pending.TryGetValue(requestId, out var request) &&
            CanSubmit(request) && _transport.TrySendRequest(_serverConnection, _descriptorId, request);
        private void OnRequest(ulong sender, Guid descriptorId, TurretCommandRequest request)
        {
            if (!CanOperate || !_isAuthority || descriptorId != _descriptorId ||
                request == null || request.Context.MatchId != _matchId || _replies.Count >= _capacity)
            { RejectedMessages++; return; }
            PlayerId? actual = _resolveParticipant(sender);
            if (!actual.HasValue || actual.Value.IsEmpty) { RejectedMessages++; return; }
            TurretCommandResult result = _service.Submit(request, actual.Value);
            // Feature 콜백이 Match를 끝냈다면 폐기된 gateway에 늦은 응답을 쌓지 않습니다.
            if (CanOperate) _replies.Enqueue((sender, result));
        }
        private void OnResult(ulong sender, Guid descriptorId, TurretCommandResult result)
        {
            if (!CanOperate || _isAuthority || sender != _serverConnection ||
                descriptorId != _descriptorId || result == null || result.MatchId != _matchId ||
                !_pending.TryGetValue(result.RequestId, out var request) || request.Kind != result.Kind || request.TargetId != result.TargetId)
            { RejectedMessages++; return; }
            // 재진입 중 응답은 최종 결과가 아닙니다. 같은 요청의 확정 응답/명시적 재전송을 기다립니다.
            if (result.Status == TurretCommandStatus.InProgress) return;
            _pending.Remove(result.RequestId); ResultReceived?.Invoke(result);
        }
        public void Pump()
        {
            if (_disposed) return;
            if (!CanOperate) { _replies.Clear(); return; }
            for (int i = 0; i < 8 && _replies.Count > 0; i++)
            {
                var next = _replies.Peek();
                if (!_resolveParticipant(next.connection).HasValue) { _replies.Dequeue(); continue; }
                if (!_transport.TrySendResult(next.connection, _descriptorId, next.result)) break;
                _replies.Dequeue();
            }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _transport.RequestReceived -= OnRequest; _transport.ResultReceived -= OnResult;
            _pending.Clear(); _replies.Clear(); ResultReceived = null;
        }
    }
}
