// 실제 Host Player를 구조 원본으로 등록하고 전멸 후 월드 복귀를 조율합니다.
// CU 패배는 기존 Coordinator가 먼저 확정합니다. 비환급 기록은 월드 rollback과 분리합니다.
// 저장이 없으면 같은 실행의 시작 상태를 다시 열며 기술 실패는 정지 상태에서 재시도합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class InitialStageCompositionRoot
    {
        [SerializeField] private MissionRecoverySettings rescueSettings = new();
        private MissionRecoveryLedger _recoveryLedger;
        private IDisposable _recoveryPause;
        private bool _recoveryBusy;
        public MatchRecovery Recovery => _coordinator?.Recovery;
        public string RecoveryError { get; private set; }
        public bool IsRecovering => _recoveryBusy;

        private IDisposable RegisterRescueActor(PlayerId player, GameObject actor, bool localInput)
        {
            var service = Recovery ?? throw new InvalidOperationException("Configure recovery before actor registration.");
            var sources = actor.GetComponentsInChildren<MonoBehaviour>(true).OfType<IPlayerRescueActor>().ToArray();
            if (sources.Length != 1) throw new InvalidOperationException("Each authority Player requires one rescue source.");
            service.Register(player, sources[0]);
            var consumers = localInput ? actor.GetComponentsInChildren<MonoBehaviour>(true).OfType<IPlayerRescueConsumer>().ToArray()
                : Array.Empty<IPlayerRescueConsumer>();
            try { foreach (var consumer in consumers) consumer.BindRescue(player, service); }
            catch { service.Unregister(player); throw; }
            return new RescueActorLease(service, player, consumers);
        }
        private readonly List<IDisposable> _rescueActorLeases = new();
        private void BindRecoveryActors(PlayerId local)
        {
            foreach (var pair in _playerActors.Actors)
                _rescueActorLeases.Add(RegisterRescueActor(pair.Key, pair.Value, pair.Key == local));
            Recovery.Failure += OnRecoveryFailure;
        }
        private void FinishRecoveryArrival()
        {
            if (_recoveryLedger.PendingFailure == Guid.Empty) return;
            Guid failure = _recoveryLedger.PendingFailure;
            Recovery.AdoptPendingRecovery(failure);
            Recovery.CompleteRecovery(failure);
            FlushGrowthSaves();
            (_matchHost as AppRoot)?.CompleteMissionRecovery();
        }
        private void PumpRecovery()
        {
            var service = Recovery;
            if (service == null) return;
            var saved = Checkpoints?.LastSavedData;
            if (saved != null && !service.IsRecoveryPending)
                _recoveryLedger.RecordCheckpoint(Checkpoints.LastSavedSlot, saved.CheckpointId);
            service.Pump();
        }
        private async void OnRecoveryFailure(Guid failure, bool allowed, string reason)
        {
            if (!allowed)
            {
                _coordinator.CompleteMatch(MatchOutcome.Defeat);
                return;
            }
            await RetryRecoveryAsync();
        }
        /// <summary>Game Debugger/실패 UI가 기술 실패 후 같은 복귀를 재시도합니다. 안전장치를 추가 차감하지 않습니다.</summary>
        public async Task RetryRecoveryAsync()
        {
            if (_recoveryBusy || Recovery?.IsRecoveryPending != true) return;
            var service = Recovery;
            var app = _matchHost as AppRoot;
            Guid run = service.Ledger.RunId, failure = service.Ledger.PendingFailure;
            app?.RememberMissionRecovery(gameObject.scene.path, service.Ledger);
            _recoveryBusy = true; RecoveryError = null;
            _recoveryPause ??= _matchTime.AcquirePause("MissionRecovery");
            try
            {
                FlushGrowthSaves();
                await LeaveFinalBattleForRecoveryAsync();
                if (!service.Ledger.LastCheckpoint.IsEmpty)
                {
                    if (app == null) throw new InvalidOperationException("Checkpoint recovery requires its App store owner.");
                    var checkpoints = Checkpoints;
                    var gameplay = _coordinator.GameplayCancellation;
                    var data = await app.ReadCheckpointAsync(service.Ledger.LastCheckpoint, gameplay);
                    gameplay.ThrowIfCancellationRequested();
                    if (this == null || !HasCurrentMatch || Checkpoints != checkpoints)
                        throw new OperationCanceledException("The recovery Scene ended while reading its checkpoint.");
                    if (data == null || data.CheckpointId != service.Ledger.LastCheckpointId)
                        throw new InvalidOperationException("The committed safety checkpoint is missing or was replaced.");
                    var result = checkpoints.Restore(data, cancellation: gameplay);
                    if (result.Status != CheckpointOperationStatus.Restored)
                        throw new InvalidOperationException("Recovery restore failed: " + result.Reason, result.Error);
                    service.CompleteRecovery(failure);
                    FlushGrowthSaves();
                    app?.CompleteMissionRecovery();
                    _recoveryPause.Dispose(); _recoveryPause = null;
                }
                else
                {
                    if (app == null) throw new InvalidOperationException("Scene retry requires its App owner.");
                    await app.RetryMissionForRecoveryAsync(gameObject.scene.path, run, failure);
                }
            }
            catch (Exception error)
            {
                // 기술 오류를 새 전멸이나 성공으로 바꾸지 않습니다. 원본과 정지를 유지하고 재시도 이유를 노출합니다.
                if (this != null) RecoveryError = error.Message;
                app?.ReportMissionRecoveryError(failure, error);
                Debug.LogError("안전장치 복귀 중단: " + error.Message);
            }
            finally { if (this != null) _recoveryBusy = false; }
        }
        private void ReleaseRecovery()
        {
            if (Recovery != null) Recovery.Failure -= OnRecoveryFailure;
            foreach (var lease in _rescueActorLeases) lease.Dispose();
            _rescueActorLeases.Clear();
            _recoveryPause?.Dispose(); _recoveryPause = null;
            if (_matchHost is AppRoot app) app.UnbindMissionRecovery(this);
        }
        private sealed class RescueActorLease : IDisposable
        {
            private MatchRecovery _service;
            private readonly PlayerId _player;
            private readonly IPlayerRescueConsumer[] _consumers;
            public RescueActorLease(MatchRecovery service, PlayerId player, IPlayerRescueConsumer[] consumers)
            { _service = service; _player = player; _consumers = consumers; }
            public void Dispose()
            {
                if (_service == null) return;
                foreach (var consumer in _consumers) consumer.UnbindRescue();
                _service.Unregister(_player); _service = null;
            }
        }
    }
}
