// 현재 Host Match에 실제 writer·필수 목록·명시 저장 요청을 조립합니다.
// 범용 API는 저장 시점을 선택하지 않습니다. Stage 진행의 전용 관찰자는 명시 저장을 요청합니다.
// 누락 writer가 있으면 전체 저장/복원을 거부합니다.
// API/진단은 이 파일, Feature 원본 캡처와 적용은 각 Feature의 .Checkpoint에 있습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class InitialStageCompositionRoot
    {
        private MatchCheckpointScope _checkpointScope;
        private bool _restoringCheckpoint;
        /// <summary>현재 Host의 실행 API입니다. Scene/Match 해제 후 이전 참조를 재사용하지 않습니다.</summary>
        public CheckpointService Checkpoints => _checkpointScope?.Service;
        /// <summary>기획이 정한 사건의 관찰자가 저장/복원 의도를 Publish합니다. 범용 저장기는 발동 시점을 결정하지 않습니다.</summary>
        public CheckpointRequestChannel CheckpointRequests => _checkpointScope?.Requests;
        public IReadOnlyList<string> MissingCheckpointParticipants => Checkpoints?.MissingParticipantKeys ?? Array.Empty<string>();

        private void BindCheckpoints()
        {
            if (_checkpointScope != null)
                throw new InvalidOperationException("A checkpoint scope already exists.");
            if (!(_matchHost is AppRoot app))
                return; // 독립 조립 검사에는 App 파일 저장소를 강제하지 않습니다.
            var session = _matchSession;
            var coordinator = _coordinator;
            MatchId match = session.Config.MatchId;
            var required = new HashSet<string>(StringComparer.Ordinal)
            {
                "core/random", "world/spatial", "world/region-start", "world/control-unit", "world/field-turrets", "world/enemies", "world/encounter"
            };
            if (_weatherSource != null)
                required.Add("world/weather");
            if (_timeOfDaySource != null)
                required.Add("world/time-of-day");
            for (int slot = 0; slot < session.Config.Players.Count; slot++)
            {
                string prefix = "world/player/" + slot;
                required.Add(prefix + "/actor");
                required.Add(prefix + "/weapon");
                required.Add(prefix + "/loadout");
                required.Add(prefix + "/movement-effects");
            }
            required.Add("world/campaign");
            var participants = FindSceneBehaviours().OfType<ICheckpointParticipant>().ToList();
            if (_encounterConsumer is ICheckpointParticipantProvider provider)
                foreach (var writer in provider.GetCheckpointParticipants())
                    if (!participants.Contains(writer))
                        participants.Add(writer);
            if (coordinator.EncounterProduction != null)
            {
                required.Add("world/encounter-production");
                participants.Add(new EncounterProductionCheckpointParticipant(match, coordinator.EncounterProduction));
            }
            required.Add("world/field-time");
            required.Add("world/stage-progress");
            participants.Add(new StageProgressCheckpointParticipant(coordinator.StageProgression));
            participants.Add(new FieldTimeCheckpointParticipant(_matchTime.FieldClock));
            participants.Add(new CampaignCheckpointParticipant(app.CampaignSlot, _recoveryLedger.RunId));
            participants.Add(session.CreateRandomCheckpointParticipant());
            participants.Add(new MatchSpatialCheckpointParticipant(session, _regionBinding.RestoreCheckpoint));
            var runtime = new MatchCheckpointRuntime(session, _matchTime,
                () => HasCurrentMatch && _matchSession == session,
                () => coordinator.IsGameplayRunning,
                coordinator.GameplayCancellation,
                () => { FlushGrowthSaves(); _restoringCheckpoint = true; },
                FinishCheckpointRestore,
                error =>
                {
                    // 기술적 복원 실패를 전투 패배/보상으로 바꾸지 않습니다. 잘못된 월드로 진행을 계속하지 않습니다.
                    _restoringCheckpoint = false;
                    Debug.LogException(error, this);
                    if (!app.TryEndMatch(match))
                        throw new InvalidOperationException("The faulted Match could not be released.", error);
                }, () => Recovery?.CanCaptureWorld == true && AllowsFieldCheckpointWorld, () => AllowsFieldCheckpointWorld);
            _checkpointScope = app.CreateCheckpointScope(runtime, participants, required);
        }

        /// <summary>런타임 Feature writer 생성자가 원본 등록 후 호출합니다. 교체/Despawn 시 반환 lease를 해제합니다.</summary>
        public IDisposable RegisterCheckpointParticipant(ICheckpointParticipant participant)
            => _checkpointScope?.Register(participant) ?? throw new InvalidOperationException("No Host checkpoint scope exists.");

        private IDisposable RegisterActorCheckpointScope(PlayerId player, GameObject actor)
        {
            if (_fieldScope == null)
                throw new InvalidOperationException("No authority field scope exists.");
            var leases = new List<IDisposable> { _fieldScope.RegisterActor(player, actor) };
            try
            {
                leases.Add(RegisterRescueActor(player, actor, false));
                if (_checkpointScope != null)
                    foreach (var writer in actor.GetComponentsInChildren<MonoBehaviour>(true).OfType<ICheckpointParticipant>())
                        leases.Add(_checkpointScope.Register(writer));
                return new ActorCheckpointLease(leases);
            }
            catch
            {
                new ActorCheckpointLease(leases).Dispose();
                throw;
            }
        }

        private void FinishCheckpointRestore()
        {
            _regionBinding.RefreshDynamicAfterRestore();
            _restoringCheckpoint = false;
            OnTurretLayoutChanged();
            RefreshDynamicInput();
            (_environmentScope as MatchEnvironmentSceneScope)?.PublishCurrent();
            _nextBattlefieldSampleTime = 0;
            PublishGrowthRestoreWarnings();
            Recovery?.OnWorldRestored();
        }

        private void ReleaseCheckpoints()
        {
            var scope = _checkpointScope;
            _checkpointScope = null;
            _restoringCheckpoint = false;
            scope?.Dispose();
        }

        private sealed class ActorCheckpointLease : IDisposable
        {
            private List<IDisposable> _leases;
            public ActorCheckpointLease(List<IDisposable> leases) => _leases = leases;
            public void Dispose()
            {
                var leases = _leases;
                _leases = null;
                if (leases == null)
                    return;
                var errors = new List<Exception>();
                for (int index = leases.Count - 1; index >= 0; index--)
                    try
                    {
                        leases[index].Dispose();
                    }
                    catch (Exception error) { errors.Add(error); }
                if (errors.Count > 0)
                    throw new AggregateException("Player registrations could not all be released.", errors);
            }
        }
    }
}
