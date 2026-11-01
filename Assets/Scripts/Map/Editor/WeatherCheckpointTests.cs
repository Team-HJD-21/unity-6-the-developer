// 실제 WeatherManager의 예고·난수 진행·남은 전환 시간과 취소된 예약의 재등록을 검사합니다.
// 새 날씨 규칙이나 자동 저장 조건을 선정하는 테스트가 아닙니다.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Map;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class WeatherCheckpointTests
{
    private sealed class Timestamp : ITimestampSource
    { public double Now; public bool TryReadSeconds(out double seconds) { seconds = Now; return true; } }
    private Timestamp _source;
    private MatchClock _clock;
    private MatchScheduler _scheduler;
    private CancellationTokenSource _lifetime;
    private IDisposable _binding;
    private GameObject _object;
    private WeatherManager _weather;
    private MatchConfig _config;
    private readonly BinaryCheckpointPayloadCodec _codec = new();
    private ICheckpointParticipant Writer => _weather;
    private CheckpointCaptureContext Context => new(_config, _clock.ElapsedSeconds, _clock.Snapshot.Epoch);

    [SetUp]
    public void SetUp()
    {
        _source = new Timestamp(); var match = new MatchId("weather-checkpoint");
        _clock = new MatchClock(match, _source); _scheduler = new MatchScheduler(_clock);
        _lifetime = new CancellationTokenSource();
        _config = new MatchConfig(match, MatchMode.Story, new StageId("stage"), new DefinitionId("normal"), "content", 42,
            new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
        _object = new GameObject("checkpoint weather");
        _weather = _object.AddComponent<WeatherManager>(); _object.AddComponent<CountTick>();
        _binding = _weather.BindAuthority(match, _clock, _scheduler, 42, () => true, _lifetime.Token);
    }
    [TearDown]
    public void TearDown()
    {
        _binding?.Dispose(); Object.DestroyImmediate(_object);
        _scheduler?.Dispose(); _clock?.Dispose(); _lifetime?.Dispose();
    }
    private void Advance(double seconds) { _source.Now += seconds; _clock.Pump(); _scheduler.Pump(); }
    private CheckpointRestoreContext RestoreContext(CheckpointSection section)
    {
        var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, _config.MatchId, _config.StageId, _config.DifficultyProfileId,
            _config.Mode, _config.RandomSeed, _config.ContentVersion, _clock.ElapsedSeconds, 1, new[] { section });
        return new CheckpointRestoreContext(Context, data, CheckpointRestorePolicy.Default);
    }

    [Test]
    public void ForecastAndRngContinueWithTheSameFutureAfterRestoration()
    {
        Advance(2);
        var saved = Writer.Capture(Context, _codec);
        string initial = _weather.WeatherId.Value;
        string[] forecast = _weather.Forecast.Select(id => id.Value).ToArray();
        var expected = new List<string>();
        for (int i = 0; i < 8; i++) { _weather.ChangeWeather(); expected.Add(string.Join(",", _weather.Forecast.Select(id => id.Value))); }
        using var restore = Writer.PrepareRestore(RestoreContext(saved), saved, _codec);
        _clock.Restore(2); restore.Apply();
        Assert.That(_weather.WeatherId.Value, Is.EqualTo(initial));
        Assert.That(_weather.Forecast.Select(id => id.Value), Is.EqualTo(forecast));
        var actual = new List<string>();
        for (int i = 0; i < 8; i++) { _weather.ChangeWeather(); actual.Add(string.Join(",", _weather.Forecast.Select(id => id.Value))); }
        Assert.That(actual, Is.EqualTo(expected));
    }

    [Test]
    public void RemainingGameDurationIsRestoredAndOnlyOneTimerRemains()
    {
        Advance(2); var saved = Writer.Capture(Context, _codec);
        Advance(17);
        using var restore = Writer.PrepareRestore(RestoreContext(saved), saved, _codec);
        _clock.Restore(2); restore.Apply();
        int changes = 0; _weather.Changed += () => changes++;
        Assert.That(_scheduler.PendingCount, Is.EqualTo(1));
        Advance(2.9); Assert.That(changes, Is.Zero);
        Advance(0.2); Assert.That(changes, Is.EqualTo(1));
        _clock.SetPaused(true); Advance(50); Assert.That(changes, Is.EqualTo(1));
        _clock.SetPaused(false); Advance(4.9); Assert.That(changes, Is.EqualTo(2));
    }

    [Test]
    public void RollbackRecreatesCancelledTimerEvenWhenApplyWasNotRun()
    {
        var saved = Writer.Capture(Context, _codec);
        Advance(4);
        string[] backup = _weather.Forecast.Select(id => id.Value).ToArray();
        using var restore = Writer.PrepareRestore(RestoreContext(saved), saved, _codec);
        _clock.Restore(0); _clock.Restore(4); restore.Rollback();
        Assert.That(_weather.Forecast.Select(id => id.Value), Is.EqualTo(backup));
        int changes = 0; _weather.Changed += () => changes++;
        Advance(0.9); Assert.That(changes, Is.Zero);
        Advance(0.2); Assert.That(changes, Is.EqualTo(1));
        Assert.That(_scheduler.PendingCount, Is.EqualTo(1));
    }

    [Test]
    public void CorruptRngAndFutureSchemaRejectBeforeChangingCurrentWeather()
    {
        var saved = Writer.Capture(Context, _codec); byte[] bytes = saved.CopyPayload();
        Array.Clear(bytes, 12, 4); // Current, queue size, weight 뒤의 RNG 상태입니다.
        var bad = new CheckpointSection(saved.Key, 1, saved.Kind, bytes);
        var before = _weather.Forecast.Select(id => id.Value).ToArray();
        Assert.Throws<InvalidDataException>(() => Writer.PrepareRestore(RestoreContext(bad), bad, _codec));
        var future = new CheckpointSection(saved.Key, 2, saved.Kind, saved.CopyPayload());
        Assert.Throws<NotSupportedException>(() => Writer.PrepareRestore(RestoreContext(future), future, _codec));
        Assert.That(_weather.Forecast.Select(id => id.Value), Is.EqualTo(before));
        Assert.That(_scheduler.PendingCount, Is.EqualTo(1));
    }
}
