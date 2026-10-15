// 실제 NGO 연결의 신뢰 전송·분할 조립·프레임 예산을 담당하는 공용 채널입니다.
// 상태 Snapshot만 미전송 결과를 최신 것으로 합칠 수 있으며 명령 순서는 보존합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class NgoMessageChannel : INetworkMessagePump, IDisposable
    {
        private readonly string _channelName;
        private readonly int _maximumClientMessageBytes;
        private const int PacketMagic = 0x31434A48;
        private readonly NetworkManager _manager;
        private CustomMessagingManager _registeredMessaging;
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
        public long CoalescedMessages { get; private set; }
        public event Action<ulong, byte[]> MessageReceived;
        public NgoMessageChannel(NetworkManager manager, string channelName, int maximumClientMessageBytes = 4096)
        {
            if (manager == null || !manager.IsListening || manager.CustomMessagingManager == null)
                throw new ArgumentException("Attach the lifecycle channel after the network starts.", nameof(manager));
            if (string.IsNullOrWhiteSpace(channelName)) throw new ArgumentException("A channel name is required.", nameof(channelName));
            if (maximumClientMessageBytes < 0 || maximumClientMessageBytes > NetworkMessageLimits.MaximumMessageBytes)
                throw new ArgumentOutOfRangeException(nameof(maximumClientMessageBytes));
            _manager = manager;
            _channelName = channelName;
            _maximumClientMessageBytes = maximumClientMessageBytes;
            RegisterMessaging();
            manager.OnClientDisconnectCallback += OnPeerDisconnected;
            manager.OnServerStarted += RegisterMessaging;
            manager.OnClientStarted += RegisterMessaging;
            manager.OnServerStopped += OnStopped;
            manager.OnClientStopped += OnStopped;
        }
        private void RegisterMessaging()
        {
            if (_disposed || _manager == null || _manager.CustomMessagingManager == null ||
                ReferenceEquals(_registeredMessaging, _manager.CustomMessagingManager)) return;
            _registeredMessaging?.UnregisterNamedMessageHandler(_channelName);
            _registeredMessaging = _manager.CustomMessagingManager;
            _registeredMessaging.RegisterNamedMessageHandler(_channelName, OnMessage);
        }
        private void OnStopped(bool wasHost)
        {
            if (_disposed) return;
            _registeredMessaging?.UnregisterNamedMessageHandler(_channelName);
            _registeredMessaging = null;
            _outgoing.Clear(); _pendingBytes = 0; _assembler.Clear();
        }
        public bool TrySend(ulong target, byte[] bytes, bool replaceUnsent = false)
        {
            if (_disposed || bytes == null || bytes.Length < 1 || bytes.Length > NetworkMessageLimits.MaximumMessageBytes || !IsPeer(target))
                return false;
            if (replaceUnsent)
                foreach (Outgoing pending in _outgoing)
                    if (pending.Target == target && pending.Offset == 0)
                    {
                        if (bytes.Length > BoundedPacketAssembler.MaximumPendingBytes - _pendingBytes + pending.Bytes.Length) return false;
                        _pendingBytes += bytes.Length - pending.Bytes.Length;
                        pending.Bytes = (byte[])bytes.Clone();
                        CoalescedMessages++;
                        return true;
                    }
            if (_outgoing.Count >= 8 || bytes.Length > BoundedPacketAssembler.MaximumPendingBytes - _pendingBytes) return false;
            byte[] id = Guid.NewGuid().ToByteArray();
            _outgoing.Enqueue(new Outgoing { Target = target, Low = BitConverter.ToUInt64(id, 0),
                High = BitConverter.ToUInt64(id, 8), Bytes = (byte[])bytes.Clone() });
            _pendingBytes += bytes.Length;
            return true;
        }
        /// <summary>소유한 Unity 조립부의 Update에서 호출합니다. 게임시간 정지 중에도 네트워크는 진행합니다.</summary>
        public void Pump(int packetBudget = 8)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(NgoMessageChannel));
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
                _registeredMessaging.SendNamedMessage(_channelName, job.Target, writer, NetworkDelivery.ReliableSequenced);
                job.Offset += bytes;
                if (job.Offset == job.Bytes.Length) RemoveOutgoing();
            }
        }
        void INetworkMessagePump.Pump() => Pump();
        private void RemoveOutgoing() { _pendingBytes -= _outgoing.Dequeue().Bytes.Length; }
        private bool IsPeer(ulong id)
        {
            if (_manager == null || !_manager.IsListening || _manager.ShutdownInProgress || _registeredMessaging == null ||
                !ReferenceEquals(_registeredMessaging, _manager.CustomMessagingManager) || id == _manager.LocalClientId) return false;
            if (!_manager.IsServer) return id == NetworkManager.ServerClientId && _manager.IsConnectedClient;
            return _manager.ConnectedClients.ContainsKey(id);
        }
        private void OnMessage(ulong sender, FastBufferReader reader)
        {
            // NGO는 NamedMessage hash를 읽은 reader를 넘기므로 전체 Length가 아니라 남은 payload를 검사합니다.
            int available = reader.Length - reader.Position;
            if (_disposed || !IsPeer(sender) || available < 28 || available > 28 + BoundedPacketAssembler.ChunkBytes)
            { RejectedPackets++; return; }
            byte[] message;
            try
            {
                reader.ReadValueSafe(out int magic); reader.ReadValueSafe(out ulong low); reader.ReadValueSafe(out ulong high);
                reader.ReadValueSafe(out int total); reader.ReadValueSafe(out int offset);
                if (magic != PacketMagic || (_manager.IsServer && total > _maximumClientMessageBytes)) { RejectedPackets++; return; }
                byte[] id = new byte[16]; Array.Copy(BitConverter.GetBytes(low), 0, id, 0, 8); Array.Copy(BitConverter.GetBytes(high), 0, id, 8, 8);
                byte[] chunk = new byte[reader.Length - reader.Position]; reader.ReadBytesSafe(ref chunk, chunk.Length);
                PacketAssemblyStatus state = _assembler.Add(sender, new Guid(id), total, offset, chunk, Time.realtimeSinceStartupAsDouble, out message);
                if (state == PacketAssemblyStatus.Rejected) { RejectedPackets++; return; }
                if (state != PacketAssemblyStatus.Complete) return;
            }
            catch (Exception error) when (error is FormatException || error is OverflowException || error is ArgumentException)
            { RejectedPackets++; return; }
            // 소비자 오류를 패킷 오류로 오인하지 않습니다.
            MessageReceived?.Invoke(sender, message);
        }
        private void OnPeerDisconnected(ulong id) => _assembler.RemovePeer(id);
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            if (_manager != null)
            {
                _manager.OnClientDisconnectCallback -= OnPeerDisconnected;
                _manager.OnServerStarted -= RegisterMessaging;
                _manager.OnClientStarted -= RegisterMessaging;
                _manager.OnServerStopped -= OnStopped;
                _manager.OnClientStopped -= OnStopped;
            }
            _registeredMessaging?.UnregisterNamedMessageHandler(_channelName);
            _registeredMessaging = null;
            _outgoing.Clear(); _pendingBytes = 0; _assembler.Dispose();
            MessageReceived = null;
        }
    }
}
