// Match 게임 시각의 deadline으로 단발/반복 예약을 실행합니다.
// 주 스레드 소유이며 callback 재진입·밀린 반복·실행량 상한을 제어합니다.
// lease/토큰 취소와 epoch 변경은 과거 예약을 폐기하고 외부 Feature는 상태에 맞게 재등록합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchScheduler : IMatchScheduler, IDisposable
    {
        private sealed class Entry : IDisposable
        {
            public long Id;
            public double Deadline, Interval;
            public Action<MatchTimerTick> Callback;
            public CancellationTokenRegistration Registration;
            private int _cancelled;
            public bool IsCancelled => Volatile.Read(ref _cancelled) != 0;
            public void Cancel() => Interlocked.Exchange(ref _cancelled, 1);
            public void Dispose() { Cancel(); Registration.Dispose(); }
        }
        private sealed class EntryOrder : IComparer<Entry>
        {
            public int Compare(Entry a, Entry b)
            { int byTime = a.Deadline.CompareTo(b.Deadline); return byTime != 0 ? byTime : a.Id.CompareTo(b.Id); }
        }
        private readonly IMatchTime _clock;
        private readonly SortedSet<Entry> _queue = new SortedSet<Entry>(new EntryOrder());
        private readonly HashSet<Entry> _entries = new HashSet<Entry>();
        private readonly List<Entry> _work = new List<Entry>();
        private readonly List<Entry> _cancelledEntries = new List<Entry>();
        private readonly int _ownerThread, _capacity, _callbacksPerPump;
        private readonly CancellationToken _lifetime;
        private Guid _epoch;
        private long _sequence;
        private bool _pumping, _disposed;
        public int PendingCount { get { CheckOwner(); Prune(); return _entries.Count; } }

        public MatchScheduler(IMatchTime clock, int capacity = 4096, int callbacksPerPump = 64, CancellationToken lifetime = default)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            if (capacity <= 0) throw new ArgumentOutOfRangeException(nameof(capacity));
            if (callbacksPerPump <= 0) throw new ArgumentOutOfRangeException(nameof(callbacksPerPump));
            _capacity = capacity; _callbacksPerPump = callbacksPerPump;
            _lifetime = lifetime;
            _ownerThread = Thread.CurrentThread.ManagedThreadId; _epoch = clock.Snapshot.Epoch;
        }

        /// <inheritdoc />
        public IDisposable Schedule(double delaySeconds, Action<MatchTimerTick> callback, CancellationToken cancellation = default)
            => Add(delaySeconds, 0, callback, cancellation);
        /// <inheritdoc />
        public IDisposable ScheduleRepeating(double intervalSeconds, Action<MatchTimerTick> callback, CancellationToken cancellation = default)
            => ScheduleRepeating(intervalSeconds, intervalSeconds, callback, cancellation);
        /// <inheritdoc />
        public IDisposable ScheduleRepeating(double firstDelaySeconds, double intervalSeconds,
            Action<MatchTimerTick> callback, CancellationToken cancellation = default)
        {
            if (double.IsNaN(intervalSeconds) || double.IsInfinity(intervalSeconds) || intervalSeconds <= 0)
                throw new ArgumentOutOfRangeException(nameof(intervalSeconds));
            return Add(firstDelaySeconds, intervalSeconds, callback, cancellation);
        }
        private IDisposable Add(double delay, double interval, Action<MatchTimerTick> callback, CancellationToken cancellation)
        {
            CheckOwner();
            _lifetime.ThrowIfCancellationRequested();
            if (callback == null) throw new ArgumentNullException(nameof(callback));
            if (double.IsNaN(delay) || double.IsInfinity(delay) || delay < 0) throw new ArgumentOutOfRangeException(nameof(delay));
            cancellation.ThrowIfCancellationRequested(); Prune();
            if (_entries.Count >= _capacity) throw new InvalidOperationException("The Match timer capacity is full.");
            double now = _clock.ElapsedSeconds, deadline = now + delay;
            if (double.IsInfinity(deadline) || (delay > 0 && deadline <= now))
                throw new ArgumentOutOfRangeException(nameof(delay), "The deadline cannot be represented.");
            var entry = new Entry { Id = checked(++_sequence), Deadline = deadline, Interval = interval, Callback = callback };
            if (cancellation.CanBeCanceled) entry.Registration = cancellation.Register(state => ((Entry)state).Cancel(), entry);
            _entries.Add(entry); _queue.Add(entry);
            return entry;
        }

        /// <summary>Match 소유자가 시계 Pump 다음에 호출해 기한이 된 예약을 실행합니다.</summary>
        /// <remarks>한 번의 상한을 넘는 예약은 다음 Pump로 넘깁니다. callback에서 추가한 작업도 다음 Pump부터 실행합니다.</remarks>
        public void Pump()
        {
            CheckOwner();
            if (_pumping) throw new InvalidOperationException("A Match scheduler cannot pump recursively.");
            if (_lifetime.IsCancellationRequested) { CancelAll(); return; }
            Prune();
            if (!_clock.IsAvailable || _clock.Snapshot.IsPaused || _clock.Snapshot.Rate == 0) return;
            double now = _clock.ElapsedSeconds;
            _pumping = true;
            List<Exception> errors = null;
            try
            {
                _work.Clear();
                while (_queue.Count > 0 && _work.Count < _callbacksPerPump && _queue.Min.Deadline <= now)
                { Entry entry = _queue.Min; _queue.Remove(entry); _work.Add(entry); }
                foreach (Entry entry in _work)
                {
                    if (_disposed) break;
                    if (_lifetime.IsCancellationRequested) { CancelAll(); break; }
                    if (_clock.Snapshot.Epoch != _epoch) { CancelAll(); _epoch = _clock.Snapshot.Epoch; break; }
                    if (!_clock.IsAvailable || _clock.Snapshot.IsPaused || _clock.Snapshot.Rate == 0) break;
                    if (entry.IsCancelled) { Remove(entry); continue; }
                    double scheduled = entry.Deadline;
                    double periods = entry.Interval > 0 ? Math.Floor((now - scheduled) / entry.Interval) + 1 : 1;
                    long missed = periods >= long.MaxValue ? long.MaxValue : (long)periods - 1;
                    bool repeats = entry.Interval > 0;
                    if (repeats)
                    {
                        entry.Deadline = scheduled + periods * entry.Interval;
                        if (double.IsInfinity(entry.Deadline) || entry.Deadline <= now)
                        { Remove(entry); (errors ??= new List<Exception>()).Add(new InvalidOperationException("The next timer deadline cannot be represented.")); repeats = false; }
                    }
                    else Remove(entry);
                    try { entry.Callback(new MatchTimerTick(scheduled, now, missed)); }
                    catch (Exception error) { (errors ??= new List<Exception>()).Add(error); }
                    if (repeats && !entry.IsCancelled && !_disposed) _queue.Add(entry);
                    else if (repeats) Remove(entry);
                }
            }
            finally
            {
                // callback에서 정지해도 아직 실행하지 않은 작업의 예약을 잃지 않습니다.
                if (!_disposed)
                    foreach (Entry entry in _work)
                        if (_entries.Contains(entry) && !entry.IsCancelled) _queue.Add(entry);
                _work.Clear(); _pumping = false;
            }
            if (errors != null) throw new AggregateException("Match timer callbacks failed; other due work was processed.", errors);
        }

        private void Prune()
        {
            if (_clock.Snapshot.Epoch != _epoch) { CancelAll(); _epoch = _clock.Snapshot.Epoch; return; }
            _cancelledEntries.Clear();
            foreach (Entry entry in _entries) if (entry.IsCancelled) _cancelledEntries.Add(entry);
            foreach (Entry entry in _cancelledEntries) Remove(entry);
            _cancelledEntries.Clear();
        }
        private void Remove(Entry entry) { _queue.Remove(entry); _entries.Remove(entry); entry.Dispose(); }
        private void CancelAll()
        { foreach (Entry entry in _entries) entry.Dispose(); _entries.Clear(); _queue.Clear(); }
        /// <summary>Match 소유자가 완료·중단 시 모든 예약을 취소합니다. callback 중 호출해도 뒤의 작업을 차단합니다.</summary>
        public void CancelPending() { CheckOwner(); CancelAll(); }
        public void Dispose()
        {
            if (_disposed) return;
            CheckOwner(); _disposed = true; CancelAll();
        }
        private void CheckOwner()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchScheduler));
            if (_ownerThread != Thread.CurrentThread.ManagedThreadId)
                throw new InvalidOperationException("Use the Match scheduler on its owning thread; cancel through the lease/token from other threads.");
        }
    }
}
