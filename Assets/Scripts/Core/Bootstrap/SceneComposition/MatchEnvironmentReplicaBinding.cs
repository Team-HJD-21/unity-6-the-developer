// Client의 확정 환경 읽기 모델을 World 표현에 연결합니다.
// 독립 타이머/Random을 시작하지 않고 다른 Match의 늦은 상태를 적용하지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed class MatchEnvironmentReplicaBinding : IDisposable
    {
        private readonly MatchId _match;
        private readonly IMatchEnvironmentSource _source;
        private readonly HashSet<IMatchEnvironmentFeature> _features;
        private readonly List<IDisposable> _leases = new List<IDisposable>();
        private bool _disposed;
        public MatchEnvironmentReplicaBinding(MatchId match, IMatchEnvironmentSource source,
            IMatchWeatherSource weather, IMatchTimeOfDaySource time)
        {
            if (source?.Snapshot == null || source.Snapshot.MatchId != match) throw new ArgumentException("Provide the current environment replica.");
            _match = match; _source = source; _features = MatchEnvironmentSceneScope.Collect(weather, time);
            try
            {
                foreach (var feature in _features) _leases.Add(feature.BindReplica(match));
                source.Changed += Apply; Apply(source.Snapshot);
            }
            catch (Exception initializationError)
            {
                try { Dispose(); }
                catch (Exception cleanupError) { throw new AggregateException(initializationError, cleanupError); }
                throw;
            }
        }
        private void Apply(MatchEnvironmentSnapshot snapshot)
        {
            if (_disposed || snapshot == null || snapshot.MatchId != _match) return;
            var errors = new List<Exception>();
            foreach (var feature in _features) try { feature.ApplyReplica(snapshot); } catch (Exception error) { errors.Add(error); }
            if (errors.Count > 0) throw new AggregateException("Environment received; some World views failed.", errors);
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true; _source.Changed -= Apply;
            var errors = new List<Exception>();
            foreach (var lease in _leases) try { lease.Dispose(); } catch (Exception error) { errors.Add(error); }
            _leases.Clear();
            if (errors.Count > 0) throw new AggregateException("World replica cleanup failed.", errors);
        }
    }
}
