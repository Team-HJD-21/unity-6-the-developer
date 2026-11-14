// 정책 소유자가 명시적으로 요청한 Boss Scene 준비와 실패 원복을 조율하는 연결 API입니다.
// 콘텐츠 준비는 지정 driver가 맡고 Core는 현재 Match/도착 명단/실제 Scene 개체·사망 원본을 확인합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Scene = UnityEngine.SceneManagement.Scene;

namespace TeamHJD.Game.Bootstrap.SceneComposition
{
    public sealed partial class InitialStageCompositionRoot
    {
        [Tooltip("같은 Stage의 IFinalBattleSceneDriver 구현. 실제 Boss Scene 경로·보스·도착·연출을 제공해야 합니다.")]
        [SerializeField] private MonoBehaviour _finalBattleSceneDriver;
        [SerializeField, Min(1)] private float _finalBattlePreparationTimeout = 120;
        private Task _finalBattleFlow;
        private FinalSceneOperation _finalSceneOperation;
        // 비동기 완료가 늦게 도착해도 다음 Match의 필드/객체 목록을 정리하지 않도록 시도별 실제 참조를 보존합니다.
        private sealed class FinalSceneOperation
        {
            public AppRoot App;
            public IFinalBattleSceneDriver Driver;
            public bool TransactionStarted, OwnsScene;
            public Scene Scene;
            public readonly List<FinalBattleActorSnapshot> Actors = new();
            public readonly List<IEnemyCombatSource> CombatSources = new();
        }
        public IReadOnlyDictionary<PlayerId, GameObject> AuthorityActors => _playerActors?.Actors;
        public bool FinalBattleFlowBusy => _finalBattleFlow != null && !_finalBattleFlow.IsCompleted;
        private bool AllowsFieldCheckpointWorld => _finalSceneOperation?.TransactionStarted != true && _finalBossTransition?.IsPrepared != true;

        /// <summary>실제 진행 정책이 정해진 뒤 Host 소유자가 호출할 연결 API입니다. Update/점유 변경은 자동 호출하지 않습니다.</summary>
        /// <remarks>실제 저장·Scene·도착·보스·연출을 기다립니다. 호출자는 Task 실패를 처리해야 하며 재호출도 명시적으로 결정합니다.</remarks>
        public Task EnterFinalBattleSceneAsync(MatchId match, CancellationToken cancellation = default)
        {
            cancellation.ThrowIfCancellationRequested();
            if (match != _ownedMatchId || !CanPrepareFinalBattle())
                throw new InvalidOperationException("현재 Host의 준비된 Stage에서만 명시적으로 요청할 수 있습니다.");
            if (FinalBattleFlowBusy || FinalBossTransitionPending)
                throw new InvalidOperationException("최종 Scene 준비가 이미 진행 중입니다.");
            FinalBossTransitionError = null;
            _finalBattleFlow = RunFinalBattleSceneAsync(cancellation);
            return _finalBattleFlow;
        }

