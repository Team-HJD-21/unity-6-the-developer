// 저장 메타데이터를 읽고 새 Mission 구성과 실제 Feature 복원을 조율합니다.
// 이전 Match/NGO 소유권은 재사용하지 않습니다. 자동 저장 사건은 이 파일의 책임이 아닙니다.
// 첫 연결은 1인 Story 복원이며 2인 재접속은 별도 참가자 계약이 필요합니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Bootstrap
{
    public sealed partial class AppRoot
    {
        private CheckpointArrival _checkpointArrival;
        private bool _readingCheckpoint;
        public bool IsResumingCheckpoint => _readingCheckpoint || _checkpointArrival != null;
        public CheckpointOperationResult LastCheckpointResume
        {
            get; private set;
        }

        /// <summary>시작/허브 UI가 슬롯과 제작 Mission 경로를 명시합니다. writer 누락이나 복원 실패는 입장 성공으로 보고하지 않습니다.</summary>
        public async Task<CheckpointOperationResult> ResumeCheckpointAsync(CheckpointSlot slot, string missionScene,
            CheckpointRestorePolicy policy = null, CancellationToken cancellation = default)
        {
            ThrowIfUnavailable();
            if (GameFlow == null)
                throw new InvalidOperationException("Configure the App game flow before resuming a Mission.");
            if (IsResumingCheckpoint || GameFlow.IsTransitioning)
                throw new InvalidOperationException("A checkpoint or Scene transition is already running.");
            string restriction = GameFlowTravelRestriction;
            if (!string.IsNullOrEmpty(restriction))
                throw new InvalidOperationException(restriction);
            if (!Services.SceneFlow.CanLoad(missionScene))
                throw new ArgumentException("Enable the checkpoint Mission Scene in Build Settings.", nameof(missionScene));
            Guid operation = Guid.NewGuid();
            LastCheckpointResume = null;
            _readingCheckpoint = true;
            CheckpointArrival arrival = null;
            try
            {
                var data = await ReadCheckpointAsync(slot, cancellation);
                cancellation.ThrowIfCancellationRequested();
                ThrowIfUnavailable();
                if (data == null)
                    return LastCheckpointResume = new CheckpointOperationResult(operation, CheckpointOperationStatus.NotFound, "SlotNotFound", true);
                if (data.ParticipantCount != 1 || data.Mode != MatchMode.Story)
                    throw new NotSupportedException("Cooperative resume requires the agreed participant/rejoin contract.");
                arrival = new CheckpointArrival(data, missionScene, policy ?? CheckpointRestorePolicy.Default, cancellation);
                _checkpointArrival = arrival;
                await GameFlow.ResumeMissionAsync(missionScene, cancellation);
                return LastCheckpointResume = arrival.Result ?? throw new InvalidOperationException("The Mission did not consume its checkpoint arrival.");
            }
            catch (Exception error)
            {
                // Single Scene 교체 이후 취소/실패해도 입장 도중 만든 Match를 남기지 않습니다.
                // 이전 Match가 아니라 이 복원 작업이 구성한 ID만 정리합니다.
                if (arrival != null && !arrival.TargetMatchId.IsEmpty)
                {
                    try
                    {
                        EndFailedMissionArrival(arrival.TargetMatchId);
                    }
                    catch (Exception cleanup) { error = new AggregateException("Checkpoint arrival cleanup failed.", error, cleanup); }
                }
                return LastCheckpointResume = error is OperationCanceledException
                    ? new CheckpointOperationResult(operation, CheckpointOperationStatus.Cancelled, error.Message, false, error: error)
                    : arrival?.Result ?? new CheckpointOperationResult(operation, CheckpointOperationStatus.Rejected,
                        error.Message, false, error: error);
            }
            finally
            {
                if (ReferenceEquals(_checkpointArrival, arrival))
                    _checkpointArrival = null;
                _readingCheckpoint = false;
            }
        }

        /// <summary>Scene의 local/NGO 준비 조립부가 첫 Match 생성 전에 호출합니다. 새 ID와 현재 접속자는 유지하고 저장된 실행 입력을 적용합니다.</summary>
        internal MatchConfig ConfigureCheckpointArrival(string scenePath, MatchConfig authored)
        {
            var arrival = _checkpointArrival;
            if (arrival == null)
                return authored;
            var saved = arrival.Data;
            if (arrival.Scene != scenePath || authored.Players.Count != saved.ParticipantCount || authored.Mode != saved.Mode ||
                authored.StageId != saved.StageId || authored.ContentVersion != saved.ContentVersion || authored.DifficultyProfileId != saved.DifficultyProfileId)
                throw new InvalidOperationException("The chosen Mission, participants or content version do not match the checkpoint.");
            // 새 actor가 만들어지기 전에 해당 월드가 참조하는 최신 Host 성장 슬롯을 선택합니다.
            _campaignSlot = TeamHJD.Game.Application.CampaignCheckpointParticipant.Read(saved, Services.CheckpointPayloads);
            ExpectCheckpointRun(TeamHJD.Game.Application.CampaignCheckpointParticipant.ReadRun(saved, Services.CheckpointPayloads));
            arrival.TargetMatchId = authored.MatchId;
            return new MatchConfig(authored.MatchId, saved.Mode, saved.StageId, saved.DifficultyProfileId,
                saved.ContentVersion, saved.RandomSeed, authored.Players);
        }

        internal void ApplyCheckpointArrival(InitialStageCompositionRoot scene)
        {
            var arrival = _checkpointArrival;
            if (arrival == null)
                return;
            if (scene.gameObject.scene.path != arrival.Scene || arrival.Result != null || scene.Checkpoints == null)
                throw new InvalidOperationException("The current Mission cannot accept this checkpoint arrival.");
            arrival.Result = scene.Checkpoints.Restore(arrival.Data, arrival.Policy, arrival.Cancellation);
            if (arrival.Result.Status != CheckpointOperationStatus.Restored)
                throw new InvalidOperationException("Checkpoint arrival failed: " + arrival.Result.Reason, arrival.Result.Error);
        }

        private sealed class CheckpointArrival
        {
            public readonly CheckpointData Data;
            public readonly string Scene;
            public readonly CheckpointRestorePolicy Policy;
            public readonly CancellationToken Cancellation;
            public MatchId TargetMatchId;
            public CheckpointOperationResult Result;
            public CheckpointArrival(CheckpointData data, string scene, CheckpointRestorePolicy policy, CancellationToken cancellation)
            {
                Data = data;
                Scene = scene;
                Policy = policy;
                Cancellation = cancellation;
            }
        }
    }
}
