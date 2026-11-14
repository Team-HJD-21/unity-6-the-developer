// 실제 최종 Boss Scene 소유자에게 저장이 끝난 현재 Match의 전환 범위를 제공합니다.
// Scene 교체나 콘텐츠를 임의 생성하지 않으며 같은 Match의 additive 준비·도착·연출은 호출자가 연결합니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class InitialStageCompositionRoot
    {
        private FinalBossTransition _finalBossTransition;
        public bool FinalBossTransitionPending => _finalBossTransition != null && !_finalBossTransition.IsDisposed;
        public string FinalBossTransitionError { get; private set; }

        /// <summary>Host의 실제 Scene 전환 소유자가 호출합니다. 반환 뒤 기존 SceneTransitions.LoadAsync로 같은 Match의 보스 Scene을 준비합니다.</summary>
        /// <remarks>반환 범위는 using으로 유지합니다. 전체 저장 실패 시 예외이며 Scene 이동/연출을 시작하면 안 됩니다.</remarks>
        public async Task<FinalBossTransition> PrepareFinalBossTransitionAsync(MatchId match, CancellationToken cancellation = default)
        {
            if (match != _ownedMatchId || !CanPrepareFinalBattle() || !(_matchHost is AppRoot app))
                throw new InvalidOperationException("현재 Host의 생존·점유·Field 사건 준비를 먼저 확인해야 합니다.");
            if (FinalBossTransitionPending) throw new InvalidOperationException("최종 보스 전환이 이미 준비 중입니다.");
            var transition = new FinalBossTransition(_coordinator.StageProgression, _matchTime, app.LocalInput,
                CanPrepareFinalBattle, _coordinator.GameplayCancellation, cancellation);
            // 전환 범위 자체가 두 취소 원본을 소유해 이 함수 반환 뒤에도 취소를 전달합니다.
            _finalBossTransition = transition;
            FinalBossTransitionError = null;
            try
            {
                await transition.PrepareAsync(new CheckpointSlot("stage-" + _recoveryLedger.RunId.ToString("N") + "-before-final"),
                    SaveStageCheckpointAsync);
                return transition;
            }
            catch (Exception error)
            {
                if (ReferenceEquals(_finalBossTransition, transition))
                {
                    _finalBossTransition = null;
                    if (HasCurrentMatch) FinalBossTransitionError = error.Message;
                }
                transition.Dispose();
                throw;
            }
        }

        private bool CanPrepareFinalBattle() => HasCurrentMatch && !_restoringCheckpoint &&
            _coordinator?.IsGameplayRunning == true && Recovery?.CanCaptureWorld == true &&
            Recovery.IsRecoveryPending != true && _coordinator.StageProgression?.Snapshot.CanEnterFinalBattle == true;

        private void PumpFinalBossTransition()
        {
            _finalBossTransition?.InvalidateIfStale();
            if (_finalBossTransition?.IsDisposed == true) _finalBossTransition = null;
        }
    }
}
