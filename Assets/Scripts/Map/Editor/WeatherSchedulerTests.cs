// 실제 WeatherManager/CountTick의 공용 예약 연결과 중복 진행·취소를 확인합니다.
// 날씨 추첨·표현 규칙을 바꾸지 않고 Clock/Scheduler와 Feature lease의 접점을 검증합니다.
using System;
using System.Threading;
using Map;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class WeatherSchedulerTests
{
    private sealed class Source : ITimestampSource
    {
        public double Now;
        public bool TryReadSeconds(out double seconds) { seconds = Now; return true; }
    }
    private GameObject _object;
    private Source _source;
    private MatchClock _clock;
    private MatchScheduler _scheduler;
    private WeatherManager _weather;
    private CountTick _tick;
    private IDisposable _binding;
    private CancellationTokenSource _gameplay;
    private int _changes;

    [SetUp]
    public void CreateFeature()
    {
        _changes = 0;
        _source = new Source(); _clock = new MatchClock(new MatchId("weather-timer"), _source);
        _scheduler = new MatchScheduler(_clock); _gameplay = new CancellationTokenSource();
        _object = new GameObject("Scheduled Weather Test");
        _weather = _object.AddComponent<WeatherManager>(); _tick = _object.AddComponent<CountTick>();
        _binding = _weather.BindAuthority(new MatchId("weather-timer"), _clock, _scheduler, 5, () => true, _gameplay.Token);
        _weather.Changed += OnChanged;
    }
    private void OnChanged() => _changes++;
    private void Advance(double seconds) { _source.Now += seconds; _clock.Pump(); _scheduler.Pump(); }
    [TearDown]
    public void DisposeFeature()
    {
        if (_weather != null) _weather.Changed -= OnChanged;
        _binding?.Dispose();
        if (_object != null) Object.DestroyImmediate(_object);
        _scheduler?.Dispose(); _clock?.Dispose(); _gameplay?.Dispose();
    }

    [Test]
    public void ScheduledWeatherDoesNotAlsoAdvanceThroughThePollingCompatibilityPath()
    {
        Assert.That(_tick.UsesMatchScheduler, Is.True);
        Advance(5); Assert.That(_changes, Is.EqualTo(1));
        _weather.TickWeather(5, 1000); Assert.That(_changes, Is.EqualTo(1));
        _clock.SetPaused(true); Advance(50); Assert.That(_changes, Is.EqualTo(1));
        _clock.SetPaused(false); Advance(5); Assert.That(_changes, Is.EqualTo(2));
    }
    [Test]
    public void LongFrameCoalescesTransitionAndGameplayCancellationStopsFutureTransitions()
    {
        Advance(50); Assert.That(_changes, Is.EqualTo(1));
        _gameplay.Cancel(); Advance(50);
        Assert.That(_changes, Is.EqualTo(1)); Assert.That(_scheduler.PendingCount, Is.Zero);
    }
    [Test]
    public void ReleasingWeatherScopeCancelsTheTimerAndUnbindsTheIntervalComponent()
    {
        _binding.Dispose(); _binding = null;
        Assert.That(_tick.UsesMatchScheduler, Is.False);
        Assert.That(_scheduler.PendingCount, Is.Zero);
        Advance(20); _weather.TickWeather(5, 20); Assert.That(_changes, Is.Zero);
    }
}
