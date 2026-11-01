// Weather Feature의 제작 간격을 공용 scheduler에 등록합니다.
// Match 예약이 없을 때만 기존 독립 Scene 진행을 사용하고 두 경로를 동시에 실행하지 않습니다.
using System;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace Map
{
    public class CountTick : MonoBehaviour
    {
        [SerializeField, Min(0.01f)] private float weatherChangeTime = 5f;
        
        private WeatherManager _weatherManager;
        private TimerBinding _timerBinding;
        public bool UsesMatchScheduler => _timerBinding != null;
        void Awake()
        {
            _weatherManager = GetComponent<WeatherManager>();
        }
        void Update()
        {
            if (_timerBinding != null) { _timerBinding.RefreshInterval(); return; }
            if (_weatherManager != null) _weatherManager.TickWeather(weatherChangeTime, Time.deltaTime);
        }
        /// <summary>WeatherManager의 Host 바인딩이 제작 간격의 예약을 연결합니다.</summary>
        /// <returns>Weather scope가 해제할 lease입니다. 실제 날씨 전이 규칙은 WeatherManager가 소유합니다.</returns>
        public IDisposable BindScheduler(IMatchScheduler scheduler, Action<MatchTimerTick> advance, CancellationToken gameplay)
        {
            if (_timerBinding != null) throw new InvalidOperationException("Weather timers are already bound.");
            var binding = new TimerBinding(this, scheduler, advance, gameplay);
            try { binding.RefreshInterval(); _timerBinding = binding; return binding; }
            catch { binding.Dispose(); throw; }
        }
        private void OnDestroy() => _timerBinding?.Dispose();

        private sealed class TimerBinding : IDisposable
        {
            private CountTick _owner;
            private readonly IMatchScheduler _scheduler;
            private readonly Action<MatchTimerTick> _advance;
            private readonly CancellationToken _gameplay;
            private IDisposable _timer;
            private float _interval;
            public TimerBinding(CountTick owner, IMatchScheduler scheduler, Action<MatchTimerTick> advance, CancellationToken gameplay)
            {
                _owner = owner; _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
                _advance = advance ?? throw new ArgumentNullException(nameof(advance)); _gameplay = gameplay;
            }
            public void RefreshInterval()
            {
                if (_owner == null || _gameplay.IsCancellationRequested) return;
                float interval = _owner.weatherChangeTime;
                if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0)
                    throw new InvalidOperationException("Weather interval must be a positive finite game duration.");
                if (_timer != null && interval == _interval) return;
                _timer?.Dispose(); _timer = null;
                _timer = _scheduler.ScheduleRepeating(interval, Advance, _gameplay); _interval = interval;
            }
            private void Advance(MatchTimerTick tick)
            { if (_owner != null && _owner.isActiveAndEnabled && !_gameplay.IsCancellationRequested) _advance(tick); }
            public void Dispose()
            {
                var owner = _owner; _owner = null;
                if (owner != null && ReferenceEquals(owner._timerBinding, this)) owner._timerBinding = null;
                _timer?.Dispose(); _timer = null;
            }
        }
    }
}
