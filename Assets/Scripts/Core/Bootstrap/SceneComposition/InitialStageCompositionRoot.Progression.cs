// 현재 Scene의 확정 점유율을 Stage 진행에 전달하고 실제 Boss Feature의 준비/사망을 연결합니다.
// Boss 콘텐츠가 없으면 미연결 이유를 표시하며 임의 대체 생성이나 승리 처리를 하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class InitialStageCompositionRoot
    {
        private readonly List<IEnemyCombatSource> _stageCombatSources = new();
        private StageCheckpointGate _stageCheckpoints;
        public StageProgressSnapshot StageProgress => _coordinator?.StageProgression?.Snapshot;
        public IReadOnlyList<string> PendingStageCheckpoints => _stageCheckpoints?.PendingKeys ?? Array.Empty<string>();
        public string StageCheckpointError => _stageCheckpoints?.FailureReason;
        private void BindStageProgression()
        {
            var sources = FindSceneBehaviours().OfType<IFieldBossSpawnService>().ToList();
            if (_encounterConsumer is IFieldBossSpawnService encounter && !sources.Contains(encounter)) sources.Add(encounter);
            if (sources.Count > 1) throw new InvalidOperationException("Keep one Field boss spawn owner per Stage.");
            _stageCheckpoints = new StageCheckpointGate(_ownedMatchId, _recoveryLedger.RunId,
                () => Checkpoints?.Epoch ?? Guid.Empty, SaveStageCheckpointAsync, sources.SingleOrDefault(), _coordinator.GameplayCancellation);
            _coordinator.ConfigureStageProgression(_recoveryLedger.RunId, _matchTime.FieldClock,
                _stageCheckpoints.HasSpawnSource ? _stageCheckpoints : null);
            _coordinator.StageProgression.FieldBossDefeated += _stageCheckpoints.RecordFieldBossDefeated;

            foreach (var source in FindSceneBehaviours().OfType<IEnemyCombatSource>().Distinct())
            {
                source.DeathObserved += ReportStageEnemyDeath;
                _stageCombatSources.Add(source);
            }
        }

        private void ReleaseStageCombatSources()
        {
            var errors = new List<Exception>();
            RunCleanup(() => _finalBossTransition?.Dispose(), errors);
            _finalBossTransition = null;
            foreach (var source in _stageCombatSources)
                RunCleanup(() => source.DeathObserved -= ReportStageEnemyDeath, errors);
            _stageCombatSources.Clear();
            if (_stageCheckpoints != null)
            {
                if (_coordinator?.StageProgression != null)
                    _coordinator.StageProgression.FieldBossDefeated -= _stageCheckpoints.RecordFieldBossDefeated;
                RunCleanup(_stageCheckpoints.Dispose, errors);
                _stageCheckpoints = null;
            }
            if (errors.Count > 0) throw new AggregateException("Stage 전환·전투 원본을 해제하는 중 오류가 발생했습니다.", errors);
        }

        private Task<CheckpointOperationResult> SaveStageCheckpointAsync(CheckpointSlot slot, CancellationToken cancellation)
        {
            var service = Checkpoints;
            return service != null ? service.SaveAsync(slot, cancellation) : Task.FromResult(new CheckpointOperationResult(
                Guid.NewGuid(), CheckpointOperationStatus.Rejected, "CheckpointUnavailable", false));
        }

        /// <summary>원인을 해결한 실패 UI/Debugger가 같은 Stage 저장 요청만 재시도합니다. 안전장치를 차감하거나 보스를 대체하지 않습니다.</summary>
        public void RetryStageCheckpoints()
        {
            if (!HasCurrentMatch || Recovery?.IsRecoveryPending == true) return;
            _stageCheckpoints?.RetryFailed();
        }
        /// <summary>실제 저장을 마친 전환 범위 안에서 Boss Scene 조립부가 보스·전원 도착·연출 완료 뒤 호출합니다.</summary>
        public void ConfirmFinalBossReady(MatchId match, EntityId actor)
        {
            if (!HasCurrentMatch || match != _ownedMatchId) throw new InvalidOperationException("Use the current Host Stage.");
            if (_finalBossTransition == null) throw new InvalidOperationException("PrepareFinalBossTransitionAsync의 저장 완료 범위가 필요합니다.");
            _finalBossTransition.ConfirmReady(actor);
            _finalBossTransition = null;
        }
        /// <summary>Enemy 권위 HP 원본에서 확인한 실제 죽음만 전달합니다. UI 요청·Despawn을 사망 사실로 제출하지 않습니다.</summary>
        public void ReportStageEnemyDeath(MatchId match, EntityId actor)
        {
            if (HasCurrentMatch && match == _ownedMatchId) _coordinator.StageProgression?.ObserveEnemyDeath(actor);
        }
    }
}
