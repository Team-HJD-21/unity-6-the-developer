// 현재 Scene의 확정 점유율을 Stage 진행에 전달하고 실제 Boss Feature의 준비/사망을 연결합니다.
// Boss 콘텐츠가 없으면 미연결 이유를 표시하며 임의 대체 생성이나 승리 처리를 하지 않습니다.
using System;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class InitialStageCompositionRoot
    {
        public StageProgressSnapshot StageProgress => _coordinator?.StageProgression?.Snapshot;
        private void BindStageProgression()
        {
            var sources = FindSceneBehaviours().OfType<IFieldBossSpawnService>().ToList();
            if (_encounterConsumer is IFieldBossSpawnService encounter && !sources.Contains(encounter)) sources.Add(encounter);
            if (sources.Count > 1) throw new InvalidOperationException("Keep one Field boss spawn owner per Stage.");
            _coordinator.ConfigureStageProgression(_recoveryLedger.RunId, _matchTime.FieldClock, sources.SingleOrDefault());
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
