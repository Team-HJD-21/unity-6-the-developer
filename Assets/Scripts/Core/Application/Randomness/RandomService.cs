// 범용 난수 서비스의 이름별 흐름·seed·권한·저장 원본을 소유합니다.
// 같은 서비스의 같은 이름만 상태를 공유합니다. Feature는 IRandomStreams만 전달받습니다.
// 정수·실수 모두 상한 제외이며 seed 변경/복원은 Core 소유자에게만 노출합니다.

using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class RandomService : IRandomStreams, IDisposable
    {
        private Dictionary<string, Pcg32> _states = new(StringComparer.Ordinal);
        private readonly Func<bool> _mayUse;
        private bool _disposed;

        public int Seed { get; }
        public bool IsAvailable => !_disposed && _mayUse();

        /// <summary>Core 조립부가 App/Match 등 독립된 수명의 난수 서비스를 생성합니다.</summary>
        /// <param name="mayUse">Host·현재 Match 등 소비 권한 검사입니다. 생략하면 로컬 소유자로 사용합니다.</param>
        /// <remarks>메인 스레드 전용입니다. UI 효과는 게임 진행용 서비스와 다른 인스턴스를 주입합니다.</remarks>
        public RandomService(int seed, Func<bool> mayUse = null)
        {
            Seed = seed;
            _mayUse = mayUse ?? (() => true);
        }

        /// <inheritdoc />
        public IRandomSource GetStream(string streamId)
        {
            RequireAvailable();
            RandomStreamState.ValidateId(streamId);
            EnsureStream(streamId);
            return new Source(this, streamId);
        }

        /// <summary>Core 저장 담당이 모든 흐름의 진행 상태를 복사합니다. 추첨을 진행하지 않습니다.</summary>
        public RandomStateSnapshot Capture()
        {
            RequireAvailable();
            return new RandomStateSnapshot(RandomStateSnapshot.CurrentAlgorithmVersion, Seed,
                _states.Select(pair => new RandomStreamState(pair.Key, pair.Value.State)));
        }

        /// <summary>Core 저장 복원 단계가 검증된 같은 seed의 상태를 원자적으로 적용합니다.</summary>
        /// <remarks>기존 source 참조도 새 상태를 따릅니다. 저장 이후 처음 생긴 흐름은 초기 상태로 돌아갑니다.</remarks>
        public void Restore(RandomStateSnapshot snapshot)
        {
            RequireAvailable();
            if (snapshot == null)
                throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.Seed != Seed)
                throw new ArgumentException("Random state belongs to another seed.", nameof(snapshot));
            var next = new Dictionary<string, Pcg32>(StringComparer.Ordinal);
            foreach (var entry in snapshot.Streams)
            {
                var generator = Create(entry.StreamId);
                generator.State = entry.State;
                next.Add(entry.StreamId, generator);
            }
            _states = next;
        }

        private Pcg32 Create(string id)
        {
            // string.GetHashCode는 실행마다 달라질 수 있어 저장 계약에 고정한 FNV-1a를 사용합니다.
            ulong hash = 14695981039346656037UL;
            foreach (char c in id)
                hash = unchecked((hash ^ c) * 1099511628211UL);
            return new Pcg32(unchecked((uint)Seed), hash);
        }

        private void EnsureStream(string id)
        {
            if (_states.ContainsKey(id))
                return;
            if (_states.Count == RandomStateSnapshot.MaximumStreams)
                throw new InvalidOperationException("Too many random streams in this scope.");
            _states.Add(id, Create(id));
        }

        private uint Draw(string id, uint? bound = null)
        {
            RequireAvailable();
            EnsureStream(id);
            var generator = _states[id];
            uint value = bound.HasValue ? generator.Bounded(bound.Value) : generator.Next();
            _states[id] = generator;
            return value;
        }

        private void RequireAvailable()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(RandomService));
            if (!_mayUse())
                throw new InvalidOperationException("This scope cannot consume authoritative random values.");
        }

        /// <summary>Core 소유자가 scope 종료 때 호출합니다. 이미 전달한 source도 즉시 사용할 수 없게 됩니다.</summary>
        public void Dispose()
        {
            _disposed = true;
            _states.Clear();
        }

        private sealed class Source : IRandomSource
        {
            private readonly RandomService _owner;
            private readonly string _id;

            public bool IsAvailable => _owner.IsAvailable;
            public Source(RandomService owner, string id)
            {
                _owner = owner;
                _id = id;
            }

            public uint NextUInt32() => _owner.Draw(_id);
            public int NextInt(int minInclusive, int maxExclusive)
            {
                if (minInclusive >= maxExclusive)
                    throw new ArgumentOutOfRangeException(nameof(maxExclusive));
                uint width = (uint)((long)maxExclusive - minInclusive);
                return (int)(minInclusive + (long)_owner.Draw(_id, width));
            }

            public float NextFloat() => (_owner.Draw(_id) >> 8) * (1f / 16777216f);
            public double NextDouble()
            {
                ulong high = _owner.Draw(_id) >> 5, low = _owner.Draw(_id) >> 6;
                return ((high << 26) | low) * (1d / 9007199254740992d);
            }

            public float NextFloat(float minInclusive, float maxExclusive)
            {
                ValidateRange(minInclusive, maxExclusive);
                double t = NextFloat();
                float result = (float)((1d - t) * minInclusive + t * maxExclusive);
                if (result < maxExclusive)
                    return Math.Max(minInclusive, result);
                if (maxExclusive == 0)
                    return -float.Epsilon;
                int bits = BitConverter.SingleToInt32Bits(maxExclusive);
                return BitConverter.Int32BitsToSingle(maxExclusive > 0 ? bits - 1 : bits + 1);
            }

            public double NextDouble(double minInclusive, double maxExclusive)
            {
                ValidateRange(minInclusive, maxExclusive);
                double t = NextDouble();
                double result = (1d - t) * minInclusive + t * maxExclusive;
                return result >= maxExclusive ? PreviousDouble(maxExclusive) : Math.Max(minInclusive, result);
            }

            private static void ValidateRange(double min, double max)
            {
                if (double.IsNaN(min) || double.IsNaN(max) || double.IsInfinity(min) || double.IsInfinity(max) || min >= max)
                    throw new ArgumentOutOfRangeException(nameof(max), "Use finite ascending bounds.");
            }

            private static double PreviousDouble(double value)
            {
                if (value == 0)
                    return -double.Epsilon;
                long bits = BitConverter.DoubleToInt64Bits(value);
                return BitConverter.Int64BitsToDouble(value > 0 ? bits - 1 : bits + 1);
            }
        }
    }
}
