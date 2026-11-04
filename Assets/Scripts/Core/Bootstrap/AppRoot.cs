// 앱 범위 서비스를 소유하고 Scene composition root에 Match host를 전달하는 persistent Unity 객체입니다.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Infrastructure;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour, IAppMatchHost, IDisposable
    {
        private readonly HashSet<int> _composedSceneHandles = new HashSet<int>();
        private MatchSession _currentMatch;
        private bool _isDisposed;
        private AppScreenPresenter _screenPresenter;
        private IDisposable _completionSubscription;
        private readonly Dictionary<string, Exception> _compositionFailures = new(StringComparer.Ordinal);
        public GameFlowController GameFlow { get; private set; }
        internal bool IsCurrentMatchRunning => _currentMatch != null && !_currentMatch.IsDisposed && _currentMatch.IsGameplayRunning;
        internal string GameFlowTravelRestriction => GetSceneTravelRestriction();
        internal bool TryGetCompositionFailure(string sceneKey, out Exception error) => _compositionFailures.TryGetValue(sceneKey, out error);

        /// <summary>Core 시작 Scene이 기존 시각 요소와 제작된 경로를 연결합니다. App 수명에 한 흐름만 구성합니다.</summary>
        public GameFlowController ConfigureGameFlow(GameFlowRoutes routes)
        {
            ThrowIfUnavailable();
            if (routes == null) throw new ArgumentNullException(nameof(routes));
            if (GameFlow != null)
            {
                if (GameFlow.Routes.MainMenuScene != routes.MainMenuScene || GameFlow.Routes.SpaceshipScene != routes.SpaceshipScene)
                    throw new InvalidOperationException("The App game-flow routes are already configured.");
                return GameFlow;
            }
            return GameFlow = new GameFlowController(new AppGameFlowHost(this), routes);
        }

        /// <summary>권위 Scene 조립부가 현재 Match의 저장 scope를 만듭니다. 반환 scope의 종료 책임은 호출자에게 있습니다.</summary>
        public MatchCheckpointScope CreateCheckpointScope(ICheckpointRuntime runtime, IEnumerable<ICheckpointParticipant> participants,
            IEnumerable<string> requiredParticipantKeys)
        {
            ThrowIfUnavailable();
            if (runtime == null || runtime.Config.MatchId != CurrentMatchId || !runtime.IsCurrent)
                throw new InvalidOperationException("Create checkpoint bindings for the current Match only.");
            return Services.CreateCheckpointScope(runtime, participants, requiredParticipantKeys);
        }

        /// <summary>게임 진입 호출자가 저장 메타데이터를 먼저 읽습니다. 새 Match 구성·Feature 복원이나 자동 로드를 수행하지 않습니다.</summary>
        public Task<CheckpointData> ReadCheckpointAsync(CheckpointSlot slot, CancellationToken cancellation = default)
        {
            ThrowIfUnavailable();
            if (Services.CheckpointStore == null) throw new InvalidOperationException("Checkpoint storage is not configured.");
            return Services.CheckpointStore.LoadAsync(slot, cancellation);
        }

        internal AppServices Services { get; private set; }
        public MatchId CurrentMatchId => _currentMatch == null ? default : _currentMatch.Config.MatchId;
        public SceneFlowController SceneTransitions { get { ThrowIfUnavailable(); return Services.SceneTransitions; } }
        public IInputBlocker LocalInput { get { ThrowIfUnavailable(); return Services.InputLocks; } }
        public ScreenManager Screens { get { ThrowIfUnavailable(); return Services.Screens; } }
        public INetworkSession NetworkSession => Services?.NetworkSession;
        public NetworkMatchCoordinator NetworkMatches => Services?.NetworkMatches;
        public RegionReplicationSession NetworkRegions => Services?.NetworkRegions;
        public EnvironmentReplicationSession NetworkEnvironments => Services?.NetworkEnvironments;
        public MatchEnvironmentSnapshot CurrentEnvironment => Services?.NetworkEnvironments?.Source?.Snapshot ?? _currentMatch?.Environment?.Snapshot;
        public IEncounterSpawnSourceRegistry CurrentSpawnSources => _currentMatch?.SpawnSources;

        public EnvironmentReplicationSession AttachEnvironmentTransport(IMatchEnvironmentTransport transport)
        { ThrowIfUnavailable(); return Services.AttachEnvironmentTransport(transport); }
        public ITurretCommandTransport NetworkTurretCommands => Services?.NetworkTurretCommands;
        public ITurretPresentationTransport NetworkTurretPresentation => Services?.NetworkTurretPresentation;

        public void AttachTurretPresentationTransport(ITurretPresentationTransport transport)
        {
            ThrowIfUnavailable();
            Services.AttachTurretPresentationTransport(transport);
        }

        public void AttachTurretCommandTransport(ITurretCommandTransport transport)
        {
            ThrowIfUnavailable();
            Services.AttachTurretCommandTransport(transport);
        }

        public void AttachNetworkSession(INetworkSession session)
        {
            ThrowIfUnavailable();
            Services.AttachNetworkSession(session);
        }

        public NetworkMatchCoordinator AttachNetworkMatchTransport(IMatchLifecycleTransport transport)
        {
            ThrowIfUnavailable();
            var matches = Services.AttachNetworkMatchTransport(transport);
            matches.Started += OnNetworkMatchStarted;
            matches.Completed += OnNetworkMatchCompleted;
            return matches;
        }

        public RegionReplicationSession AttachRegionSnapshotTransport(IRegionSnapshotTransport transport)
        {
            ThrowIfUnavailable();
            return Services.AttachRegionSnapshotTransport(transport);
        }

        private void Update()
        {
            if (!_isDisposed && Services != null)
            {
                Services.PumpNetwork();
                _screenPresenter?.Render();
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            if (Services != null) ComposeScene(SceneManager.GetActiveScene());
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
        }

#if UNITY_EDITOR
        public event Action<BattlefieldSpatialSnapshot> BattlefieldSnapshotChanged;
        public BattlefieldSpatialSnapshot CurrentBattlefieldSnapshot => _currentMatch?.Battlefield;
        public MatchConfig CurrentMatchConfig => _currentMatch?.Config;
#endif

        public void Initialize(AppServices services)
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppRoot));
            if (Services != null) throw new InvalidOperationException("AppRoot has already been initialized.");

            Services = services ?? throw new ArgumentNullException(nameof(services));
            if (UnityEngine.Application.isPlaying)
                _screenPresenter = new AppScreenPresenter(transform, Services.Screens, Services.SceneTransitions,
                    NavigateFromScreenAsync, GetSceneTravelRestriction);
        }

        public MatchSession StartMatch(MatchConfig config, MatchState initialState, IModeRules modeRules)
        {
            return StartMatch(config, initialState, modeRules, BattlefieldSpatialInput.Empty);
        }

        public MatchSession StartMatch(
            MatchConfig config,
            MatchState initialState,
            IModeRules modeRules,
            BattlefieldSpatialInput battlefieldInput)
        {
            return StartMatch(config, initialState, modeRules, battlefieldInput,
                BattlefieldDynamicSpatialInput.Empty, BattlefieldGridConfiguration.Default);
        }

        public MatchSession StartMatch(
            MatchConfig config,
            MatchState initialState,
            IModeRules modeRules,
            BattlefieldSpatialInput battlefieldInput,
            BattlefieldDynamicSpatialInput dynamicBattlefieldInput,
            BattlefieldGridConfiguration battlefieldGridConfiguration)
        {
            ThrowIfUnavailable();
            var nextMatch = Services.MatchSessions.Create(config, initialState, modeRules, battlefieldInput,
                dynamicBattlefieldInput, battlefieldGridConfiguration);
            try
            {
                nextMatch.Start();
            }
            catch
            {
                nextMatch.Dispose();
                throw;
            }

            try
            {
                EndCurrentMatch();
            }
            catch (Exception endingError)
            {
                // 이전 세션 정리에 실패해도 아직 소유권을 넘기지 않은 새 세션을 누수시키지 않습니다.
                try { nextMatch.Dispose(); }
                catch (Exception nextError)
                {
                    throw new AggregateException("Match replacement and rollback both failed.", endingError, nextError);
                }
                throw;
            }
            _currentMatch = nextMatch;
            Services.Screens.BindMatch(config.MatchId);
            _completionSubscription = nextMatch.Events.Subscribe(fact =>
            {
                if (fact is MatchCompleted completed && _currentMatch == nextMatch)
                    OnNetworkMatchCompleted(completed.Completion);
            });
#if UNITY_EDITOR
            PublishBattlefieldSnapshot(_currentMatch.Battlefield);
#endif
            return nextMatch;
        }

        public void UpdateBattlefieldParticipants(BattlefieldDynamicSpatialInput dynamicInput)
        {
            ThrowIfUnavailable();
            if (_currentMatch == null) throw new InvalidOperationException("There is no active match.");
            _currentMatch.UpdateBattlefieldParticipants(dynamicInput);
#if UNITY_EDITOR
            PublishBattlefieldSnapshot(_currentMatch.Battlefield);
#endif
        }

        public void ReconfigureBattlefieldGrid(BattlefieldGridConfiguration configuration)
        {
            ThrowIfUnavailable();
            if (_currentMatch == null) throw new InvalidOperationException("There is no active match.");
            _currentMatch.ReconfigureBattlefieldGrid(configuration);
#if UNITY_EDITOR
            PublishBattlefieldSnapshot(_currentMatch.Battlefield);
#endif
        }

        public void UpdateBattlefieldTurretLayout(BattlefieldSpatialInput staticInput)
        {
            ThrowIfUnavailable();
            if (_currentMatch == null) throw new InvalidOperationException("There is no active match.");
            _currentMatch.UpdateBattlefieldTurretLayout(staticInput);
#if UNITY_EDITOR
            PublishBattlefieldSnapshot(_currentMatch.Battlefield);
#endif
        }

        public MatchCompletion CompleteCurrentMatch(MatchOutcome outcome, long eventSequence)
        {
            ThrowIfUnavailable();
            if (_currentMatch == null) throw new InvalidOperationException("There is no active match.");
            return _currentMatch.Complete(outcome, eventSequence);
        }

        public bool TryEndMatch(MatchId expectedMatchId)
        {
            if (expectedMatchId.IsEmpty || expectedMatchId != CurrentMatchId) return false;
            EndCurrentMatch();
            return true;
        }

        public void EndCurrentMatch()
        {
            if (_currentMatch == null) return;
            _completionSubscription?.Dispose(); _completionSubscription = null;
            var endingMatch = _currentMatch;
            _currentMatch = null;
            try
            {
                endingMatch.Dispose();
            }
            finally
            {
#if UNITY_EDITOR
                PublishBattlefieldSnapshot(null);
#endif
            }
        }

        private void OnApplicationQuit() => DisposeSafely();
        private void OnDestroy() => DisposeSafely();

        private void DisposeSafely()
        {
            try { Dispose(); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        /// <summary>Match와 App 서비스를 모두 정리합니다. 실패는 정리가 끝난 뒤 함께 보고합니다.</summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _composedSceneHandles.Clear();
            _compositionFailures.Clear();
            AppServices services = Services;
            if (services?.NetworkMatches != null)
            {
                services.NetworkMatches.Started -= OnNetworkMatchStarted;
                services.NetworkMatches.Completed -= OnNetworkMatchCompleted;
            }
            Services = null;
            var errors = new List<Exception>();
            try { GameFlow?.Dispose(); GameFlow = null; }
            catch (Exception exception) { errors.Add(exception); }
            try { EndCurrentMatch(); }
            catch (Exception exception) { errors.Add(exception); }
            try { services?.Dispose(); }
            catch (Exception exception) { errors.Add(exception); }
            try { _screenPresenter?.Dispose(); _screenPresenter = null; }
            catch (Exception exception) { errors.Add(exception); }
            if (errors.Count > 0) throw new AggregateException("App shutdown failed after cleanup.", errors);
        }

        private void ThrowIfUnavailable()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppRoot));
            if (Services == null) throw new InvalidOperationException("AppRoot has not been initialized.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _compositionFailures.Remove(scene.path);
            if (mode == LoadSceneMode.Single && Services != null) Services.Screens.ResetContext();
            ComposeScene(scene);
        }

        private void OnNetworkMatchStarted(NetworkMatchDescriptor descriptor) => Services?.Screens.BindMatch(descriptor.Config.MatchId);
        private void OnNetworkMatchCompleted(MatchCompletion completion)
        {
            if (!_isDisposed && _screenPresenter != null) Services.Screens.ShowResult(completion);
        }

        /// <summary>화면의 재시작/복귀 의도를 Match 종료와 실제 Scene 교체에 연결하는 조립 경계입니다.</summary>
        public async Task ReplaceGameplaySceneAsync(string scenePath, CancellationToken token = default)
        {
            ThrowIfUnavailable(); token.ThrowIfCancellationRequested();
            if (!Services.SceneFlow.CanLoad(scenePath)) throw new ArgumentException("The Scene is unavailable in Build Settings.", nameof(scenePath));
            string restriction = GetSceneTravelRestriction();
            if (!string.IsNullOrEmpty(restriction)) throw new InvalidOperationException(restriction);
            if (NetworkMatches?.Descriptor != null && !NetworkMatches.TryEnd())
                throw new InvalidOperationException("Final result transmission is pending; wait before leaving.");
            EndCurrentMatch();
            await Services.SceneTransitions.ReplaceAsync(scenePath, token);
        }

        // 기존 공용 메뉴/결과 버튼도 같은 App 흐름으로 전달해 Scene과 표시 위치가 어긋나지 않게 합니다.
        private Task NavigateFromScreenAsync(string scenePath)
        {
            if (GameFlow == null) return ReplaceGameplaySceneAsync(scenePath);
            if (scenePath == GameFlow.Routes.SpaceshipScene &&
                (GameFlow.Phase == GameFlowPhase.Mission || GameFlow.Phase == GameFlowPhase.Faulted))
                return GameFlow.ReturnToSpaceshipAsync();
            if (scenePath == GameFlow.Routes.MainMenuScene) return GameFlow.OpenMainMenuAsync();
            if (scenePath == AppScreenPresenter.StageScene)
            {
                if (GameFlow.Phase == GameFlowPhase.Mission) return GameFlow.RestartMissionAsync();
                if (GameFlow.Phase == GameFlowPhase.Spaceship) return GameFlow.LaunchMissionAsync(scenePath);
            }
            throw new InvalidOperationException("Use the configured game-flow action for this screen.");
        }

        private string GetSceneTravelRestriction()
        {
            if (_isDisposed || Services == null) return "App is stopping.";
            if (Services.SceneTransitions.IsTransitioning) return "Scene transition in progress.";
            if (NetworkSession != null && NetworkSession.State == NetworkConnectionState.ConnectedClient)
                return "Waiting for the Host's Scene decision.";
            if (NetworkSession is NgoNetworkSession ngo && ngo.IsServer && ngo.ConnectedPeerIds.Count != 1)
                return "Multiplayer Scene travel awaits the shared readiness contract.";
            if (NetworkMatches?.HasPendingCompletion == true) return "Waiting for final result transmission.";
            return string.Empty;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _composedSceneHandles.Remove(scene.handle);
            _compositionFailures.Remove(scene.path);
        }

        private void ComposeScene(Scene scene)
        {
            if (Services == null || !scene.IsValid() || !scene.isLoaded) return;
            if (_composedSceneHandles.Contains(scene.handle)) return;

            var compositionRoots = new List<ISceneCompositionRoot>();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in rootObject.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is ISceneCompositionRoot compositionRoot && behaviour.isActiveAndEnabled)
                        compositionRoots.Add(compositionRoot);
                }
            }

            if (compositionRoots.Count == 0) return;
            if (compositionRoots.Count > 1)
            {
                _compositionFailures[scene.path] = new InvalidOperationException("A Mission Scene contains multiple active composition roots.");
                Debug.LogError(
                    $"Scene '{scene.name}' has {compositionRoots.Count} active scene composition roots. " +
                    "Keep one composition root per scene to avoid duplicate Match ownership.");
                return;
            }

            try
            {
                compositionRoots[0].Compose(this);
                _composedSceneHandles.Add(scene.handle);
            }
            catch (Exception exception)
            {
                _compositionFailures[scene.path] = exception;
                Debug.LogException(exception, compositionRoots[0] as MonoBehaviour);
            }
        }

#if UNITY_EDITOR
        private void PublishBattlefieldSnapshot(BattlefieldSpatialSnapshot snapshot)
        {
            try
            {
                BattlefieldSnapshotChanged?.Invoke(snapshot);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
#endif
    }
}
