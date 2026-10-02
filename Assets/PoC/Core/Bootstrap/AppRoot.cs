// 앱 범위 서비스 구성과 종료를 맡으며 Scene 전환 후에도 유지되는 Unity 객체입니다.

using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Bootstrap
{
    public sealed class AppRoot : MonoBehaviour
    {
        private MatchSession _currentMatch;
        private bool _isDisposed;

        internal AppServices Services { get; private set; }

#if UNITY_EDITOR
        public event Action<BattlefieldSpatialSnapshot> BattlefieldSnapshotChanged;
        public BattlefieldSpatialSnapshot CurrentBattlefieldSnapshot => _currentMatch?.Battlefield;
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

            EndCurrentMatch();
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

        private void OnDestroy()
        {
            if (_isDisposed) return;
            _isDisposed = true;
            EndCurrentMatch();
            Services?.Dispose();
            Services = null;
        }

        private void ThrowIfUnavailable()
        {
            if (_isDisposed) throw new ObjectDisposedException(nameof(AppRoot));
            if (Services == null) throw new InvalidOperationException("AppRoot has not been initialized.");
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
