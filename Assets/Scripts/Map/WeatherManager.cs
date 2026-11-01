// 날씨/예고 원본과 표현을 유지하고 Core Match의 Host 진행 또는 Client 읽기 lease를 받습니다.
// 정식 Match에서는 독립 Client Random/타이머를 막으며 standalone Weather Scene은 기존 동작을 유지합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using Map;
using UnityEngine;
using UnityEngine.UI;

/*
 * WeatherManager는 날씨를 정의하고 변경하는 역할만 가지는게 옳다고 생각합니다.
 * 그래서 Tick 세는 걸 분리했습니다.
 */
namespace Map
{
    [RequireComponent(typeof(WeatherChangeEvent))]
    public partial class WeatherManager : MonoBehaviour, IMatchWeatherSource, IMatchScheduledEnvironmentFeature
    {
        public enum Weather
        {
            SUNNY,
            RAIN,
            THUNDERSTORM,
            WEATHER_MAX = THUNDERSTORM // Enum의 맨 마지막을 나타내는 열거형 변수
            // WEATHER_MAX - SUNNY 로 날씨가 더 늘어나도 개수를 알 수 있음
        }
        
        [Header("===WeatherManagement===")]
        // 날씨 정보
        [SerializeField] private Weather currentWeather = Weather.SUNNY;
        public Weather CurrentWeather =>  currentWeather;
        [SerializeField, Min(1)] private int weatherQueueSize = 5;
        [SerializeField, Min(1)] private int sunnyWeight = 3;
        private Queue<Weather> weatherQueue;
        private bool _requiresMatch;
        private WeatherScope _scope;
        private Unity.Mathematics.Random _matchRandom;
        private Weather _authoredWeather;
        private double _standaloneSeconds, _nextWeatherAt = double.NaN;
        public event Action Changed;
        public DefinitionId WeatherId => Id(currentWeather);
        public IReadOnlyList<DefinitionId> Forecast
        {
            get
            {
                var copy = new List<DefinitionId>();
                if (weatherQueue != null) foreach (Weather value in weatherQueue) copy.Add(Id(value));
                return copy.AsReadOnly();
            }
        }
        private bool MayAdvance => isActiveAndEnabled && (!_requiresMatch ||
            (_scope != null && _scope.Authority && !_scope.Gameplay.IsCancellationRequested && _scope.CanWrite()));
    
        [Header("===Weather VFX===")]
        // 각 날씨에서 사용할 Particle System => 미정
        [SerializeField] private ParticleSystem rainVFX;
        [SerializeField] private ParticleSystem sunVFX;
        [SerializeField] private ParticleSystem thunderVFX;
        
        [Header("===Weather temporary Canvas ===")]
        [SerializeField] private Image tmpweatherImage;
        [SerializeField] private Color _sunnyColor;
        [SerializeField] private Color _rainColor;
        [SerializeField] private Color _thunderColor;
    
        // 날씨 전환 시 일어날 이벤트 
        public static Action<Weather,Queue<Weather>> OnWeatherChange;
        
        private WeatherChangeEvent _weatherChangeEvent;

        [SerializeField] private ShaderTests _shaderTests;
        
        void OnEnable()
        {
            OnWeatherChange += WeatherCanvasColorChange;
        }

        void OnDisable()
        {
            OnWeatherChange -= WeatherCanvasColorChange;
        }

