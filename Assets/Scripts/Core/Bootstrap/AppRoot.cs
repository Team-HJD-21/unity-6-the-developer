// 앱 범위 서비스를 소유하고 Scene composition root에 Match host를 전달하는 persistent Unity 객체입니다.

using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed partial class AppRoot : MonoBehaviour, IAppMatchHost, IDisposable
    {
        /// <summary>UI 조립부가 로컬 연출 소비자에게 주입하는 App 수명 난수입니다. 게임 판정에 사용하지 않습니다.</summary>
        public IRandomStreams PresentationRandom => Services?.PresentationRandom;
        /// <summary>앱 화면/접속 흐름이 사용하는 플랫폼 Port입니다. SDK 구체 타입이나 AppServices 전체를 노출하지 않습니다.</summary>
        public IPlatformService Platform => Services?.Platform;
        private readonly HashSet<int> _composedSceneHandles = new HashSet<int>();
        private MatchSession _currentMatch;
        private bool _isDisposed;
        public bool IsInitialized => !_isDisposed && Services != null;
        private AppScreenPresenter _screenPresenter;
        private SpaceshipTravelService _spaceshipTravel;
        private IDisposable _completionSubscription;
        private readonly Dictionary<string, Exception> _compositionFailures = new(StringComparer.Ordinal);
        public GameFlowController GameFlow
        {
            get; private set;
        }
        internal bool IsCurrentMatchRunning => _currentMatch != null && !_currentMatch.IsDisposed && _currentMatch.IsGameplayRunning;
        internal string GameFlowTravelRestriction => GetSceneTravelRestriction();
        internal bool TryGetCompositionFailure(string sceneKey, out Exception error) => _compositionFailures.TryGetValue(sceneKey, out error);
        // Scene 준비/시작의 비동기 실패도 입장 대기자에게 즉시 전달합니다.
        internal void RecordCompositionFailure(string sceneKey, Exception error) => _compositionFailures[sceneKey] = error;


        /// <summary>Core 시작 Scene이 기존 시각 요소와 제작된 경로를 연결합니다. App 수명에 한 흐름만 구성합니다.</summary>
        public GameFlowController ConfigureGameFlow(GameFlowRoutes routes)
        {
            ThrowIfUnavailable();
            if (routes == null)
                throw new ArgumentNullException(nameof(routes));
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
            if (Services.CheckpointStore == null)
                throw new InvalidOperationException("Checkpoint storage is not configured.");
            return Services.CheckpointStore.LoadAsync(slot, cancellation);
        }

        internal AppServices Services
        {
            get; private set;
        }
        public MatchId CurrentMatchId => _currentMatch == null ? default : _currentMatch.Config.MatchId;
        public SceneFlowController SceneTransitions
        {
            get
            {
                ThrowIfUnavailable();
                return Services.SceneTransitions;
            }
        }
        public IInputBlocker LocalInput
        {
            get
            {
                ThrowIfUnavailable();
                return Services.InputLocks;
            }
        }
        public ScreenManager Screens
        {
            get
            {
                ThrowIfUnavailable();
                return Services.Screens;
            }
        }
        public INetworkSession NetworkSession => Services?.NetworkSession;
        public NetworkMatchCoordinator NetworkMatches => Services?.NetworkMatches;
        public RegionReplicationSession NetworkRegions => Services?.NetworkRegions;
        public EnvironmentReplicationSession NetworkEnvironments => Services?.NetworkEnvironments;
        public MatchEnvironmentSnapshot CurrentEnvironment => Services?.NetworkEnvironments?.Source?.Snapshot ?? _currentMatch?.Environment?.Snapshot;
        public IEncounterSpawnSourceRegistry CurrentSpawnSources => _currentMatch?.SpawnSources;

        public EnvironmentReplicationSession AttachEnvironmentTransport(IMatchEnvironmentTransport transport)
        {
            ThrowIfUnavailable();
            return Services.AttachEnvironmentTransport(transport);
        }
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
                (Services.Platform as IPlatformRuntime)?.Pump();
                Services.PumpNetwork();
                _screenPresenter?.Render();
            }
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            if (Services != null)
                ComposeScene(SceneManager.GetActiveScene());
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

        public UiDisplaySettings DisplaySettings { get; private set; }
        public GameAudioSettings AudioSettings { get; private set; }
        public GameAudioManager Audio { get; private set; }

        public void Initialize(AppServices services)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AppRoot));
            if (Services != null)
                throw new InvalidOperationException("AppRoot has already been initialized.");

            Services = services ?? throw new ArgumentNullException(nameof(services));
            DisplaySettings = new UiDisplaySettings();
            AudioSettings = new GameAudioSettings();
            if (UnityEngine.Application.isPlaying)
            {
                var audioConfig = Resources.Load<GameAudioConfiguration>("Core/GameAudioConfiguration");
                if (audioConfig != null)
                {
                    var audioObject = new GameObject("Game Audio"); audioObject.transform.SetParent(transform, false);
                    Audio = audioObject.AddComponent<GameAudioManager>(); Audio.Initialize(audioConfig, AudioSettings);
                }
            }
            _spaceshipTravel = new SpaceshipTravelService(this);
            if (Services.Platform is IPlatformRuntime) InitializePlatform();
            if (UnityEngine.Application.isPlaying)
                _screenPresenter = new AppScreenPresenter(transform, Services.Screens, Services.SceneTransitions,
                    NavigateFromScreenAsync, GetSceneTravelRestriction,
                    () => GameFlow?.IsTransitioning == true, DisplaySettings,
                    () => GameFlow == null ? !CurrentMatchId.IsEmpty :
                        GameFlow.Phase == GameFlowPhase.Mission || GameFlow.Phase == GameFlowPhase.Faulted,
                    Services.Alerts, AudioSettings);
        }

        // 포털의 실제 Scene/actor 이동은 완전 암전 구간에서만 실행합니다.
        internal Task FadeSpaceshipTravelAsync(Func<Task> move)
        {
            ThrowIfUnavailable();
            if (_screenPresenter == null) throw new InvalidOperationException("Scene fade requires the App screen presenter.");
            return _screenPresenter.FadeSceneAsync(move, destroyCancellationToken);
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
                try
                {
                    nextMatch.Dispose();
                }
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
            if (_currentMatch == null)
                throw new InvalidOperationException("There is no active match.");
            _currentMatch.UpdateBattlefieldParticipants(dynamicInput);
#if UNITY_EDITOR
            PublishBattlefieldSnapshot(_currentMatch.Battlefield);
#endif
        }

        public void ReconfigureBattlefieldGrid(BattlefieldGridConfiguration configuration)
        {
            ThrowIfUnavailable();
            if (_currentMatch == null)
                throw new InvalidOperationException("There is no active match.");
            _currentMatch.ReconfigureBattlefieldGrid(configuration);
#if UNITY_EDITOR
            PublishBattlefieldSnapshot(_currentMatch.Battlefield);
#endif
        }

        public void UpdateBattlefieldTurretLayout(BattlefieldSpatialInput staticInput)
        {
            ThrowIfUnavailable();
            if (_currentMatch == null)
                throw new InvalidOperationException("There is no active match.");
            _currentMatch.UpdateBattlefieldTurretLayout(staticInput);
#if UNITY_EDITOR
            PublishBattlefieldSnapshot(_currentMatch.Battlefield);
#endif
        }

        public MatchCompletion CompleteCurrentMatch(MatchOutcome outcome, long eventSequence)
        {
            ThrowIfUnavailable();
            if (_currentMatch == null)
                throw new InvalidOperationException("There is no active match.");
            return _currentMatch.Complete(outcome, eventSequence);
        }

        public bool TryEndMatch(MatchId expectedMatchId)
        {
            if (expectedMatchId.IsEmpty || expectedMatchId != CurrentMatchId)
                return false;
            EndCurrentMatch();
            return true;
        }

        /// <summary>일반/저장 입장 실패 시 그 작업이 만든 Match만 정리합니다. 다른 Match나 결과 전송 gate는 건너뛰지 않습니다.</summary>
        internal void EndFailedMissionArrival(MatchId expectedMatchId)
        {
            if (expectedMatchId.IsEmpty)
                return;
            if (NetworkMatches?.Descriptor?.Config.MatchId == expectedMatchId && !NetworkMatches.TryEnd())
                throw new InvalidOperationException("The failed arrival cannot end while its final result is pending.");
            TryEndMatch(expectedMatchId);
        }

        public void EndCurrentMatch()
        {
            if (_currentMatch == null)
                return;
            _completionSubscription?.Dispose();
            _completionSubscription = null;
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
            try
            {
                Dispose();
            }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        /// <summary>Match와 App 서비스를 모두 정리합니다. 실패는 정리가 끝난 뒤 함께 보고합니다.</summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;
            _isDisposed = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            _composedSceneHandles.Clear();
            _compositionFailures.Clear();
            if (Audio != null) Audio.Shutdown();
            Audio = null;
            AppServices services = Services;
            if (services?.NetworkMatches != null)
            {
                services.NetworkMatches.Started -= OnNetworkMatchStarted;
                services.NetworkMatches.Completed -= OnNetworkMatchCompleted;
            }
            Services = null;
            var errors = new List<Exception>();
            try
            {
                GameFlow?.Dispose();
                GameFlow = null;
            }
            catch (Exception exception) { errors.Add(exception); }
            try
            {
                EndCurrentMatch();
            }
            catch (Exception exception) { errors.Add(exception); }
            try
            {
                services?.Dispose();
            }
            catch (Exception exception) { errors.Add(exception); }
            try
            {
                _screenPresenter?.Dispose();
                _screenPresenter = null;
            }
            catch (Exception exception) { errors.Add(exception); }
            if (errors.Count > 0)
                throw new AggregateException("App shutdown failed after cleanup.", errors);
        }

        private void ThrowIfUnavailable()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(AppRoot));
            if (Services == null)
                throw new InvalidOperationException("AppRoot has not been initialized.");
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            _compositionFailures.Remove(scene.path);
            if (mode == LoadSceneMode.Single && Services != null)
                Services.Screens.ResetContext();
            ComposeScene(scene);
            if (NetworkSession?.State == NetworkConnectionState.ConnectedClient && GameFlow != null &&
                (scene.path == GameFlow.Routes.MainMenuScene || scene.path == GameFlow.Routes.SpaceshipScene))
                GameFlow.AdoptSynchronizedScene(scene.path, false);
        }

        // Match가 직접 시작된 경우에도 실제 조립 완료 Scene과 App 표시 위치를 일치시킵니다.
        internal void ObserveComposedNetworkMission(string sceneKey)
        {
            if (GameFlow != null && !GameFlow.IsTransitioning)
                GameFlow.AdoptSynchronizedScene(sceneKey, true);
        }

        private void OnNetworkMatchStarted(NetworkMatchDescriptor descriptor) => Services?.Screens.BindMatch(descriptor.Config.MatchId);
        private void OnNetworkMatchCompleted(MatchCompletion completion)
        {
            if (!_isDisposed && _screenPresenter != null)
                Services.Screens.ShowResult(completion);
        }

        /// <summary>화면의 재시작/복귀 의도를 Match 종료와 실제 Scene 교체에 연결하는 조립 경계입니다.</summary>
        public async Task ReplaceGameplaySceneAsync(string scenePath, CancellationToken token = default)
        {
            ThrowIfUnavailable();
            token.ThrowIfCancellationRequested();
            if (!Services.SceneFlow.CanLoad(scenePath))
                throw new ArgumentException("The Scene is unavailable in Build Settings.", nameof(scenePath));
            string restriction = GetSceneTravelRestriction();
            if (!string.IsNullOrEmpty(restriction))
                throw new InvalidOperationException(restriction);
            if (NetworkMatches?.Descriptor != null && !NetworkMatches.TryEnd())
                throw new InvalidOperationException("Final result transmission is pending; wait before leaving.");
            EndCurrentMatch();
            await Services.SceneTransitions.ReplaceAsync(scenePath, token);
        }

        // 기존 공용 메뉴/결과 버튼도 같은 App 흐름으로 전달해 Scene과 표시 위치가 어긋나지 않게 합니다.
        private Task NavigateFromScreenAsync(string scenePath)
        {
            if (GameFlow == null)
                return ReplaceGameplaySceneAsync(scenePath);
            if (scenePath == GameFlow.Routes.SpaceshipScene &&
                (GameFlow.Phase == GameFlowPhase.Mission || GameFlow.Phase == GameFlowPhase.Faulted))
                return GameFlow.ReturnToSpaceshipAsync();
            if (scenePath == GameFlow.Routes.MainMenuScene)
                return GameFlow.OpenMainMenuAsync();
            if (scenePath == AppScreenPresenter.StageScene)
            {
                if (GameFlow.Phase == GameFlowPhase.Mission)
                    return GameFlow.RestartMissionAsync();
                if (GameFlow.Phase == GameFlowPhase.Spaceship)
                    return GameFlow.LaunchMissionAsync(scenePath);
            }
            throw new InvalidOperationException("Use the configured game-flow action for this screen.");
        }

        private async void InitializePlatform()
        {
            try { await Services.Platform.SignInAsync(CancellationToken.None); }
            catch (Exception error) { Debug.LogWarning("플랫폼 초기화 실패: " + error.Message, this); }
        }

        private string GetSceneTravelRestriction()
        {
            if (_isDisposed || Services == null)
                return "App is stopping.";
            if (_spaceshipTravel?.IsTransitioning == true && !_spaceshipTravel.IsLaunchingMission)
                return "우주선 출입 중입니다. 이동이 끝난 뒤 요청하세요.";
            if (Services.Platform is IPlatformRuntime platform && !platform.IsInitialized)
                return "Steam 플랫폼이 준비되지 않았습니다. Game Debugger에서 초기화 상태를 확인하세요.";
            if (Services.SceneTransitions.IsTransitioning)
                return "Scene transition in progress.";
            if (NetworkSession != null && NetworkSession.State == NetworkConnectionState.ConnectedClient)
                return "Waiting for the Host's Scene decision.";
            if (NetworkSession is NgoNetworkSession ngo && ngo.IsServer &&
                (ngo.ConnectedPeerIds.Count < 1 || ngo.ConnectedPeerIds.Count > 2 ||
                 ngo.ConnectedPeerIds.Count != ngo.Manager.ConnectedClientsIds.Count))
                return "Wait for the fixed one/two-player connection cohort to synchronize.";
            if (NetworkMatches?.HasPendingCompletion == true)
                return "Waiting for final result transmission.";
            return string.Empty;
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _composedSceneHandles.Remove(scene.handle);
            _compositionFailures.Remove(scene.path);
        }

        private void ComposeScene(Scene scene)
        {
            if (Services == null || !scene.IsValid() || !scene.isLoaded)
                return;
            if (_composedSceneHandles.Contains(scene.handle))
                return;

            // 직접 Scene 실행의 메뉴 귀환도 기존 Hub 조립을 사용합니다. 진행 Match의 Additive 방문은 제외합니다.
            string hubScene = GameFlow?.Routes.SpaceshipScene ?? AppScreenPresenter.HubScene;
            if (scene.path == hubScene && _currentMatch == null && NetworkMatches?.Descriptor == null &&
                !scene.GetRootGameObjects().Any(root => root.GetComponentInChildren<SceneComposition.SpaceshipHubCompositionRoot>(true) != null))
            {
                var hub = new GameObject("Core Starting Hub");
                SceneManager.MoveGameObjectToScene(hub, scene);
                hub.AddComponent<SceneComposition.SpaceshipHubCompositionRoot>();
            }

            var compositionRoots = new List<ISceneCompositionRoot>();
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                foreach (MonoBehaviour behaviour in rootObject.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour is TeamHJD.Game.Presentation.Scene.ISpaceshipTravelConsumer travelConsumer)
                        travelConsumer.BindSpaceshipTravel(_spaceshipTravel);
                    if (behaviour is TeamHJD.Game.Presentation.Scene.IUiDisplaySettingsConsumer displayConsumer)
                        displayConsumer.BindUiDisplaySettings(DisplaySettings);
                    if (behaviour is ISceneCompositionRoot compositionRoot && behaviour.isActiveAndEnabled)
                        compositionRoots.Add(compositionRoot);
                }
            }

            if (compositionRoots.Count == 0)
                return;
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
