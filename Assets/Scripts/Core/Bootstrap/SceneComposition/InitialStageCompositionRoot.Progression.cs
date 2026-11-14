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
            foreach (var source in _stageCombatSources)
                source.DeathObserved -= ReportStageEnemyDeath;
            _stageCombatSources.Clear();
            if (_stageCheckpoints != null)
            {
                if (_coordinator?.StageProgression != null)
                    _coordinator.StageProgression.FieldBossDefeated -= _stageCheckpoints.RecordFieldBossDefeated;
                _stageCheckpoints.Dispose();
                _stageCheckpoints = null;
            }
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
        /// <summary>같은 Match의 실제 Boss Scene 조립부가 보스와 도착·연출 준비를 마친 뒤 호출합니다. 예전 Field만 보류합니다.</summary>
        public void ConfirmFinalBossReady(MatchId match, EntityId actor)
        {
            if (!HasCurrentMatch || match != _ownedMatchId) throw new InvalidOperationException("Use the current Host Stage.");
            _coordinator.StageProgression.ConfirmFinalBossReady(actor);
            _matchTime.SetFieldHeld(true);
        }
        /// <summary>Enemy 권위 HP 원본에서 확인한 실제 죽음만 전달합니다. UI 요청·Despawn을 사망 사실로 제출하지 않습니다.</summary>
        public void ReportStageEnemyDeath(MatchId match, EntityId actor)
        {
            if (HasCurrentMatch && match == _ownedMatchId) _coordinator.StageProgression?.ObserveEnemyDeath(actor);
        }
    }
}
