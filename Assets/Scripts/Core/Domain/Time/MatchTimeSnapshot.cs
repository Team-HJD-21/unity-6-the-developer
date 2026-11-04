// 게임 시각을 원본 시각에 대응시키는 불변 기준값입니다.
// 네트워크 표시용 anchor이며 저장 복원 시에는 현재 SDK 시각으로 새 기준을 만들어야 합니다.
using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchTimeSnapshot
    {
        public MatchId MatchId { get; }
        public Guid Epoch { get; }
        public long Revision { get; }
        public double GameSecondsAtAnchor { get; }
        public double SourceSecondsAtAnchor { get; }
        public double Rate { get; }
        public bool IsPaused { get; }
        public bool IsAvailable { get; }

        public MatchTimeSnapshot(MatchId matchId, Guid epoch, long revision, double gameSeconds,
            double sourceSeconds, double rate, bool paused, bool available)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match is required.", nameof(matchId));
            if (epoch == Guid.Empty) throw new ArgumentException("An execution epoch is required.", nameof(epoch));
            if (revision < 0) throw new ArgumentOutOfRangeException(nameof(revision));
            RequireSeconds(gameSeconds, nameof(gameSeconds)); RequireSeconds(sourceSeconds, nameof(sourceSeconds));
            RequireSeconds(rate, nameof(rate));
            MatchId = matchId; Epoch = epoch; Revision = revision;
            GameSecondsAtAnchor = gameSeconds; SourceSecondsAtAnchor = sourceSeconds;
            Rate = rate; IsPaused = paused; IsAvailable = available;
        }

        /// <summary>표현 어댑터가 같은 원본 시각계의 현재 초를 넣어 게임 시각을 계산합니다.</summary>
        /// <remarks>동기화 오차로 anchor보다 과거인 입력은 anchor에 고정합니다. 권위 상태를 변경하지 않습니다.</remarks>
        public double EstimateElapsedSeconds(double sourceSeconds)
        {
            RequireSeconds(sourceSeconds, nameof(sourceSeconds));
            if (!IsAvailable || IsPaused) return GameSecondsAtAnchor;
            double result = GameSecondsAtAnchor + Math.Max(0, sourceSeconds - SourceSecondsAtAnchor) * Rate;
            RequireSeconds(result, nameof(sourceSeconds));
            return result;
        }

        private static void RequireSeconds(double value, string parameter)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0)
                throw new ArgumentOutOfRangeException(parameter);
        }
    }
}
