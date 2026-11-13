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
        private bool _retryingRecovery;
        public string LastMissionRecoveryError { get; private set; }
        public bool CanRetryMissionRecovery => !_retryingRecovery && _activeMission == null &&
            !string.IsNullOrEmpty(_recoveryScene) && _expectedRecoveryFailure != Guid.Empty;

        internal void PrepareMissionEntry(MissionEntryKind entry)
        {
            _missionEntry = entry;
            if (entry == MissionEntryKind.NewMission)
            {
                _expectedMissionRun = Guid.Empty; _expectedRecoveryFailure = Guid.Empty;
                _recoveryScene = null; LastMissionRecoveryError = null;
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
            _expectedMissionRun = run; _expectedRecoveryFailure = Guid.Empty;
        }
        internal async Task RetryMissionForRecoveryAsync(string scene, Guid run, Guid failure)
        {
            ThrowIfUnavailable();
            if (run == Guid.Empty || failure == Guid.Empty) throw new ArgumentException("Retry only an identified failure.");
            _expectedMissionRun = run; _expectedRecoveryFailure = failure; _recoveryScene = scene;
            PrepareMissionEntry(MissionEntryKind.Retry);
            _retryingRecovery = true; LastMissionRecoveryError = null;
            // 이 Task의 수명은 App입니다. 이전 Scene의 gameplay 토큰으로 자기 전환을 취소하지 않습니다.
            try
            {
                if (GameFlow != null) await GameFlow.RetryMissionAsync(scene);
                else
                {
                    await ReplaceGameplaySceneAsync(scene);
                    await new AppGameFlowHost(this).WaitForMissionReadyAsync(scene, CancellationToken.None);
                }
                if (_activeMission == null || _activeMission.Recovery?.Ledger.RunId != run ||
                    _activeMission.Recovery.IsRecoveryPending)
                    throw new InvalidOperationException("The replacement Mission did not complete its recovery arrival.");
                _recoveryScene = null; _expectedRecoveryFailure = Guid.Empty;
            }
            catch (Exception error) { LastMissionRecoveryError = error.Message; throw; }
            finally { _retryingRecovery = false; }
        }
        /// <summary>실패 UI/Debugger가 Scene 조립 실패 후 사용합니다. 이미 지출한 같은 실패를 재시도하며 새 실행을 만들지 않습니다.</summary>
        public Task RetryPendingMissionRecoveryAsync()
        {
            if (!CanRetryMissionRecovery) throw new InvalidOperationException("No failed Mission recovery arrival can be retried.");
            return RetryMissionForRecoveryAsync(_recoveryScene, _expectedMissionRun, _expectedRecoveryFailure);
        }
        private Exception FlushMissionGrowthBeforeEnd()
        {
            try { if (_activeMission != null) _activeMission.FlushGrowthSaves(); return null; }
            catch (Exception error) { return error; }
        }
    }
}
