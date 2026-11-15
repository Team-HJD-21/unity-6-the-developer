// 실제 Player 성장 원본과 개인 저장 슬롯을 연결합니다. 월드 rollback은 이 파일을 되감지 않습니다.
// 팀원의 UI는 기존 성장 API를 그대로 사용하며, Source의 실제 변경 알림만 저장 요청으로 변환합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using UnityEngine;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class InitialStageCompositionRoot
    {
        private readonly List<GrowthSaveService> _growthSaves = new();
        private readonly HashSet<GrowthSaveService> _reportedGrowthErrors = new();
        public IReadOnlyList<GrowthSaveService> GrowthSaves => _growthSaves.AsReadOnly();

        private void BindGrowthSaves(TeamHJD.Game.Domain.PlayerId authorityPlayer)
        {
            if (_matchHost is not AppRoot app || app.Services.CheckpointStore == null) return;
            var session = _matchSession; var time = _matchTime;
            foreach (var pair in _playerActors.Actors)
            {
                var owners = pair.Value.GetComponentsInChildren<MonoBehaviour>(true).OfType<IGrowthCheckpointParticipant>().ToArray();
                if (owners.Length == 0) continue;
                var scope = new GrowthSaveService(app.Services.CheckpointStore, app.Services.CheckpointPayloads,
                    GrowthSaveService.SlotForOwner("player/" + pair.Key.Value),
                    () => new CheckpointCaptureContext(session.Config, time.Clock.ElapsedSeconds, time.Clock.Snapshot.Epoch), owners);
                try { scope.Load(); _growthSaves.Add(scope); }
                catch { scope.Dispose(); throw; }
            }
            var shared = new GrowthSaveService(app.Services.CheckpointStore, app.Services.CheckpointPayloads,
                GrowthSaveService.SlotForOwner("field/" + authorityPlayer.Value + "/" + app.CampaignSlot.Value + "/" + session.Config.StageId.Value),
                () => new CheckpointCaptureContext(session.Config, time.Clock.ElapsedSeconds, time.Clock.Snapshot.Epoch),
                new[] { _turretBinding.Controller.CreateGrowthParticipant() });
            try { shared.Load(); _growthSaves.Add(shared); }
            catch { shared.Dispose(); throw; }
            // 구조/안전장치는 성장 파일 schema에 섞지 않습니다. 이전 성장 저장의 source 집합을 그대로 보존합니다.
            var recovery = new GrowthSaveService(app.Services.CheckpointStore, app.Services.CheckpointPayloads,
                GrowthSaveService.SlotForOwner("recovery/" + authorityPlayer.Value + "/" + app.CampaignSlot.Value + "/" + session.Config.StageId.Value),
                () => new CheckpointCaptureContext(session.Config, time.Clock.ElapsedSeconds, time.Clock.Snapshot.Epoch),
                new[] { new MissionRecoveryParticipant(_recoveryLedger) }, 1);
            try
            {
                recovery.Load(); _growthSaves.Add(recovery);
                app.BindMissionRecovery(this, _recoveryLedger);
                // 초기 실행 ID도 디스크에 확정해야 저장 없는 복귀가 같은 실행을 인수할 수 있습니다.
                recovery.Flush();
            }
            catch { if (!_growthSaves.Contains(recovery)) recovery.Dispose(); throw; }
        }

        private void PublishGrowthRestoreWarnings()
        {
            var warnings = _turretBinding?.Controller?.GrowthRestoreWarnings;
            if (warnings != null && warnings.Count > 0)
                GameAlerts.Show(string.Join("\n", warnings.Take(3)), AlertTone.Info, "growth/power-restored-off");
        }

        private void PumpGrowthSaves()
        {
            foreach (var scope in _growthSaves)
            {
                scope.Pump(Time.realtimeSinceStartupAsDouble);
                if (scope.LastError != null && _reportedGrowthErrors.Add(scope))
                    Debug.LogError("성장 저장 실패: " + scope.LastError.Message + ". 파일 원본을 보존했습니다. 재시도 전 저장소를 확인하세요.", this);
                else if (scope.LastError == null) _reportedGrowthErrors.Remove(scope);
            }
        }

        /// <summary>호출자는 정상 종료/월드 복귀 전에 사용합니다. 저장 실패를 복귀 성공으로 바꾸지 않습니다.</summary>
        public void FlushGrowthSaves()
        {
            foreach (var scope in _growthSaves) scope.Flush();
        }
        private void ReleaseGrowthSaves()
        {
            var errors = new List<Exception>();
            foreach (var scope in _growthSaves)
                try { scope.Dispose(); } catch (Exception error) { errors.Add(error); }
            _growthSaves.Clear(); _reportedGrowthErrors.Clear();
            if (errors.Count > 0) throw new AggregateException("성장 저장을 완료하지 못했습니다.", errors);
        }
    }
}
