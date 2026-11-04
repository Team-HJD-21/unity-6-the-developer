// Feature의 난수 필드를 정적 함수 한 번으로 연결하는 공용 헬퍼입니다.
// 반환 바인딩을 Core가 해제하면 보관된 참조도 더 이상 난수를 소비할 수 없습니다.
// 난수 서비스·seed·Match 자체는 소유하지 않으며 새 Match에는 다시 연결할 수 있습니다.

using System;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Application
{
    public static class RandomBinding
    {
        /// <summary>Feature의 BindRandom에서 호출해 이름별 난수를 필드에 연결합니다.</summary>
        /// <param name="random">Core가 이번 App/Match 수명에 맞춰 전달한 난수 흐름입니다.</param>
        /// <param name="streamId">초기화 때 정한 이름입니다. 예: weather, combat/cannon.</param>
        /// <param name="target">초기값이 null이거나 이전 바인딩이 해제된 IRandomSource 필드입니다.</param>
        /// <returns>IRandomConsumer는 이 값을 그대로 반환합니다. Core가 종료·조립 실패 때 해제합니다.</returns>
        /// <remarks>
        /// 활성 바인딩을 덮어쓰지 않습니다. 실패하면 target을 바꾸지 않습니다.
        /// 해제 후 target은 null이 아닌 사용 불가 참조이며 IsAvailable이 false가 됩니다.
        /// 직접 조립하는 호출자는 반환값을 보관해 Dispose해야 합니다. 메인 스레드에서 사용합니다.
        /// </remarks>
        public static IDisposable Bind(IRandomStreams random, string streamId, ref IRandomSource target)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (target != null && !(target is Binding previous && previous.IsDisposed))
                throw new InvalidOperationException("The random field already has an active binding.");

            IRandomSource source = random.GetStream(streamId) ??
                throw new InvalidOperationException("The random stream did not provide a source.");
            var binding = new Binding(source);
            target = binding;
            return binding;
        }

        private sealed class Binding : IRandomSource, IDisposable
        {
            private IRandomSource _source;

            public bool IsDisposed => _source == null;
            public bool IsAvailable => _source != null && _source.IsAvailable;
            private IRandomSource Source => _source ??
                throw new ObjectDisposedException(nameof(RandomBinding));

            public Binding(IRandomSource source) => _source = source;

            public uint NextUInt32() => Source.NextUInt32();

            public int NextInt(int minInclusive, int maxExclusive) =>
                Source.NextInt(minInclusive, maxExclusive);

            public float NextFloat() => Source.NextFloat();

            public float NextFloat(float minInclusive, float maxExclusive) =>
                Source.NextFloat(minInclusive, maxExclusive);

            public double NextDouble() => Source.NextDouble();

            public double NextDouble(double minInclusive, double maxExclusive) =>
                Source.NextDouble(minInclusive, maxExclusive);

            // 다른 곳에 복사한 이전 참조도 이 바인딩과 함께 사용을 차단합니다.
            public void Dispose() => _source = null;
        }
    }
}
