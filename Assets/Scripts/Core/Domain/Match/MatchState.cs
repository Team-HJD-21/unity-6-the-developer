// MatchSession의 단계와 확정 결과만 보관합니다. 참가자 원본은 MatchConfig입니다.
// 실제 Player/CU HP·터렛·적·지역은 각 Feature/전용 snapshot을 읽습니다.
using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchState
    {
        public MatchId MatchId { get; }
        public MatchPhase Phase { get; private set; }
        public MatchResult Result { get; private set; }

        public MatchState(MatchId matchId, MatchPhase phase, MatchResult result = null)
        {
            if (matchId.IsEmpty) throw new ArgumentException("Match state requires a valid match ID.", nameof(matchId));
            if (!Enum.IsDefined(typeof(MatchPhase), phase)) throw new ArgumentOutOfRangeException(nameof(phase));
            if (result != null && result.MatchId != matchId)
                throw new ArgumentException("Initial result must belong to this Match.", nameof(result));
            MatchId = matchId;
            Phase = phase;
            Result = result;
        }

        /// <summary>Session이 실제 준비를 마친 뒤 호출합니다. 같은 상태를 두 번 시작하지 않습니다.</summary>
        public void Start()
        {
            if (Phase != MatchPhase.Preparing) throw new InvalidOperationException("Only a preparing Match can start.");
            Phase = MatchPhase.Running;
        }

        /// <summary>Session이 모드/Stage 정책으로 확정한 같은 Match의 결과만 적용합니다.</summary>
        public void Complete(MatchResult result)
        {
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.MatchId != MatchId) throw new ArgumentException("The result belongs to another Match.", nameof(result));
            if (Phase != MatchPhase.Running) throw new InvalidOperationException("Only a running Match can complete.");
            Result = result;
            Phase = MatchPhase.Completed;
        }

        /// <summary>Session 정리 끝에 단계만 닫습니다. 실제 actor/Feature는 각 소유자가 폐기합니다.</summary>
        public void MarkDisposed() => Phase = MatchPhase.Disposed;

        /// <summary>읽기 소비자가 요청한 순번의 수명주기 값을 복사합니다. 전투 HP나 저장 자료로 사용하지 않습니다.</summary>
        public MatchSnapshot CreateSnapshot(long sequence) => new(MatchId, sequence, Phase, Result);
    }
}
