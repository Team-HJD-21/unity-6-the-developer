// Scene을 교체해도 같은 Mission의 복귀 식별자를 유지합니다. HP나 복원 내용을 직접 수정하지 않습니다.
// 실제 writer/디스크 완료와 새 Scene의 준비 장벽을 통과한 뒤에만 복귀 성공으로 처리합니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Bootstrap
{
    public sealed partial class AppRoot
    {
        private InitialStageCompositionRoot _activeMission;
        private MissionEntryKind _missionEntry = MissionEntryKind.NewMission;
        private Guid _expectedMissionRun;
        private Guid _expectedRecoveryFailure;
        private string _recoveryScene;
        private CheckpointSlot _recoveryCheckpoint;
        private Guid _recoveryCheckpointId;
        private Guid _recoveryAttempt;
        /// <summary>현재 복귀 시도의 기술 오류입니다. 사용자 화면은 짧게 안내하고 원문은 진단/인계에 사용합니다.</summary>
        public string LastMissionRecoveryError { get; private set; }
        /// <summary>같은 실패를 재시도할 수 있는지 표시합니다. 실제 저장 유효성은 실행할 때 다시 검사합니다.</summary>
        public bool CanRetryMissionRecovery => _recoveryAttempt == Guid.Empty &&
            (_activeMission != null
                ? !_activeMission.IsRecovering && _activeMission.Recovery?.IsRecoveryPending == true
                : !string.IsNullOrEmpty(_recoveryScene) && _expectedRecoveryFailure != Guid.Empty);

        internal void PrepareMissionEntry(MissionEntryKind entry)
        {
            _missionEntry = entry;
            if (entry != MissionEntryKind.Retry) _recoveryAttempt = Guid.Empty;
            if (entry == MissionEntryKind.NewMission)
            {
                _expectedMissionRun = Guid.Empty; _expectedRecoveryFailure = Guid.Empty;
                CompleteMissionRecovery();
            }
        }
        internal void BindMissionRecovery(InitialStageCompositionRoot scene, MissionRecoveryLedger ledger)
        {
            if (_activeMission != null && _activeMission != scene)
                throw new InvalidOperationException("Release the previous Mission before binding recovery.");
            if (_missionEntry == MissionEntryKind.NewMission) ledger.BeginNewMission();
            else if (ledger.IsEnded || _expectedMissionRun == Guid.Empty || ledger.RunId != _expectedMissionRun ||
                (_expectedRecoveryFailure != Guid.Empty && ledger.PendingFailure != _expectedRecoveryFailure))
                throw new InvalidOperationException("The durable recovery ledger does not match this Mission arrival.");
            _activeMission = scene;
        }
        internal void UnbindMissionRecovery(InitialStageCompositionRoot scene)
        {
            if (_activeMission == scene) _activeMission = null;
        }
        internal void ExpectCheckpointRun(Guid run)
        {
            if (run == Guid.Empty) throw new ArgumentException("A saved Mission run is required.");
            _expectedMissionRun = run;
            if (_missionEntry != MissionEntryKind.Retry) _expectedRecoveryFailure = Guid.Empty;
        }
        internal void RememberMissionRecovery(string scene, MissionRecoveryLedger ledger)
        {
            _recoveryScene = scene; _expectedMissionRun = ledger.RunId; _expectedRecoveryFailure = ledger.PendingFailure;
            _recoveryCheckpoint = ledger.LastCheckpoint; _recoveryCheckpointId = ledger.LastCheckpointId;
        }
        internal void ReportMissionRecoveryError(Guid failure, Exception error)
        {
            if (_expectedRecoveryFailure == failure) LastMissionRecoveryError = error.Message;
        }
        internal void CompleteMissionRecovery()
        {
            _recoveryScene = null; _expectedRecoveryFailure = Guid.Empty; LastMissionRecoveryError = null;
            _recoveryCheckpoint = default; _recoveryCheckpointId = Guid.Empty;
        }
        internal async Task RetryMissionForRecoveryAsync(string scene, Guid run, Guid failure)
        {
            ThrowIfUnavailable();
            if (run == Guid.Empty || failure == Guid.Empty) throw new ArgumentException("Retry only an identified failure.");
            _expectedMissionRun = run; _expectedRecoveryFailure = failure; _recoveryScene = scene;
            PrepareMissionEntry(MissionEntryKind.Retry);
            Guid attempt = _recoveryAttempt = Guid.NewGuid();
            LastMissionRecoveryError = null;
            CheckpointArrival arrival = null;
            using var lifetime = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);
            lifetime.CancelAfter(TimeSpan.FromSeconds(30));
            // 이 Task의 수명은 App입니다. 이전 Scene의 gameplay 토큰으로 자기 전환을 취소하지 않습니다.
            try
            {
                if (!_recoveryCheckpoint.IsEmpty)
                {
                    if (_checkpointArrival != null) throw new InvalidOperationException("Another checkpoint arrival is still active.");
                    var data = await ReadCheckpointAsync(_recoveryCheckpoint, lifetime.Token);
                    if (_recoveryAttempt != attempt || _expectedMissionRun != run || _expectedRecoveryFailure != failure)
                        throw new OperationCanceledException("A newer Mission entry superseded this recovery.");
                    if (data == null || data.CheckpointId != _recoveryCheckpointId)
                        throw new InvalidOperationException("The committed safety checkpoint is missing or was replaced.");
                    arrival = new CheckpointArrival(data, scene, CheckpointRestorePolicy.Default, lifetime.Token);
                    _checkpointArrival = arrival;
                }
                if (GameFlow != null) await GameFlow.RetryMissionAsync(scene, lifetime.Token);
                else
                {
                    await ReplaceGameplaySceneAsync(scene, lifetime.Token);
                    await new AppGameFlowHost(this).WaitForMissionReadyAsync(scene, lifetime.Token);
                }
                if (_activeMission == null || _activeMission.Recovery?.Ledger.RunId != run ||
                    _activeMission.Recovery.IsRecoveryPending)
                    throw new InvalidOperationException("The replacement Mission did not complete its recovery arrival.");
                CompleteMissionRecovery();
            }
            catch (Exception error)
            {
                // 이전 IO가 늦게 실패해도 그 사이 시작한 새 Mission의 UI를 덮지 않습니다.
                ReportMissionRecoveryError(failure, error);
                throw;
            }
            finally
            {
                if (arrival != null && ReferenceEquals(_checkpointArrival, arrival)) _checkpointArrival = null;
                if (_recoveryAttempt == attempt) _recoveryAttempt = Guid.Empty;
            }
        }
        /// <summary>실패 UI/Debugger의 공통 진입점입니다. Scene이 살아 있거나 닫혔어도 같은 실패를 추가 차감 없이 재시도합니다.</summary>
        public async Task RetryPendingMissionRecoveryAsync()
        {
            if (!CanRetryMissionRecovery) throw new InvalidOperationException("No failed Mission recovery arrival can be retried.");
            if (_activeMission != null)
            {
                await _activeMission.RetryRecoveryAsync();
                if (LastMissionRecoveryError != null)
                    throw new InvalidOperationException(LastMissionRecoveryError);
            }
            else await RetryMissionForRecoveryAsync(_recoveryScene, _expectedMissionRun, _expectedRecoveryFailure);
        }
        private Exception FlushMissionGrowthBeforeEnd()
        {
            try { if (_activeMission != null) _activeMission.FlushGrowthSaves(); return null; }
            catch (Exception error) { return error; }
        }
    }
}
