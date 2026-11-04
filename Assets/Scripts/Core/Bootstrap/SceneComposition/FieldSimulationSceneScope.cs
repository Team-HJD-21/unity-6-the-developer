// 권위/Client 조립부가 공유하는 필드 Feature 시간 바인딩의 수명만 관리합니다.
// 참가자 정책·Player 생성·네트워크 연결은 소유하지 않습니다.
using System;
using System.Collections.Generic;
using System.Threading;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    internal sealed class FieldSimulationSceneScope : IDisposable
    {
        private readonly List<IDisposable> _bindings = new();
        private bool _disposed;
        public FieldSimulationSceneScope(IEnumerable<IFieldSimulationConsumer> features, IMatchTime time, CancellationToken gameplay)
        {
            try { foreach (var feature in features) _bindings.Add(feature.BindFieldSimulation(time, gameplay)); }
            catch (Exception error)
            { try { Dispose(); } catch (Exception cleanup) { throw new AggregateException(error, cleanup); } throw; }
        }
        public void Dispose()
        {
            if (_disposed) return; _disposed = true;
            var errors = new List<Exception>();
            for (int i = _bindings.Count - 1; i >= 0; i--)
                try { _bindings[i].Dispose(); } catch (Exception error) { errors.Add(error); }
            _bindings.Clear();
            if (errors.Count > 0) throw new AggregateException("Field simulation bindings were released with errors.", errors);
        }
    }
}
