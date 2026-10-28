// Scene의 실제 환경 source를 Host Match의 읽기 projection/clock/취소 수명에 연결합니다.
// source가 없으면 해당 축은 미제공이며 환경 변경을 Spawn 요청으로 바꾸지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class MatchEnvironmentSceneScope : IDisposable
    {
        private readonly MatchSession _session;
        private readonly IMatchWeatherSource _weather;
        private readonly IMatchTimeOfDaySource _time;
        private readonly List<IDisposable> _leases = new List<IDisposable>();
        private readonly HashSet<IMatchEnvironmentFeature> _features;
        private bool _disposed;
        public MatchEnvironmentSceneScope(MatchSession session, MatchCoordinator coordinator,
            IMatchWeatherSource weather, IMatchTimeOfDaySource time, Func<bool> mayWrite)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _weather = weather; _time = time; _features = Collect(weather, time);
            session.InitializeEnvironment(coordinator, () => !_disposed && session.IsGameplayRunning && mayWrite());
            try
            {
                foreach (var feature in _features)
                    _leases.Add(feature.BindAuthority(session.Config.MatchId, coordinator, session.Config.RandomSeed, mayWrite, coordinator.GameplayCancellation));
                foreach (var feature in _features) feature.Changed += Publish;
                session.Disposing += Dispose;
                Publish();
            }
            catch (Exception initializationError)
            {
                try { Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException(initializationError, cleanupError); }
                throw;
            }
        }
        private void Publish()
        {
            if (_disposed || _session.IsDisposed) return;
            _session.UpdateEnvironment(_weather?.WeatherId ?? default, _time?.TimeOfDayId ?? default,
                _weather?.Forecast ?? Array.Empty<DefinitionId>());
        }
        internal static HashSet<IMatchEnvironmentFeature> Collect(IMatchWeatherSource weather, IMatchTimeOfDaySource time)
        {
            var features = new HashSet<IMatchEnvironmentFeature>();
            if (weather != null) features.Add(weather); if (time != null) features.Add(time); return features;
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _session.Disposing -= Dispose;
            var errors = new List<Exception>();
            foreach (var feature in _features) try { feature.Changed -= Publish; } catch (Exception error) { errors.Add(error); }
            foreach (var lease in _leases) try { lease.Dispose(); } catch (Exception error) { errors.Add(error); }
            _leases.Clear();
            if (errors.Count > 0) throw new AggregateException("World environment cleanup failed.", errors);
        }
    }
}
