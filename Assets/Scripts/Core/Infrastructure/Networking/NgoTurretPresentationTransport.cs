// 서버의 전체 HUD 투영만 전송하며 아직 보내지 않은 동일 대상 상태만 최신 것으로 합칩니다.
// Client 상태 제출은 조각 조립 전에 거부하고 수명/명령 채널과 독립적으로 예산을 적용합니다.
using System;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoTurretPresentationTransport : ITurretPresentationTransport, INetworkMessagePump, IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly NgoMessageChannel _channel;
        private TurretPresentationFrame _cachedFrame;
        private byte[] _cachedBytes;
        private bool _disposed;
        private long _rejectedPayloads;
        public long RejectedPackets => _channel.RejectedPackets + _rejectedPayloads;
        public long CoalescedFrames => _channel.CoalescedMessages;
        public event Action<ulong, TurretPresentationFrame> FrameReceived;
        public NgoTurretPresentationTransport(NetworkManager manager)
        {
            _manager = manager; _channel = new NgoMessageChannel(manager, "teamhjd.core.turret-presentation.v2", 0);
            _channel.MessageReceived += OnMessage;
        }
        public bool TrySendFrame(ulong connectionId, TurretPresentationFrame frame)
        {
            if (_disposed || !_manager.IsServer || frame == null) return false;
            if (!ReferenceEquals(frame, _cachedFrame))
            { _cachedBytes = TurretPresentationCodec.Encode(frame); _cachedFrame = frame; }
            return _channel.TrySend(connectionId, _cachedBytes, true);
        }
        private void OnMessage(ulong sender, byte[] bytes)
        {
            if (_disposed || _manager.IsServer || sender != NetworkManager.ServerClientId) { _rejectedPayloads++; return; }
            TurretPresentationFrame frame;
            try { frame = TurretPresentationCodec.Decode(bytes); }
            catch (FormatException) { _rejectedPayloads++; return; }
            FrameReceived?.Invoke(sender, frame);
        }
        public void Pump() => _channel.Pump();
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _channel.MessageReceived -= OnMessage; _channel.Dispose(); _cachedFrame = null; _cachedBytes = null; FrameReceived = null;
        }
    }
}