        void Awake()
        {
            _authoredWeather = currentWeather;
            _weatherChangeEvent = GetComponent<WeatherChangeEvent>();
            if (_shaderTests == null || !_shaderTests.CompareTag("MainCamera"))
            {
                Camera mainCamera = Camera.main;
                _shaderTests = mainCamera != null
                    ? mainCamera.GetComponent<ShaderTests>()
                    : null;
            }
        }
        void Start()
        {
            if (MayAdvance) FillWeather();
            WeatherCanvasColorChange(currentWeather, weatherQueue);

            if (rainVFX != null && sunVFX!=null && thunderVFX != null)
            {
                rainVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                thunderVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                sunVFX.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        private void FillWeather()
        {
            if (weatherQueue == null)
            {
                weatherQueue = new Queue<Weather>();
            }

            while (weatherQueue.Count < Mathf.Max(1, weatherQueueSize))
            {
                weatherQueue.Enqueue(GetWeather());
            }
        }

        public void ChangeWeather()
        {
            if (!MayAdvance) return;
            if (weatherQueue == null || weatherQueue.Count == 0)
            {
                FillWeather();
            }

            currentWeather = weatherQueue.Dequeue();
            Debug.Log($"현재 날씨: {currentWeather}, 뽑힌 후 큐: [{string.Join(", ", weatherQueue)}]");
            FillWeather();
            NotifyWeather();
        }

        private Weather GetWeather()
        {
            // SUNNY는 sunnyWeight칸, 다른 날씨는 각각 한 칸을 차지한다.
            int maximum = sunnyWeight + (int)Weather.WEATHER_MAX;
            int roll = _matchRandom.state != 0 ? _matchRandom.NextInt(maximum) : UnityEngine.Random.Range(0, maximum);
            Weather result = roll < sunnyWeight
                ? Weather.SUNNY
                : (Weather)(roll - sunnyWeight + 1);

            return result;
        }
        
        public void RequireMatchScope()
        {
            if (weatherQueueSize < 1 || sunnyWeight < 1 || sunnyWeight > int.MaxValue - (int)Weather.WEATHER_MAX ||
                weatherQueueSize > MatchEnvironmentSnapshot.MaximumForecast)
                throw new InvalidOperationException("Weather forecast exceeds the Match environment contract limit.");
            _requiresMatch = true;
        }

        public IDisposable BindAuthority(MatchId match, IMatchClock clock, int seed, Func<bool> canWrite, CancellationToken gameplay)
        {
            RequireMatchScope();
            if (clock == null || canWrite == null) throw new ArgumentNullException(clock == null ? nameof(clock) : nameof(canWrite));
            if (!canWrite() || gameplay.IsCancellationRequested) throw new InvalidOperationException("An active Host weather scope is required.");
            var scope = BeginScope(match, true, clock, canWrite, gameplay);
            try
            {
                currentWeather = _authoredWeather; weatherQueue = new Queue<Weather>();
                // 상태를 값으로 저장할 수 있는 Unity Mathematics RNG를 Match 전용으로 사용합니다.
                uint randomSeed = unchecked((uint)seed);
                _matchRandom = new Unity.Mathematics.Random(randomSeed == 0 ? 1u : randomSeed);
                _nextWeatherAt = double.NaN;
                FillWeather(); NotifyWeather(); return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        /// <summary>Core 조립부가 공용 예약을 제공하면 CountTick의 제작 간격을 해당 수명에 연결합니다.</summary>
        /// <returns>날씨 원본/seed와 예약을 함께 해제하는 Match lease입니다.</returns>
        public IDisposable BindAuthority(MatchId match, IMatchClock clock, IMatchScheduler scheduler,
            int seed, Func<bool> canWrite, CancellationToken gameplay)
        {
            if (scheduler == null) throw new ArgumentNullException(nameof(scheduler));
            IDisposable scope = BindAuthority(match, clock, seed, canWrite, gameplay);
            try
            {
                if (!ReferenceEquals(_scope, scope)) throw new InvalidOperationException("Weather scope changed during initialization.");
                CountTick tick = GetComponent<CountTick>();
                if (tick == null) throw new InvalidOperationException("Scheduled Weather requires its authored CountTick interval component.");
                _scope.TimerLease = tick.BindScheduler(clock, scheduler, OnScheduledWeather, gameplay);
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        private void OnScheduledWeather(MatchTimerTick tick)
        { if (MayAdvance) ChangeWeather(); }

        public IDisposable BindReplica(MatchId match)
        {
            RequireMatchScope();
            var scope = BeginScope(match, false, null, () => false, default);
            weatherQueue = new Queue<Weather>(); _matchRandom = default;
            return scope;
        }

        public void ApplyReplica(MatchEnvironmentSnapshot snapshot)
        {
            if (_scope == null || _scope.Authority || snapshot == null || snapshot.MatchId != _scope.Match || !snapshot.HasWeather) return;
            Weather weather = Parse(snapshot.WeatherId);
            var forecast = new Queue<Weather>();
            foreach (DefinitionId value in snapshot.Forecast) forecast.Enqueue(Parse(value));
            currentWeather = weather; weatherQueue = forecast; NotifyWeather();
        }

        // CountTick은 clock/진행 권한을 선택하지 않고 이 Feature의 현재 lease에 요청합니다.
        public void TickWeather(float interval, float standaloneDelta)
        {
            if (float.IsNaN(interval) || float.IsInfinity(interval) || interval <= 0 ||
                float.IsNaN(standaloneDelta) || float.IsInfinity(standaloneDelta) || standaloneDelta < 0)
                throw new ArgumentOutOfRangeException(nameof(interval));
            if (!MayAdvance) return;
            if (_scope?.TimerLease != null) return;
            _standaloneSeconds += _requiresMatch ? 0 : Math.Max(0, standaloneDelta);
            double seconds = _requiresMatch ? _scope.Clock.ElapsedSeconds : _standaloneSeconds;
            if (double.IsNaN(_nextWeatherAt)) _nextWeatherAt = seconds + Math.Max(.01f, interval);
            if (seconds < _nextWeatherAt) return;
            _nextWeatherAt = seconds + Math.Max(.01f, interval);
            ChangeWeather();
        }

        private WeatherScope BeginScope(MatchId match, bool authority, IMatchClock clock, Func<bool> canWrite, CancellationToken gameplay)
        {
            if (match.IsEmpty || _scope != null) throw new InvalidOperationException("Bind one valid Match weather scope.");
            _scope = new WeatherScope(this, match, authority, clock, canWrite, gameplay); return _scope;
        }

        private void NotifyWeather()
        {
            // 기존 표현 구독자에게도 원본 Queue 대신 복사본을 전달합니다.
            var errors = new List<Exception>();
            if (Changed != null) foreach (Action observer in Changed.GetInvocationList())
                try { observer(); } catch (Exception error) { errors.Add(error); }
            if (OnWeatherChange != null) foreach (Action<Weather, Queue<Weather>> observer in OnWeatherChange.GetInvocationList())
                try { observer(currentWeather, new Queue<Weather>(weatherQueue ?? new Queue<Weather>())); }
                catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException("Weather changed; observers failed.", errors);
        }
        private static DefinitionId Id(Weather value) => new DefinitionId(value == Weather.SUNNY ? "sunny" : value == Weather.RAIN ? "rain" : "thunderstorm");
        private static Weather Parse(DefinitionId value) => value.Value switch
        {
            "sunny" => Weather.SUNNY, "rain" => Weather.RAIN, "thunderstorm" => Weather.THUNDERSTORM,
            _ => throw new ArgumentException("Unknown World weather ID: " + value.Value)
        };
        private sealed class WeatherScope : IDisposable
        {
            private WeatherManager _owner;
            public readonly MatchId Match;
            public readonly bool Authority;
            public readonly IMatchClock Clock;
            public readonly Func<bool> CanWrite;
            public readonly CancellationToken Gameplay;
            public IDisposable TimerLease;
            public WeatherScope(WeatherManager owner, MatchId match, bool authority, IMatchClock clock, Func<bool> canWrite, CancellationToken gameplay)
            { _owner = owner; Match = match; Authority = authority; Clock = clock; CanWrite = canWrite; Gameplay = gameplay; }
            public void Dispose()
            {
                var owner = _owner; _owner = null;
                if (owner != null && ReferenceEquals(owner._scope, this))
                { owner._scope = null; owner._matchRandom = default; owner._nextWeatherAt = double.NaN; }
                TimerLease?.Dispose(); TimerLease = null;
            }
        }

        // 날씨 변경 시 호출될 메소드 
        void WeatherCanvasColorChange(Weather curWeather, Queue<Weather> q)
        {
            /*
             * 날씨마다 로직을 여기서 변경 실행이 낫지 않을까
             */
            if (tmpweatherImage == null)
            {
                return;
            }
            switch (curWeather)
            {
                case Weather.SUNNY:
                    tmpweatherImage.color = _sunnyColor;
                    break;
                case Weather.RAIN:
                    tmpweatherImage.color = _rainColor;
                    break;
                case Weather.THUNDERSTORM:
                    tmpweatherImage.color = _thunderColor;
                    break;
                default:
                    tmpweatherImage.color = _sunnyColor;
                    break;
            }
        }
        public void CallWeatherChange()
        {
            if (_shaderTests == null)
            {
                Debug.LogError("Main Camera에 ShaderTests 컴포넌트가 필요합니다.", this);
                return;
            }

            _shaderTests.ChangeWeatherFlag();
        }
    }
}
