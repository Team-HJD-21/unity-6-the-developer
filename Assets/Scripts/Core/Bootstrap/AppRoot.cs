// 앱 범위 서비스를 소유하고 Scene composition root에 Match host를 전달하는 persistent Unity 객체입니다.

using System;
using System.Collections.Generic;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour, IAppMatchHost, IDisposable
    {
        private readonly HashSet<int> _composedSceneHandles = new HashSet<int>();
        private MatchSession _currentMatch;
        private bool _isDisposed;

        internal AppServices Services { get; private set; }
        public MatchId CurrentMatchId => _currentMatch == null ? default : _currentMatch.Config.MatchId;
        public SceneFlowController SceneTransitions { get { ThrowIfUnavailable(); return Services.SceneTransitions; } }
        public IInputBlocker LocalInput { get { ThrowIfUnavailable(); return Services.InputLocks; } }
        public INetworkSession NetworkSession => Services?.NetworkSession;
        public NetworkMatchCoordinator NetworkMatches => Services?.NetworkMatches;
        public RegionReplicationSession NetworkRegions => Services?.NetworkRegions;
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
            return Services.AttachNetworkMatchTransport(transport);
        }

        public RegionReplicationSession AttachRegionSnapshotTransport(IRegionSnapshotTransport transport)
        {
            ThrowIfUnavailable();
            return Services.AttachRegionSnapshotTransport(transport);
        }

        private void Update()
        {
            if (!_isDisposed && Services != null) Services.PumpNetwork();
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
            AppServices services = Services;
            Services = null;
            var errors = new List<Exception>();
            try { EndCurrentMatch(); }
            catch (Exception exception) { errors.Add(exception); }
            try { services?.Dispose(); }
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
            ComposeScene(scene);
        }

        private void OnSceneUnloaded(Scene scene)
        {
            _composedSceneHandles.Remove(scene.handle);
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
