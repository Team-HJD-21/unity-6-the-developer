// Match 시뮬레이션 단계에서 발생한 불변 사실의 공통 데이터를 정의합니다.


using System;

namespace TeamHJD.Game.Domain
{
    public abstract class MatchEvent
    {
        public MatchId MatchId { get; }
        public long Sequence { get; }

        protected MatchEvent(MatchId matchId, long sequence)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A match event requires a valid match ID.", nameof(matchId));
            if (sequence < 0) throw new ArgumentOutOfRangeException(nameof(sequence));
            MatchId = matchId;
            Sequence = sequence;
        }
    }
}
