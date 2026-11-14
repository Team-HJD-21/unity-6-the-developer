// Match 종료 시 생성되는 불변 결과입니다.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchResult
    {
        public MatchId MatchId { get; }
        public MatchOutcome Outcome { get; }
        public int CompletedWave { get; }
        public DateTimeOffset CompletedAt { get; }
        public MatchEndReason EndReason { get; }

        public MatchResult(MatchId matchId, MatchOutcome outcome, int completedWave, DateTimeOffset completedAt,
            MatchEndReason endReason = MatchEndReason.None)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Match result requires a valid match ID.", nameof(matchId));
            if (!Enum.IsDefined(typeof(MatchOutcome), outcome)) throw new ArgumentOutOfRangeException(nameof(outcome));
            if (completedWave < 0) throw new ArgumentOutOfRangeException(nameof(completedWave));
            if (!Enum.IsDefined(typeof(MatchEndReason), endReason)) throw new ArgumentOutOfRangeException(nameof(endReason));
            if (outcome != MatchOutcome.Defeat && endReason != MatchEndReason.None) throw new ArgumentException("Only defeats carry a failure reason.", nameof(endReason));
            MatchId = matchId;
            Outcome = outcome;
            CompletedWave = completedWave;
            CompletedAt = completedAt;
            EndReason = endReason;
        }
    }
}
