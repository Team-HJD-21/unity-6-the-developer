// 로컬 입력 잠금을 개별 lease로 보유하고 마지막 소유자가 해제될 때만 풉니다.
// App 종료 뒤의 늦은 해제는 무해하며 새 잠금 획득은 거부합니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Application
{
    public sealed class InputLockService : IInputBlocker, IDisposable
    {
        private readonly Dictionary<long, string> _owners = new Dictionary<long, string>();
        private long _sequence;
        private bool _disposed;
        public bool IsBlocked => _disposed || _owners.Count > 0;
        public int Count => _owners.Count;
        public IDisposable Acquire(string owner)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(InputLockService));
            if (string.IsNullOrWhiteSpace(owner)) throw new ArgumentException("An input lock owner is required.", nameof(owner));
            long id = checked(++_sequence); _owners.Add(id, owner);
            return new Lease(this, id);
        }
        public void Dispose() { if (_disposed) return; _disposed = true; _owners.Clear(); }
        private sealed class Lease : IDisposable
        {
            private InputLockService _owner;
            private readonly long _id;
            public Lease(InputLockService owner, long id) { _owner = owner; _id = id; }
            public void Dispose() { var owner = _owner; _owner = null; owner?._owners.Remove(_id); }
        }
    }
}
