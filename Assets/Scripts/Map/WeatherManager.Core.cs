// 실제 날씨 원본을 Core의 환경 snapshot과 Host/Client 수명에 연결합니다.
// 날씨 선택 규칙·Random 교체·Weather 저장 writer는 담당자의 구현에 남깁니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace Map
{
    public partial class WeatherManager : IMatchWeatherSource, IMatchScheduledEnvironmentFeature
    {
        private bool _coreRequiresMatch;
        private CoreScope _coreScope;
        public event Action Changed;
        public DefinitionId WeatherId => ToCoreId(currentWeather);
        public IReadOnlyList<DefinitionId> Forecast
        {
            get
            {
                var copy = new List<DefinitionId>();
                if (weatherQueue != null) foreach (var value in weatherQueue) copy.Add(ToCoreId(value));
                return copy.AsReadOnly();
            }
        }
        internal bool MayChangeCoreWeather => _coreScope != null
            ? _coreScope.Authority && !_coreScope.Gameplay.IsCancellationRequested && _coreScope.MayWrite()
            : !_coreRequiresMatch;

        public void RequireMatchScope()
        {
            _coreRequiresMatch = true;
            GetComponent<CountTick>()?.RequireMatchScope();
        }

        public IDisposable BindAuthority(MatchId match, IMatchClock clock, int seed, Func<bool> canWrite, CancellationToken gameplay)
            => BindAuthority(match, clock, null, seed, canWrite, gameplay);

        /// <summary>Core Scene 조립부가 난수 주입 뒤 연결합니다. 낮밤 신호가 있으면 이를 쓰고, 독립 Scene의 간격 예약만 CountTick에 맡깁니다.</summary>
        public IDisposable BindAuthority(MatchId match, IMatchClock clock, IMatchScheduler scheduler,
            int seed, Func<bool> canWrite, CancellationToken gameplay)
        {
            if (clock == null || canWrite == null) throw new ArgumentNullException(clock == null ? nameof(clock) : nameof(canWrite));
            var scope = BeginCoreScope(match, true, canWrite, gameplay);
            try
            {
                FillWeather();
                var dayNight = GetComponent<IMatchDayNightCycle>();
                if (dayNight != null)
                {
                    // 낮밤 주기가 있으면 독립 CountTick 예약과 중복 실행하지 않습니다.
                    dayNight.PhaseAdvanced += ChangeWeather;
                    scope.Timer = new DayNightSubscription(dayNight, ChangeWeather);
                }
                else scope.Timer = GetComponent<CountTick>()?.BindScheduler(clock, scheduler, ChangeWeather, gameplay);
                return scope;
            }
            catch { scope.Dispose(); throw; }
        }

        public IDisposable BindReplica(MatchId match) => BeginCoreScope(match, false, () => false, default);

        /// <summary>Client의 확정 환경을 표현에만 적용합니다. Queue 난수 생성이나 새 Encounter 요청을 하지 않습니다.</summary>
        public void ApplyReplica(MatchEnvironmentSnapshot snapshot)
        {
            if (_coreScope == null || _coreScope.Authority || snapshot == null || snapshot.MatchId != _coreScope.Match || !snapshot.HasWeather) return;
            var selected = FromCoreId(snapshot.WeatherId);
            var forecast = new Queue<Weather>();
            foreach (var id in snapshot.Forecast) forecast.Enqueue(FromCoreId(id));
            currentWeather = selected;
            weatherQueue = forecast;
            WeatherCanvasColorChange(selected, forecast);
            ApplyWeatherShader();
        }

        private CoreScope BeginCoreScope(MatchId match, bool authority, Func<bool> mayWrite, CancellationToken gameplay)
        {
            if (match.IsEmpty || _coreScope != null || gameplay.IsCancellationRequested)
                throw new InvalidOperationException("Bind Weather to one live Match.");
            _coreRequiresMatch = true;
            _coreScope = new CoreScope(this, match, authority, mayWrite, gameplay);
            GetComponent<CountTick>()?.RequireMatchScope();
            return _coreScope;
        }

        private void PublishCoreWeather()
        {
            if (Changed == null) return;
            foreach (Action observer in Changed.GetInvocationList())
                try { observer(); }
                catch (Exception error) { UnityEngine.Debug.LogException(error, this); }
        }

        private static DefinitionId ToCoreId(Weather value) => new(value switch
        {
            Weather.SUNNY => "sunny",
            Weather.RAIN => "rain",
            Weather.THUNDERSTORM => "thunderstorm",
            Weather.HEATWAVE => "heatwave",
            _ => throw new ArgumentOutOfRangeException(nameof(value))
        });
        private static Weather FromCoreId(DefinitionId id) => id.Value switch
        {
            "sunny" => Weather.SUNNY,
            "rain" => Weather.RAIN,
            "thunderstorm" => Weather.THUNDERSTORM,
            "heatwave" => Weather.HEATWAVE,
            _ => throw new ArgumentException("Unknown Weather definition: " + id.Value)
        };

        private sealed class DayNightSubscription : IDisposable
        {
            private IMatchDayNightCycle _source;
            private readonly Action _advance;
            public DayNightSubscription(IMatchDayNightCycle source, Action advance) { _source = source; _advance = advance; }
            public void Dispose() { if (_source == null) return; _source.PhaseAdvanced -= _advance; _source = null; }
        }

        private sealed class CoreScope : IDisposable
        {
            private WeatherManager _owner;
            public readonly MatchId Match;
            public readonly bool Authority;
            public readonly Func<bool> MayWrite;
            public readonly CancellationToken Gameplay;
            public IDisposable Timer;
            public CoreScope(WeatherManager owner, MatchId match, bool authority, Func<bool> mayWrite, CancellationToken gameplay)
            { _owner = owner; Match = match; Authority = authority; MayWrite = mayWrite; Gameplay = gameplay; }
            public void Dispose()
            {
                var owner = _owner;
                _owner = null;
                Timer?.Dispose();
                Timer = null;
                if (owner != null && ReferenceEquals(owner._coreScope, this)) owner._coreScope = null;
            }
        }
    }
}
