// Stage1의 100% 점유부터 컷씬·보스전·안전장치·승리 귀환까지 조율합니다.
// 보스 전투는 기존 Enemy 원본, 표시는 IStageBossBattlePresentation에 위임합니다.
// 보스전 저장은 정지된 Field와 분리하여 보스·참가자의 실제 writer만 사용합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation;
using UnityEngine;
using EntityId = TeamHJD.Game.Domain.EntityId;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public enum StageBossBattlePhase { Field, Entrance, Preparing, Intro, Battle, Victory, Returning, Faulted }

    public sealed partial class InitialStageCompositionRoot
    {
        [SerializeField] private bool enableStage1BossFlow;
        private Task _bossBattleTask;
        private IStageBossBattlePresentation _bossPresentation;
        private MatchCheckpointScope _bossCheckpointScope;
        private EntityId _preparedBossId;
        private GameObject _bossActor;
        public StageBossBattlePhase BossBattlePhase { get; private set; }
        public string BossBattleError { get; private set; }
        public bool IsBossMusicActive => BossBattlePhase is StageBossBattlePhase.Intro or StageBossBattlePhase.Battle or StageBossBattlePhase.Victory;
        public CheckpointService BossStartCheckpoint => _bossCheckpointScope?.Service;

        // 이 Stage의 연출이 완료 결과를 대신 표시할 때만 App의 일반 결과 화면을 생략합니다.
        internal bool PresentsBossVictory(MatchCompletion completion) => enableStage1BossFlow &&
            BossBattlePhase == StageBossBattlePhase.Returning && completion.Result.MatchId == _ownedMatchId &&
            completion.Result.Outcome == MatchOutcome.Victory;

        private void PumpStageBossBattle()
        {
            if (!enableStage1BossFlow || !HasCurrentMatch || _bossBattleTask is { IsCompleted: false }) return;
            if (BossBattlePhase == StageBossBattlePhase.Field && StageProgress?.CanEnterFinalBattle == true && CanPrepareFinalBattle())
                _bossBattleTask = EnterStageBossBattleAsync();
            else if (BossBattlePhase == StageBossBattlePhase.Battle && StageProgress?.FinalBossDefeated == true)
                _bossBattleTask = FinishStageBossBattleAsync();
        }

        private async Task EnterStageBossBattleAsync()
        {
            var app = _matchHost as AppRoot;
            var match = _ownedMatchId;
            var cancellation = _coordinator.GameplayCancellation;
            BossBattlePhase = StageBossBattlePhase.Entrance; BossBattleError = null;
            try
            {
                _bossPresentation = FindSceneBehaviours().OfType<IStageBossBattlePresentation>().SingleOrDefault()
                    ?? throw new InvalidOperationException("Stage1 보스 연출 연결이 필요합니다.");
                using var pause = _matchTime.AcquirePause("BossEntrance");
                using var input = app.LocalInput.Acquire("BossEntrance");
                await _bossPresentation.PlayEntranceAsync(cancellation);
                Task intro = null;
                await app.FadeSceneTravelAsync(async () =>
                {
                    BossBattlePhase = StageBossBattlePhase.Preparing;
                    await EnterFinalBattleSceneAsync(match, cancellation);
                    _bossActor = FindPreparedBossActor();
                    BossBattlePhase = StageBossBattlePhase.Intro;
                    // UI는 암전 중 마지막 컷씬 화면을 해제한 뒤 기존 HUD 등장 연출을 시작합니다.
                    intro = _bossPresentation.ShowBattleIntroAsync(_bossActor, "감염된 트리언트", cancellation);
                }, cancellation);
                await intro;
                cancellation.ThrowIfCancellationRequested();
                await SaveBossStartAsync(app, cancellation);
                ConfirmFinalBossReady(match, _preparedBossId);
                BossBattlePhase = StageBossBattlePhase.Battle;
            }
            catch (Exception error)
            {
                if (cancellation.IsCancellationRequested) return;
                try { await LeaveFinalBattleForRecoveryAsync(); }
                catch (Exception cleanup) { error = new AggregateException(error, cleanup); }
                _bossPresentation?.ResetBattle();
                _bossCheckpointScope?.Dispose(); _bossCheckpointScope = null;
                BossBattleError = error.Message; BossBattlePhase = StageBossBattlePhase.Faulted;
                Debug.LogError("보스전 진입 중단: " + error.Message, this);
                GameAlerts.Show("보스전 진입에 실패했습니다. Game Debugger에서 원인을 확인하세요.");
            }
        }

        private async Task SaveBossStartAsync(AppRoot app, CancellationToken cancellation)
        {
            var providers = _finalSceneOperation.Scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).OfType<ICheckpointParticipantProvider>();
            var participants = providers.SelectMany(p => p.GetCheckpointParticipants()).ToList();
            foreach (var actor in AuthorityActors.Values)
                participants.AddRange(actor.GetComponentsInChildren<MonoBehaviour>(true).OfType<ICheckpointParticipant>()
                    .Where(p => p.Kind == CheckpointSectionKind.World));
            participants.Add(_matchSession.CreateRandomCheckpointParticipant());
            var required = new HashSet<string>(StringComparer.Ordinal) { "world/enemies", "core/random" };
            AddRequiredPlayerWorldWriters(required);
            var runtime = new MatchCheckpointRuntime(_matchSession, _matchTime, () => HasCurrentMatch,
                () => _coordinator.IsGameplayRunning, _coordinator.GameplayCancellation,
                BeginCheckpointRestore,
                () => { _restoringCheckpoint = false; },
                error => { BossBattleError = error.Message; BossBattlePhase = StageBossBattlePhase.Faulted; },
                () => BossBattlePhase is StageBossBattlePhase.Intro or StageBossBattlePhase.Battle,
                () => BossBattlePhase == StageBossBattlePhase.Battle);
            _bossCheckpointScope?.Dispose();
            _bossCheckpointScope = app.CreateCheckpointScope(runtime, participants, required);
            var result = await _bossCheckpointScope.Service.SaveAsync(new CheckpointSlot("boss-start-" + _recoveryLedger.RunId.ToString("N")), cancellation);
            if (result.Status != CheckpointOperationStatus.Saved || !result.IsCurrentScope)
                throw new InvalidOperationException("보스전 시작 안전장치 저장 실패: " + result.Reason, result.Error);
            _recoveryLedger.RecordCheckpoint(_bossCheckpointScope.Service.LastSavedSlot, _bossCheckpointScope.Service.LastSavedData.CheckpointId);
        }

        private async Task FinishStageBossBattleAsync()
        {
            var app = _matchHost as AppRoot;
            var cancellation = _coordinator.IsGameplayRunning ? _coordinator.GameplayCancellation : app.destroyCancellationToken;
            BossBattlePhase = StageBossBattlePhase.Victory;
            try
            {
                using var pause = _matchTime.AcquirePause("BossVictory");
                using var input = app.LocalInput.Acquire("BossVictory");
                await _bossPresentation.PlayVictoryAsync("보스를 처치하는데에 성공했습니다", 5, cancellation);
                BossBattlePhase = StageBossBattlePhase.Returning;
                // 귀환은 Match를 종료하므로 그 Match의 취소 토큰으로 귀환 자체를 취소하지 않습니다.
                await app.FadeSceneTravelAsync(async () =>
                {
                    if (_coordinator.IsGameplayRunning) _coordinator.CompleteMatch(MatchOutcome.Victory);
                    using var deadline = CancellationTokenSource.CreateLinkedTokenSource(app.destroyCancellationToken);
                    deadline.CancelAfter(TimeSpan.FromSeconds(30));
                    while (app.NetworkMatches?.HasPendingCompletion == true)
                    { deadline.Token.ThrowIfCancellationRequested(); await Task.Yield(); }
                    if (app.GameFlow != null) await app.GameFlow.ReturnToSpaceshipAsync(deadline.Token);
                    else await app.ReplaceGameplaySceneAsync(AppRoot.DefaultHubScene, deadline.Token);
                }, app.destroyCancellationToken);
            }
            catch (Exception error)
            {
                if (app == null || app.destroyCancellationToken.IsCancellationRequested) return;
                BossBattleError = error.Message; BossBattlePhase = StageBossBattlePhase.Faulted;
                Debug.LogError("보스전 귀환 중단: " + error.Message, this);
            }
        }

        private GameObject FindPreparedBossActor()
        {
            // 복원은 이전 NGO 개체를 Despawn하고 새 개체를 만듭니다. 프레임 끝 파괴를 기다리는 옛 개체는 제외합니다.
            return _finalSceneOperation.Scene.GetRootGameObjects()
                .SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true))
                .Single(b => b is IEntityIdentity identified && identified.EntityId == _preparedBossId &&
                    b.TryGetComponent<Unity.Netcode.NetworkObject>(out var network) && network.IsSpawned).gameObject;
        }

        /// <summary>수정된 제작 연결을 다시 적용한 뒤 Debugger가 명시적으로 재시도합니다. 자동 재시도 루프는 만들지 않습니다.</summary>
        public void RetryBossBattle()
        {
            if (BossBattlePhase != StageBossBattlePhase.Faulted || _bossBattleTask is { IsCompleted: false }) return;
            BossBattleError = null;
            BossBattlePhase = StageProgress?.FinalBossDefeated == true ? StageBossBattlePhase.Battle : StageBossBattlePhase.Field;
        }

        private void ReleaseBossBattle()
        {
            _bossPresentation?.ResetBattle(); _bossPresentation = null;
            _bossCheckpointScope?.Dispose(); _bossCheckpointScope = null;
            _bossActor = null; _preparedBossId = default; BossBattlePhase = StageBossBattlePhase.Field;
        }
    }
}
