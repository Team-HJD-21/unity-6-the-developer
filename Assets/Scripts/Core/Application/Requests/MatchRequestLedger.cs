// Match 범위에서 요청의 진행·확정 결과를 보관해 재전송 중복 실행을 막습니다.
// 권한 검증은 호출자 책임이며 기록을 임의 퇴출해 이미 실행된 요청을 재실행하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchRequestLedger<TResult> : IDisposable
    {
        private sealed class Entry
        {
            public PlayerId Issuer;
            public string PayloadKey;
            public bool IsCompleted;
            public TResult Result;
        }
        private readonly Dictionary<Guid, Entry> _entries = new Dictionary<Guid, Entry>();
        private readonly int _capacity;
        private bool _isDisposed;
        public MatchId MatchId { get; }
        public int Count { get { ThrowIfDisposed(); return _entries.Count; } }

        public MatchRequestLedger(MatchId matchId, int capacity = 4096)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A match id is required.", nameof(matchId));
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            MatchId = matchId;
            _capacity = capacity;
        }

        public MatchRequestRegistration TryBegin(MatchRequestContext context, string payloadKey, out TResult cachedResult)
        {
            ThrowIfDisposed();
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (string.IsNullOrWhiteSpace(payloadKey)) throw new ArgumentException("A canonical operation/payload key is required.", nameof(payloadKey));
            cachedResult = default;
            if (context.MatchId != MatchId) return MatchRequestRegistration.DifferentMatch;
            if (_entries.TryGetValue(context.RequestId, out Entry entry))
            {
                if (entry.Issuer != context.IssuerId || !StringComparer.Ordinal.Equals(entry.PayloadKey, payloadKey))
                    return MatchRequestRegistration.Conflict;
                if (!entry.IsCompleted) return MatchRequestRegistration.InProgress;
                cachedResult = entry.Result;
                return MatchRequestRegistration.Completed;
            }
            if (_entries.Count >= _capacity) return MatchRequestRegistration.CapacityExceeded;
            _entries.Add(context.RequestId, new Entry { Issuer = context.IssuerId, PayloadKey = payloadKey });
            return MatchRequestRegistration.Started;
        }

        /// <summary>부분 실행/실패도 결과로 확정해야 같은 요청이 다시 실행되지 않습니다.</summary>
        public void Complete(Guid requestId, TResult result)
        {
            ThrowIfDisposed();
            if (!_entries.TryGetValue(requestId, out Entry entry))
                throw new InvalidOperationException("The request has not been registered.");
            if (entry.IsCompleted) throw new InvalidOperationException("The request is already complete.");
            entry.Result = result;
            entry.IsCompleted = true;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            _entries.Clear();
        }

        private void ThrowIfDisposed()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(MatchRequestLedger<TResult>));
        }
    }
}
