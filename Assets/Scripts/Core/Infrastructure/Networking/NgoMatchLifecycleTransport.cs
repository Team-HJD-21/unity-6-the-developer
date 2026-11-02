// Match 수명 DTO와 공용 NGO 바이트 채널을 연결합니다.
// 준비/시작/종료 메시지는 합치지 않고 실제 발신 역할을 다시 검사합니다.
using System;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoMatchLifecycleTransport : IMatchLifecycleTransport, INetworkMessagePump, IDisposable
    {
        private readonly NetworkManager _manager;
        private readonly NgoMessageChannel _channel;
        private long _rejectedPayloads;
        private bool _disposed;
        public long RejectedPackets => _channel.RejectedPackets + _rejectedPayloads;
        public int PendingOutgoingMessages => _channel.PendingOutgoingMessages;
        public event Action<ulong, NetworkMatchDescriptor> DescriptorReceived;
        public event Action<ulong, MatchLifecycleSignalKind, MatchLifecycleSignal> SignalReceived;
        public NgoMatchLifecycleTransport(NetworkManager manager)
        {
            _manager = manager;
            _channel = new NgoMessageChannel(manager, "teamhjd.core.match.v1");
            _channel.MessageReceived += OnMessage;
        }
        public bool TrySendDescriptor(ulong target, NetworkMatchDescriptor descriptor)
        {
            if (_disposed || !_manager.IsServer) return false;
            try { return _channel.TrySend(target, MatchLifecycleCodec.Encode(descriptor)); }
            catch (ArgumentException) { return false; }
        }
        public bool TrySendSignal(ulong target, MatchLifecycleSignalKind kind, MatchLifecycleSignal signal)
        {
            if (_disposed || (_manager.IsServer ? kind == MatchLifecycleSignalKind.Ready : kind != MatchLifecycleSignalKind.Ready)) return false;
            try { return _channel.TrySend(target, MatchLifecycleCodec.Encode(kind, signal)); }
            catch (ArgumentException) { return false; }
        }
        public void Pump(int packetBudget = 8) => _channel.Pump(packetBudget);
        void INetworkMessagePump.Pump() => Pump();
        private void OnMessage(ulong sender, byte[] bytes)
        {
            NetworkMatchDescriptor descriptor; MatchLifecycleSignalKind kind; MatchLifecycleSignal signal;
            try { MatchLifecycleCodec.Decode(bytes, out descriptor, out kind, out signal); }
            catch (FormatException) { _rejectedPayloads++; return; }
            if (_manager.IsServer)
            {
                if (descriptor != null || kind != MatchLifecycleSignalKind.Ready) { _rejectedPayloads++; return; }
                SignalReceived?.Invoke(sender, kind, signal);
            }
            else if (descriptor != null) DescriptorReceived?.Invoke(sender, descriptor);
            else if (kind != MatchLifecycleSignalKind.Ready) SignalReceived?.Invoke(sender, kind, signal);
            else _rejectedPayloads++;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            _channel.MessageReceived -= OnMessage; _channel.Dispose();
            DescriptorReceived = null; SignalReceived = null;
        }
    }
}