        private async Task RunFinalBattleSceneAsync(CancellationToken cancellation)
        {
            var app = _matchHost as AppRoot;
            var match = _ownedMatchId;
            try
            {
                if (app == null || _finalBattleSceneDriver == null || !_finalBattleSceneDriver.isActiveAndEnabled ||
                    _finalBattleSceneDriver.gameObject.scene != gameObject.scene || !(_finalBattleSceneDriver is IFinalBattleSceneDriver driver))
                    throw new InvalidOperationException("실제 IFinalBattleSceneDriver가 지정되지 않았습니다.");
                string path = driver.ScenePath;
                if (path == gameObject.scene.path || !app.Services.SceneFlow.CanLoad(path))
                    throw new InvalidOperationException("실제 Boss Scene 경로를 Build Settings에 등록해야 합니다.");
                if (_finalSceneOperation != null) await ReleaseFinalBattleSceneAsync(_finalSceneOperation);
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
                deadline.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _finalBattlePreparationTimeout)));
                using var transition = await PrepareFinalBossTransitionAsync(match, deadline.Token);
                var operation = new FinalSceneOperation { App = app, Driver = driver, OwnsScene = !app.Services.SceneFlow.IsLoaded(path), TransactionStarted = true };
                foreach (var actor in AuthorityActors.Values) operation.Actors.Add(new FinalBattleActorSnapshot(actor));
                _finalSceneOperation = operation;
                try
                {
                    await app.SceneTransitions.LoadAsync(path, transition.Cancellation);
                    operation.Scene = SceneManager.GetSceneByPath(path);
                    if (!transition.IsCurrent || !HasCurrentMatch || _ownedMatchId != match)
                        throw new OperationCanceledException("이전 Match의 Scene 준비는 새 Match를 조작할 수 없습니다.", transition.Cancellation);
                    var ready = await AwaitFinalPreparation(driver.PrepareAsync(this, transition.Cancellation), transition.Cancellation);
                    transition.Cancellation.ThrowIfCancellationRequested();
                    if (!HasCurrentMatch || _ownedMatchId != match || !operation.Scene.isLoaded || ready == null || ready.MatchId != match ||
                        !new HashSet<PlayerId>(_matchSession.Config.Players.Select(p => p.PlayerId)).SetEquals(ready.ArrivedPlayers) ||
                        AuthorityActors.Count != ready.ArrivedPlayers.Count || AuthorityActors.Values.Any(a => a == null || a.scene != operation.Scene))
                        throw new InvalidOperationException("현재 Match의 모든 실제 참가자가 Boss Scene에 도착해야 합니다.");
                    var components = operation.Scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).ToArray();
                    if (!components.OfType<IEntityIdentity>().Any(a => a.EntityId == ready.Boss))
                        throw new InvalidOperationException("보고된 보스 ID의 실제 Scene 개체가 없습니다.");
                    if (!components.OfType<IBattlefieldEnemyPositionSource>().Any(s =>
                            s.CaptureEnemyPositions()?.Any(a => a.EntityId == ready.Boss) == true))
                        throw new InvalidOperationException("보고된 보스가 실제 살아있는 Enemy 원본에 등록되어 있지 않습니다.");
                    foreach (var source in components.OfType<IEnemyCombatSource>().Distinct())
                    {
                        source.DeathObserved += ReportStageEnemyDeath;
                        operation.CombatSources.Add(source);
                    }
                    if (operation.CombatSources.Count == 0) throw new InvalidOperationException("실제 Boss HP 사망 원본이 연결되지 않았습니다.");
                    ConfirmFinalBossReady(match, ready.Boss);
                }
                catch
                {
                    if (!operation.Scene.IsValid()) operation.Scene = SceneManager.GetSceneByPath(path);
                    await ReleaseFinalBattleSceneAsync(operation);
                    throw;
                }
            }
            catch (Exception error)
            {
                if (this != null && HasCurrentMatch && _ownedMatchId == match) FinalBossTransitionError = error.Message;
                throw;
            }
        }

        private async Task ReleaseFinalBattleSceneAsync(FinalSceneOperation operation)
        {
            var errors = new List<Exception>();
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            try { await AwaitFinalPreparation(StopAndRollback(), cleanup.Token); }
            catch (Exception error) { errors.Add(error); }
            async Task<bool> StopAndRollback()
            {
                operation.Driver.StopMatch();
                await operation.Driver.RollbackAsync(cleanup.Token);
                return true;
            }
            foreach (var actor in operation.Actors) RunCleanup(actor.Restore, errors);
            foreach (var source in operation.CombatSources) RunCleanup(() => source.DeathObserved -= ReportStageEnemyDeath, errors);
            operation.CombatSources.Clear();
            bool actorsReturned = operation.Actors.All(a => !a.IsInScene(operation.Scene));
            if (actorsReturned && operation.OwnsScene && operation.Scene.IsValid() && operation.Scene.isLoaded && operation.App != null)
                try { await operation.App.SceneTransitions.UnloadAsync(operation.Scene.path, CancellationToken.None); }
                catch (Exception error) { errors.Add(error); }
            if (!actorsReturned) errors.Add(new InvalidOperationException("참가자 원위치 복구 전에는 Boss Scene을 해제하지 않습니다."));
            if (errors.Count > 0) throw new AggregateException("최종 Scene 준비를 원복하는 중 오류가 발생했습니다.", errors);
            if (ReferenceEquals(_finalSceneOperation, operation)) _finalSceneOperation = null;
        }

        private async Task LeaveFinalBattleForRecoveryAsync()
        {
            if (!FinalBattleFlowBusy) _finalBossTransition?.Dispose();
            else _finalBossTransition?.CancelPreparation();
            if (FinalBattleFlowBusy)
                try { await _finalBattleFlow; }
                catch (OperationCanceledException) { /* 복귀가 취소한 준비의 원복은 위 Task에서 완료합니다. */ }
            if (_finalSceneOperation != null) await ReleaseFinalBattleSceneAsync(_finalSceneOperation);
        }

        private static async Task<T> AwaitFinalPreparation<T>(Task<T> pending, CancellationToken cancellation)
        {
            var cancelled = new TaskCompletionSource<bool>();
            using var registration = cancellation.Register(() => cancelled.TrySetResult(true));
            if (await Task.WhenAny(pending, cancelled.Task) != pending)
            {
                _ = pending.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new OperationCanceledException("실제 Scene 준비/원복이 취소되거나 제한 시간을 넘었습니다.", cancellation);
            }
            return await pending;
        }

        private void StopFinalBattleBindings()
        {
            var operation = _finalSceneOperation;
            _finalSceneOperation = null;
            if (operation == null) return;
            var errors = new List<Exception>();
            RunCleanup(operation.Driver.StopMatch, errors);
            foreach (var source in operation.CombatSources) RunCleanup(() => source.DeathObserved -= ReportStageEnemyDeath, errors);
            operation.CombatSources.Clear();
            if (errors.Count > 0) throw new AggregateException("최종 Scene 바인딩 종료 중 오류가 발생했습니다.", errors);
        }
    }
}
