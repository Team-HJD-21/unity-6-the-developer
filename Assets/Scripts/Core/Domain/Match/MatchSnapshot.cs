// 특정 순번에서의 Match 단계·확정 결과입니다. Feature HP/전투 상태나 월드 저장 자료가 아닙니다.
using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchSnapshot
    {
        public MatchId MatchId { get; }
        public long Sequence { get; }
        public MatchPhase Phase { get; }
        public MatchResult Result { get; }

        internal MatchSnapshot(MatchId matchId, long sequence, MatchPhase phase, MatchResult result)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Snapshot requires a valid match ID.", nameof(matchId));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!Enum.IsDefined(typeof(MatchPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (result != null && result.MatchId != matchId)
                throw new ArgumentException("Snapshot result must belong to its Match.", nameof(result));
            MatchId = matchId;
            Sequence = sequence;
            Phase = phase;
            Result = result;
        }
    }
}
