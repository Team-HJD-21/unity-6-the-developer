// Feature에 난수를 주입하고 반환받은 바인딩을 Match 종료·조립 실패 때 해제합니다.
// 난수 서비스와 Feature 객체 자체의 수명은 소유하지 않습니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Application
{
    public sealed class RandomConsumerScope : IDisposable
    {
        private readonly List<IDisposable> _leases = new();
        private bool _disposed;

        /// <summary>Core Scene 조립부가 권위 Feature 시작 전에 난수를 주입합니다.</summary>
        public RandomConsumerScope(IRandomStreams random, IEnumerable<IRandomConsumer> consumers)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (consumers == null)
                throw new ArgumentNullException(nameof(consumers));
            try
            {
                foreach (var consumer in new HashSet<IRandomConsumer>(consumers))
                {
                    if (consumer == null)
                        throw new ArgumentException("Null random consumer.", nameof(consumers));
                    _leases.Add(consumer.BindRandom(random) ??
                        throw new InvalidOperationException("Random binding must return a lease."));
                }
            }
            catch (Exception error)
            {
                try
                {
                    Dispose();
                }
                catch (Exception cleanup)
                {
                    throw new AggregateException(error, cleanup);
                }
                throw;
            }
        }

        /// <summary>소유 Scene이 종료/조립 실패 시 역순으로 모든 바인딩을 해제합니다.</summary>
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            var errors = new List<Exception>();
            for (int i = _leases.Count - 1; i >= 0; i--)
                try
                {
                    _leases[i].Dispose();
                }
                catch (Exception error)
                {
                    errors.Add(error);
                }
            _leases.Clear();
            if (errors.Count > 0)
                throw new AggregateException("Random bindings failed to release.", errors);
        }
    }
}
