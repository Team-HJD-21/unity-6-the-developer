// Client 의도는 Host에만, 확정 결과는 Host에서만 전달하는 실제 NGO 어댑터입니다.
// 기존 제한 채널을 재사용하고 명령을 최신 상태처럼 합치거나 자동 재실행하지 않습니다.
using System;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoTurretCommandTransport : ITurretCommandTransport, INetworkMessagePump, IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly NgoMessageChannel _channel;
        private long _rejectedPayloads;
        private bool _disposed;
        public long RejectedPackets => _channel.RejectedPackets + _rejectedPayloads;
        public event Action<ulong, Guid, TurretCommandRequest> RequestReceived;
        public event Action<ulong, Guid, TurretCommandResult> ResultReceived;
        public NgoTurretCommandTransport(NetworkManager manager)
        {
            _manager = manager;
            _channel = new NgoMessageChannel(manager, "teamhjd.core.turret.v1", TurretCommandCodec.MaximumMessageBytes);
            _channel.MessageReceived += OnMessage;
        }
        public bool TrySendRequest(ulong target, Guid descriptorId, TurretCommandRequest request)
        {
            if (_disposed || _manager.IsServer || target != NetworkManager.ServerClientId) return false;
            try { return _channel.TrySend(target, TurretCommandCodec.Encode(descriptorId, request)); }
            catch (ArgumentException) { return false; }
        }
        public bool TrySendResult(ulong target, Guid descriptorId, TurretCommandResult result)
        {
            if (_disposed || !_manager.IsServer) return false;
            try { return _channel.TrySend(target, TurretCommandCodec.Encode(descriptorId, result)); }
            catch (ArgumentException) { return false; }
        }
        private void OnMessage(ulong sender, byte[] bytes)
        {
            Guid descriptor; TurretCommandRequest request; TurretCommandResult result;
            try { TurretCommandCodec.Decode(bytes, out descriptor, out request, out result); }
            catch (FormatException) { _rejectedPayloads++; return; }
            if (_manager.IsServer && request != null) RequestReceived?.Invoke(sender, descriptor, request);
            else if (!_manager.IsServer && sender == NetworkManager.ServerClientId && result != null)
                ResultReceived?.Invoke(sender, descriptor, result);
            else _rejectedPayloads++;
        }
        public void Pump() => _channel.Pump();
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _channel.MessageReceived -= OnMessage; _channel.Dispose();
            RequestReceived = null; ResultReceived = null;
        }
    }
}
