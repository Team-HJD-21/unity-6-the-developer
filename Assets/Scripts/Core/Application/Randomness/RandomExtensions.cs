// 범용 난수 source에 확률·목록 선택·가중치 선택·셔플·2D 분포 함수를 제공합니다.
// 선택 규칙만 계산하며 Feature 상태와 Unity 객체를 직접 변경하지 않습니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Application
{
    public static class RandomExtensions
    {

        /// <summary>확률 0~1로 성공을 선택합니다. 0은 항상 실패, 1은 항상 성공입니다.</summary>
        /// <remarks>끝값 0/1에서는 난수를 소비하지 않습니다. 사용 권한은 동일하게 검사합니다.</remarks>
        public static bool Chance(this IRandomSource random, double probability)
        {
            Require(random);
            if (double.IsNaN(probability) || probability < 0 || probability > 1)
                throw new ArgumentOutOfRangeException(nameof(probability));
            return probability == 1 || (probability != 0 && random.NextDouble() < probability);
        }

        /// <summary>동일한 확률로 true/false를 선택합니다.</summary>
        public static bool NextBool(this IRandomSource random)
        {
            Require(random);
            return random.NextInt(0, 2) == 1;
        }

        /// <summary>비어 있지 않은 목록에서 한 항목을 같은 확률로 선택합니다.</summary>
        public static T Choose<T>(this IRandomSource random, IReadOnlyList<T> values)
        {
            Require(random);
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (values.Count == 0)
                throw new ArgumentException("Cannot choose from an empty list.", nameof(values));
            return values[random.NextInt(0, values.Count)];
        }

        /// <summary>0 이상의 유한한 가중치에 비례해 인덱스를 선택합니다. 0인 항목은 선택하지 않습니다.</summary>
        /// <remarks>합이 0인 입력은 거부합니다. 큰 가중치 합의 overflow를 막도록 최댓값으로 정규화합니다.</remarks>
        public static int WeightedIndex(this IRandomSource random, IReadOnlyList<double> weights)
        {
            Require(random);
            if (weights == null)
                throw new ArgumentNullException(nameof(weights));
            double maximum = 0;
            int lastPositive = -1;
            for (int i = 0; i < weights.Count; i++)
            {
                double weight = weights[i];
                if (double.IsNaN(weight) || double.IsInfinity(weight) || weight < 0)
                    throw new ArgumentOutOfRangeException(nameof(weights));
                if (weight > 0)
                {
                    maximum = Math.Max(maximum, weight);
                    lastPositive = i;
                }
            }
            if (lastPositive < 0)
                throw new ArgumentException("At least one weight must be positive.", nameof(weights));
            double total = 0;
            for (int i = 0; i < weights.Count; i++)
                total += weights[i] / maximum;
            double selection = random.NextDouble() * total, accumulated = 0;
            for (int i = 0; i < weights.Count; i++)
            {
                accumulated += weights[i] / maximum;
                if (weights[i] > 0 && selection < accumulated)
                    return i;
            }
            return lastPositive;
        }

        /// <summary>같은 길이의 항목/가중치 목록에서 가중치에 비례해 한 항목을 선택합니다.</summary>
        public static T WeightedChoose<T>(this IRandomSource random, IReadOnlyList<T> values, IReadOnlyList<double> weights)
        {
            Require(random);
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            if (weights == null)
                throw new ArgumentNullException(nameof(weights));
            if (values.Count != weights.Count)
                throw new ArgumentException("Values and weights must have the same count.", nameof(weights));
            return values[random.WeightedIndex(weights)];
        }

        /// <summary>호출자가 소유한 수정 가능한 목록을 Fisher–Yates 방식으로 제자리에서 섞습니다.</summary>
        public static void Shuffle<T>(this IRandomSource random, IList<T> values)
        {
            Require(random);
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            // 배열의 IsReadOnly는 true지만 원소 교환은 가능하므로 배열은 허용합니다.
            if (values.IsReadOnly && !(values is T[]))
                throw new ArgumentException("Shuffle requires a mutable list.", nameof(values));
            for (int i = values.Count - 1; i > 0; i--)
            {
                int j = random.NextInt(0, i + 1);
                T temporary = values[i];
                values[i] = values[j];
                values[j] = temporary;
            }
        }

        /// <summary>XY 단위 원판의 면적에 균일한 좌표를 반환합니다. Unity Vector2 변환은 호출자가 합니다.</summary>
        public static (double X, double Y) InsideUnitCircle(this IRandomSource random)
        {
            Require(random);
            double radius = Math.Sqrt(random.NextDouble()), angle = random.NextDouble() * (Math.PI * 2);
            return (radius * Math.Cos(angle), radius * Math.Sin(angle));
        }

        private static void Require(IRandomSource random)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));
            if (!random.IsAvailable)
                throw new InvalidOperationException("The random source is unavailable in this scope.");
        }
    }
}
