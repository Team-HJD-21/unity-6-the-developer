// Client의 읽기 시계/확정 위치를 로컬 Player와 필드 표현에 연결합니다.
// 권위 위치 등록이나 시계 제어권을 주지 않으며 Scene 해제 때 모든 읽기 바인딩을 닫습니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    internal sealed class MatchFieldReplicaScope : IDisposable
    {
        private readonly IMatchTime _time;
        private readonly CancellationTokenSource _gameplay = new();
        private readonly FieldSimulationSceneScope _features;
        private readonly PlayerMatchPresence _actor;
        private bool _disposed;
        public MatchFieldReplicaScope(IMatchTime time, IEnumerable<IFieldSimulationConsumer> features, GameObject actor)
        {
            _time = time;
            try
            {
                _actor = actor.GetComponent<PlayerMatchPresence>() ?? actor.AddComponent<PlayerMatchPresence>();
                _actor.BindReplica(time, _gameplay.Token);
                _features = new FieldSimulationSceneScope(features, time, _gameplay.Token);
            }
            catch (Exception error)
            { try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); } throw; }
        }
        public void ApplyLocation(MatchPlayerLocation location) { if (!_disposed && _actor != null) _actor.ApplyReplicaLocation(_time, location); }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            try { _gameplay.Cancel(); } catch (Exception error) { errors.Add(error); }
            try { if (_actor != null) _actor.UnbindReplica(_time); } catch (Exception error) { errors.Add(error); }
            try { _features?.Dispose(); } catch (Exception error) { errors.Add(error); }
            _gameplay.Dispose();
            if (errors.Count > 0) throw new AggregateException("Client field scope cleanup failed.", errors);
        }
    }
}
