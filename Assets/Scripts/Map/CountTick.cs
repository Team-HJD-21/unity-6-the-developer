// 날씨 변경 간격을 Core의 Match 시계/예약에 연결합니다.
// 날씨를 선택하지 않으며 독립 Scene에서는 Unity 절대 시각으로만 간격을 계산합니다.
// 저장 writer는 남은 대기를 읽고 복원할 수 있지만 자동 저장 시점은 정하지 않습니다.
using System;
using System.Threading;
using TeamHJD.Game.Contracts;
using UnityEngine;

namespace Map
{
    public sealed class CountTick : MonoBehaviour
    {
        [SerializeField, Min(.01f)] private float weatherChangeTime = 5f;
        private WeatherManager _weather;
        private IMatchClock _clock;
        private IMatchScheduler _scheduler;
        private IDisposable _timer;
        private Action _advance;
        private CancellationToken _gameplay;
        private bool _requiresMatch;
        private bool _bound;
        private float _interval;
        private double _nextAt = double.NaN;
        private int _generation;
        public bool UsesMatchScheduler => _bound && _scheduler != null;

        private void Awake() => _weather = GetComponent<WeatherManager>();
        public void RequireMatchScope() { _requiresMatch = true; _nextAt = double.NaN; }

        /// <summary>Weather의 Host scope가 한 번 호출합니다. 반환 lease를 해제하면 이전 예약도 취소됩니다.</summary>
        public IDisposable BindScheduler(IMatchClock clock, IMatchScheduler scheduler, Action advance,
            CancellationToken gameplay)
        {
            if (_bound) throw new InvalidOperationException("Weather timer already belongs to a Match.");
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _advance = advance ?? throw new ArgumentNullException(nameof(advance));
            _scheduler = scheduler;
            _gameplay = gameplay;
            _bound = true;
            int generation = ++_generation;
            try { RefreshInterval(); return new TimerLease(this, generation); }
            catch { Release(generation); throw; }
        }

        private void Update()
        {
            if (_gameplay.IsCancellationRequested || (_requiresMatch && !_bound)) return;
            RefreshInterval();
            if (_scheduler != null) return;
            if (_weather == null || !_weather.MayChangeCoreWeather) return;
            double now = _clock?.ElapsedSeconds ?? Time.timeAsDouble;
            if (now < _nextAt) return;
            _nextAt = now + _interval;
            if (_bound) _advance(); else _weather.ChangeWeather();
        }

        private void RefreshInterval()
        {
            float interval = weatherChangeTime;
            if (!float.IsFinite(interval) || interval <= 0)
                throw new InvalidOperationException("Weather interval must be a positive finite game duration.");
            if (!double.IsNaN(_nextAt) && interval == _interval) return;
            _interval = interval;
            Schedule(interval);
        }

        private void Schedule(double remaining)
        {
            _timer?.Dispose();
            _timer = null;
            _nextAt = (_clock?.ElapsedSeconds ?? Time.timeAsDouble) + remaining;
            if (_scheduler == null) return;
            _timer = _scheduler.ScheduleRepeating(remaining, _interval, tick =>
            {
                _nextAt = tick.ScheduledAt + (tick.MissedIntervals + 1d) * _interval;
                if (isActiveAndEnabled && !_gameplay.IsCancellationRequested) _advance();
            }, _gameplay);
        }

        /// <summary>진태의 Weather writer가 일관된 정지 프레임에서 호출합니다. Queue/날씨는 Weather가 별도로 저장합니다.</summary>
        public TimerState CaptureTimerState()
        {
            if (!_bound || _gameplay.IsCancellationRequested) throw new InvalidOperationException("Bind a live Host Weather timer first.");
            return new TimerState(_interval, Math.Max(0, Math.Min(_interval, _nextAt - _clock.ElapsedSeconds)));
        }

        /// <summary>Weather writer가 Core 시각 복원 뒤 호출합니다. 이전 예약을 바꾸며 즉시 날씨를 전환하지 않습니다.</summary>
        public void RestoreTimerState(TimerState state)
        {
            if (!_bound || _gameplay.IsCancellationRequested) throw new InvalidOperationException("Bind a live Host Weather timer first.");
            _ = new TimerState(state.Interval, state.Remaining);
            weatherChangeTime = _interval = state.Interval;
            Schedule(state.Remaining);
        }

        public readonly struct TimerState
        {
            public float Interval { get; }
            public double Remaining { get; }
            public TimerState(float interval, double remaining)
            {
                if (!float.IsFinite(interval) || interval <= 0 || !double.IsFinite(remaining) || remaining < 0 || remaining > interval)
                    throw new ArgumentOutOfRangeException(nameof(remaining));
                Interval = interval;
                Remaining = remaining;
            }
        }

        private void Release(int generation)
        {
            if (!_bound || generation != _generation) return;
            _timer?.Dispose();
            _timer = null;
            _clock = null;
            _scheduler = null;
            _advance = null;
            _gameplay = default;
            _bound = false;
            _nextAt = double.NaN;
        }

        private void OnDestroy() => Release(_generation);
        private sealed class TimerLease : IDisposable
        {
            private CountTick _owner;
            private readonly int _generation;
            public TimerLease(CountTick owner, int generation) { _owner = owner; _generation = generation; }
            public void Dispose() { var owner = _owner; _owner = null; if (owner != null) owner.Release(_generation); }
        }
    }
}
