// 환경 전체 상태를 Host에서만 보내며 Client 상태 주입을 거부합니다.
// 아직 보내지 않은 상태는 최신 값으로 합치고 연결 소유자는 App에 남깁니다.
using System;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoEnvironmentTransport : IMatchEnvironmentTransport, INetworkMessagePump, IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly NgoMessageChannel _channel;
        private EnvironmentReplicationFrame _cached;
        private byte[] _bytes;
        private bool _disposed;
        private long _rejected;
        public long RejectedPackets => _channel.RejectedPackets + _rejected;
        public event Action<ulong, EnvironmentReplicationFrame> FrameReceived;
        public NgoEnvironmentTransport(NetworkManager manager)
        {
            _manager = manager; _channel = new NgoMessageChannel(manager, "teamhjd.core.environment.v1", 0);
            _channel.MessageReceived += OnMessage;
        }
        public bool TrySendFrame(ulong connection, EnvironmentReplicationFrame frame)
        {
            if (_disposed || _manager == null || !_manager.IsServer || frame == null) return false;
            if (!ReferenceEquals(_cached, frame)) { _bytes = EnvironmentReplicationCodec.Encode(frame); _cached = frame; }
            return _channel.TrySend(connection, _bytes, true);
        }
        public void Pump() => _channel.Pump();
        private void OnMessage(ulong sender, byte[] bytes)
        {
            if (_manager.IsServer || sender != NetworkManager.ServerClientId) { _rejected++; return; }
            EnvironmentReplicationFrame frame;
            try { frame = EnvironmentReplicationCodec.Decode(bytes); }
            catch (FormatException) { _rejected++; return; }
            FrameReceived?.Invoke(sender, frame);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _channel.MessageReceived -= OnMessage; _channel.Dispose(); _cached = null; _bytes = null; FrameReceived = null;
        }
    }
}
