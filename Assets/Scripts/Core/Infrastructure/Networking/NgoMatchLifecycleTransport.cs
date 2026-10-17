// NGO의 실제 발신 연결과 타입 Match 메시지를 Application Port에 연결합니다.
// 큰 Grid 설정은 제한된 분할 큐로 보내며 프레임당 Pump 예산으로 전송 폭주를 방지합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoMatchLifecycleTransport : IMatchLifecycleTransport, INetworkMessagePump, IDisposable
    {
        private const string ChannelName = "teamhjd.core.match.v1";
        private const int PacketMagic = 0x31434A48;
        private readonly NetworkManager _manager;
        private readonly BoundedPacketAssembler _assembler = new BoundedPacketAssembler();
        private sealed class Outgoing
        {
            public ulong Target, Low, High; public byte[] Bytes; public int Offset;
        }
        private readonly Queue<Outgoing> _outgoing = new Queue<Outgoing>();
        private int _pendingBytes;
        private bool _disposed;
        public long RejectedPackets { get; private set; }
        public int PendingOutgoingMessages => _outgoing.Count;
        public event Action<ulong, NetworkMatchDescriptor> DescriptorReceived;
        public event Action<ulong, MatchLifecycleSignalKind, MatchLifecycleSignal> SignalReceived;
        public NgoMatchLifecycleTransport(NetworkManager manager)
        {
            if (manager == null || !manager.IsListening || manager.CustomMessagingManager == null)
                throw new ArgumentException("Attach the lifecycle channel after the network starts.", nameof(manager));
            _manager = manager;
            manager.CustomMessagingManager.RegisterNamedMessageHandler(ChannelName, OnMessage);
            manager.OnClientDisconnectCallback += OnPeerDisconnected;
        }
        public bool TrySendDescriptor(ulong target, NetworkMatchDescriptor descriptor)
        {
            if (_disposed || !_manager.IsServer) return false;
            try { return Queue(target, MatchLifecycleCodec.Encode(descriptor)); }
            catch (ArgumentException) { return false; }
        }
        public bool TrySendSignal(ulong target, MatchLifecycleSignalKind kind, MatchLifecycleSignal signal)
        {
            if (_disposed || (_manager.IsServer ? kind == MatchLifecycleSignalKind.Ready : kind != MatchLifecycleSignalKind.Ready)) return false;
            try { return Queue(target, MatchLifecycleCodec.Encode(kind, signal)); }
            catch (ArgumentException) { return false; }
        }
        private bool Queue(ulong target, byte[] bytes)
        {
            if (!IsPeer(target) || _outgoing.Count >= 8 || bytes.Length > BoundedPacketAssembler.MaximumPendingBytes - _pendingBytes) return false;
            byte[] id = Guid.NewGuid().ToByteArray();
            _outgoing.Enqueue(new Outgoing { Target = target, Low = BitConverter.ToUInt64(id, 0),
                High = BitConverter.ToUInt64(id, 8), Bytes = bytes });
            _pendingBytes += bytes.Length;
            return true;
        }
        /// <summary>소유한 Unity 조립부의 Update에서 호출합니다. 게임시간 정지 중에도 네트워크는 진행합니다.</summary>
        public void Pump(int packetBudget = 8)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NgoMatchLifecycleTransport));
            if (packetBudget < 1) throw new ArgumentOutOfRangeException(nameof(packetBudget));
            _assembler.Prune(Time.realtimeSinceStartupAsDouble);
            for (int count = 0; count < packetBudget && _outgoing.Count > 0; count++)
            {
                Outgoing job = _outgoing.Peek();
                if (!IsPeer(job.Target)) { RemoveOutgoing(); continue; }
                int bytes = Math.Min(BoundedPacketAssembler.ChunkBytes, job.Bytes.Length - job.Offset);
                using var writer = new FastBufferWriter(32 + BoundedPacketAssembler.ChunkBytes, Allocator.Temp);
                writer.WriteValueSafe(PacketMagic); writer.WriteValueSafe(job.Low); writer.WriteValueSafe(job.High);
                writer.WriteValueSafe(job.Bytes.Length); writer.WriteValueSafe(job.Offset);
                writer.WriteBytesSafe(job.Bytes, bytes, job.Offset);
                _manager.CustomMessagingManager.SendNamedMessage(ChannelName, job.Target, writer, NetworkDelivery.ReliableSequenced);
                job.Offset += bytes;
                if (job.Offset == job.Bytes.Length) RemoveOutgoing();
            }
        }
        void INetworkMessagePump.Pump() => Pump();
        private void RemoveOutgoing() { _pendingBytes -= _outgoing.Dequeue().Bytes.Length; }
        private bool IsPeer(ulong id)
        {
            if (_manager == null || !_manager.IsListening || _manager.ShutdownInProgress || id == _manager.LocalClientId) return false;
            if (!_manager.IsServer) return id == NetworkManager.ServerClientId && _manager.IsConnectedClient;
            return _manager.ConnectedClients.ContainsKey(id);
        }
        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            // NGO는 NamedMessage hash를 읽은 reader를 넘기므로 전체 Length가 아니라 남은 payload를 검사합니다.
            int available = reader.Length - reader.Position;
            if (_disposed || !IsPeer(sender) || available < 28 || available > 28 + BoundedPacketAssembler.ChunkBytes)
            { RejectedPackets++; return; }
            NetworkMatchDescriptor descriptor;
            MatchLifecycleSignalKind kind;
            MatchLifecycleSignal signal;
            try
            {
                reader.ReadValueSafe(out int magic); reader.ReadValueSafe(out ulong low); reader.ReadValueSafe(out ulong high);
                reader.ReadValueSafe(out int total); reader.ReadValueSafe(out int offset);
                if (magic != PacketMagic || (_manager.IsServer && total > 4096)) { RejectedPackets++; return; }
                byte[] id = new byte[16]; Array.Copy(BitConverter.GetBytes(low), 0, id, 0, 8); Array.Copy(BitConverter.GetBytes(high), 0, id, 8, 8);
                byte[] chunk = new byte[reader.Length - reader.Position]; reader.ReadBytesSafe(ref chunk, chunk.Length);
                PacketAssemblyStatus state = _assembler.Add(sender, new Guid(id), total, offset, chunk, Time.realtimeSinceStartupAsDouble, out byte[] message);
                if (state == PacketAssemblyStatus.Rejected) { RejectedPackets++; return; }
                if (state != PacketAssemblyStatus.Complete) return;
                MatchLifecycleCodec.Decode(message, out descriptor, out kind, out signal);
            }
            catch (Exception error) when (error is FormatException || error is OverflowException || error is ArgumentException)
            { RejectedPackets++; return; }
            // 소비자 예외를 잘못된 패킷으로 숨기지 않습니다. 상태 전이/정리 오류는 호출 경계에 보고합니다.
            if (_manager.IsServer)
            {
                if (descriptor != null || kind != MatchLifecycleSignalKind.Ready) { RejectedPackets++; return; }
                SignalReceived?.Invoke(sender, kind, signal);
            }
            else if (descriptor != null) DescriptorReceived?.Invoke(sender, descriptor);
            else if (kind != MatchLifecycleSignalKind.Ready) SignalReceived?.Invoke(sender, kind, signal);
            else RejectedPackets++;
        }
        private void OnPeerDisconnected(ulong id) => _assembler.RemovePeer(id);
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            if (_manager != null)
            {
                _manager.OnClientDisconnectCallback -= OnPeerDisconnected;
                _manager.CustomMessagingManager?.UnregisterNamedMessageHandler(ChannelName);
            }
            _outgoing.Clear(); _pendingBytes = 0; _assembler.Dispose();
            DescriptorReceived = null; SignalReceived = null;
        }
    }
}
