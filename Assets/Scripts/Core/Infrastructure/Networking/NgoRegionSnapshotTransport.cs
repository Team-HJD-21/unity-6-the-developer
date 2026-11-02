// 확정 지역 상태를 서버에서 Client로만 전달합니다.
// 아직 보내지 않은 상태는 최신 Snapshot으로 합치고 실행/준비 명령 채널과 분리합니다.
using System;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoRegionSnapshotTransport : IRegionSnapshotTransport, INetworkMessagePump, IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly NgoMessageChannel _channel;
        private RegionReplicationFrame _cachedFrame;
        private byte[] _cachedBytes;
        private bool _disposed;
        private long _rejectedPayloads;
        public long RejectedPackets => _channel.RejectedPackets + _rejectedPayloads;
        public long CoalescedFrames => _channel.CoalescedMessages;
        public event Action<ulong, RegionReplicationFrame> FrameReceived;
        public NgoRegionSnapshotTransport(NetworkManager manager)
        {
            _manager = manager;
            _channel = new NgoMessageChannel(manager, "teamhjd.core.regions.v1", 0);
            _channel.MessageReceived += OnMessage;
        }
        public bool TrySendFrame(ulong connectionId, RegionReplicationFrame frame)
        {
            if (_disposed || !_manager.IsServer || frame == null) return false;
            if (!ReferenceEquals(_cachedFrame, frame))
            {
                _cachedBytes = RegionReplicationCodec.Encode(frame);
                _cachedFrame = frame;
            }
            return _channel.TrySend(connectionId, _cachedBytes, true);
        }
        public void Pump() => _channel.Pump();
        private void OnMessage(ulong sender, byte[] bytes)
        {
            if (_manager.IsServer || sender != NetworkManager.ServerClientId) { _rejectedPayloads++; return; }
            RegionReplicationFrame frame;
            try { frame = RegionReplicationCodec.Decode(bytes); }
            catch (FormatException) { _rejectedPayloads++; return; }
            FrameReceived?.Invoke(sender, frame);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _channel.MessageReceived -= OnMessage; _channel.Dispose();
            _cachedFrame = null; _cachedBytes = null; FrameReceived = null;
        }
    }
}
