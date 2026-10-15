// 분할된 큰 설정 메시지를 연결·전송 ID별로 제한된 메모리에 재조립합니다.
// 중복/순서 변경을 허용하지만 상충 조각·무진행·과도한 전송은 거부합니다.
using System;
using System.Collections.Generic;
namespace TeamHJD.Game.Infrastructure
{
    public sealed class BoundedPacketAssembler : IDisposable
    {
        public const int ChunkBytes = 1000;
        public const int MaximumPendingBytes = 8 * 1024 * 1024;
        private sealed class Buffer
        {
            public byte[] Bytes; public bool[] Received; public int Count;
            public double Created, Progress;
        }
        private readonly Dictionary<(ulong, Guid), Buffer> _pending = new Dictionary<(ulong, Guid), Buffer>();
        private bool _disposed;
        public int PendingBytes { get; private set; }
        public int PendingTransfers => _pending.Count;
        public PacketAssemblyStatus Add(ulong sender, Guid transferId, int total, int offset, byte[] chunk, double now, out byte[] complete)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BoundedPacketAssembler));
            if (double.IsNaN(now) || double.IsInfinity(now)) throw new ArgumentOutOfRangeException(nameof(now));
            complete = null; Prune(now);
            if (transferId == Guid.Empty || total < 1 || total > NetworkMessageLimits.MaximumMessageBytes ||
                offset < 0 || offset >= total || offset % ChunkBytes != 0 || chunk == null ||
                chunk.Length != Math.Min(ChunkBytes, total - offset)) return PacketAssemblyStatus.Rejected;
            var key = (sender, transferId);
            if (!_pending.TryGetValue(key, out Buffer buffer))
            {
                if (_pending.Count >= 4 || total > MaximumPendingBytes - PendingBytes) return PacketAssemblyStatus.Rejected;
                buffer = new Buffer { Bytes = new byte[total], Received = new bool[(total + ChunkBytes - 1) / ChunkBytes], Created = now, Progress = now };
                _pending.Add(key, buffer); PendingBytes += total;
            }
            if (buffer.Bytes.Length != total) { Remove(key); return PacketAssemblyStatus.Rejected; }
            int index = offset / ChunkBytes;
            if (buffer.Received[index])
            {
                for (int i = 0; i < chunk.Length; i++)
                    if (buffer.Bytes[offset + i] != chunk[i]) { Remove(key); return PacketAssemblyStatus.Rejected; }
                return PacketAssemblyStatus.Incomplete;
            }
            Array.Copy(chunk, 0, buffer.Bytes, offset, chunk.Length);
            buffer.Received[index] = true; buffer.Count++; buffer.Progress = now;
            if (buffer.Count < buffer.Received.Length) return PacketAssemblyStatus.Incomplete;
            complete = buffer.Bytes; Remove(key); return PacketAssemblyStatus.Complete;
        }
        public void Prune(double now)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BoundedPacketAssembler));
            var expired = new List<(ulong, Guid)>();
            foreach (var item in _pending)
                if (now - item.Value.Progress > 10 || now - item.Value.Created > 120) expired.Add(item.Key);
            foreach (var key in expired) Remove(key);
        }
        public void RemovePeer(ulong connectionId)
        {
            var keys = new List<(ulong, Guid)>();
            foreach (var key in _pending.Keys) if (key.Item1 == connectionId) keys.Add(key);
            foreach (var key in keys) Remove(key);
        }
        private void Remove((ulong, Guid) key)
        {
            if (_pending.TryGetValue(key, out Buffer buffer)) { PendingBytes -= buffer.Bytes.Length; _pending.Remove(key); }
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _pending.Clear(); PendingBytes = 0; }
    }
}
