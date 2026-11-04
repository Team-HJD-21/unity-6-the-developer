// seed·알고리즘 버전·이름별 상태를 불변 복사본으로 보관합니다.
// 같은 seed의 새 Match 복원도 허용하되 다른 seed나 알고리즘으로 해석하지 않습니다.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace TeamHJD.Game.Domain
{
    public sealed class RandomStateSnapshot
    {
        public const int CurrentAlgorithmVersion = 1;
        public const int MaximumStreams = 256;
        public int AlgorithmVersion { get; }
        public int Seed { get; }
        public IReadOnlyList<RandomStreamState> Streams { get; }

        /// <summary>Core가 지원 버전·중복·개수를 검증하고 이름순으로 상태를 복사합니다.</summary>
        public RandomStateSnapshot(int algorithmVersion, int seed, IEnumerable<RandomStreamState> streams)
        {
            if (algorithmVersion != CurrentAlgorithmVersion)
                throw new NotSupportedException("Unsupported random algorithm version.");
            if (streams == null)
                throw new ArgumentNullException(nameof(streams));
            var copy = new List<RandomStreamState>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var stream in streams)
            {
                RandomStreamState.ValidateId(stream.StreamId);
                if (copy.Count == MaximumStreams || !ids.Add(stream.StreamId))
                    throw new ArgumentException(
                        "Random streams exceed the limit or contain duplicate IDs.", nameof(streams));
                copy.Add(stream);
            }
            AlgorithmVersion = algorithmVersion;
            Seed = seed;
            Streams = new ReadOnlyCollection<RandomStreamState>(
                copy.OrderBy(s => s.StreamId, StringComparer.Ordinal).ToArray());
        }
    }
}
