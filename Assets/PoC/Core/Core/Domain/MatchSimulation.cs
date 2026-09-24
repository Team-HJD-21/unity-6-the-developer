// Match 단계 전이와 Domain 명령 실행 경계를 담당합니다.

using System;

namespace TeamHJD.Game.Domain
{
    public sealed class MatchSimulation
    {
        public void Start(MatchState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Phase != MatchPhase.Preparing) throw new InvalidOperationException("Only a preparing match can start.");
            state.SetPhase(MatchPhase.Running);
        }

        public SimulationOutcome Execute(MatchState state, GameCommand command, IModeRules modeRules)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (modeRules == null) throw new ArgumentNullException(nameof(modeRules));
            if (state.Phase != MatchPhase.Running) throw new InvalidOperationException("Commands can only be simulated while the match is running.");

            // 실제 게임 규칙을 구현하기 전까지 명령 처리기는 의도적으로 비워 둡니다.
            return new SimulationOutcome(SimulationStatus.NotHandled, Array.Empty<MatchEvent>());
        }

        public void Complete(MatchState state, MatchResult result)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (result == null) throw new ArgumentNullException(nameof(result));
            if (result.MatchId != state.MatchId) throw new ArgumentException("Match result must belong to the target match.", nameof(result));
            if (state.Phase != MatchPhase.Running) throw new InvalidOperationException("Only a running match can complete.");
            state.SetResult(result);
            state.SetPhase(MatchPhase.Completed);
        }

        public void Dispose(MatchState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            state.SetPhase(MatchPhase.Disposed);
        }
    }
}
