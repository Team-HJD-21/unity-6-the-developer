// 시작 화면·우주선 메인 허브·출격·명시 귀환의 App 흐름을 조율합니다.
// 임시 허브 방문·Match 승패·보상·Feature 원본은 소유하지 않습니다.
// Scene 로드와 실제 Match 조립 성공을 구분하며 실패 위치를 진단에 남깁니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class GameFlowController : IDisposable
    {
        private readonly IGameFlowHost _host;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly TimeSpan _missionTimeout;
        private bool _disposed;
        public GameFlowRoutes Routes
        {
            get;
        }
        public GameFlowPhase Phase { get; private set; } = GameFlowPhase.Startup;
        public string CurrentSceneKey
        {
            get; private set;
        }
        public bool IsTransitioning
        {
            get; private set;
        }
        public Exception LastFailure
        {
            get; private set;
        }
        public Exception LastObserverError
        {
            get; private set;
        }
        public event Action Changed;

        public GameFlowController(IGameFlowHost host, GameFlowRoutes routes, TimeSpan? missionTimeout = null)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            Routes = routes ?? throw new ArgumentNullException(nameof(routes));
            _missionTimeout = missionTimeout ?? TimeSpan.FromSeconds(30);
            if (_missionTimeout <= TimeSpan.Zero || _missionTimeout.TotalMilliseconds > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(missionTimeout));
        }

        /// <summary>시작 Scene 조립부가 이미 로드된 화면을 등록합니다. 같은 Scene을 다시 로드하지 않습니다.</summary>
        public void AdoptMainMenu()
        {
            RequireAvailable();
            if (Phase != GameFlowPhase.Startup || IsTransitioning || !_host.IsLoaded(Routes.MainMenuScene))
                throw new InvalidOperationException("Adopt only the loaded startup menu before travel.");
            CurrentSceneKey = Routes.MainMenuScene;
            Phase = GameFlowPhase.MainMenu;
            Publish();
        }

        /// <summary>App의 NGO Scene/Match 관찰자가 Host가 확정한 실제 도착을 반영합니다. 새 로드나 접속을 요청하지 않습니다.</summary>
        public void AdoptSynchronizedScene(string sceneKey, bool missionReady)
        {
            RequireAvailable();
            if (IsTransitioning || !_host.IsLoaded(sceneKey))
                throw new InvalidOperationException("Observe only a loaded, composed Scene outside a local transition.");
            GameFlowPhase destination;
            if (sceneKey == Routes.MainMenuScene)
                destination = GameFlowPhase.MainMenu;
            else if (sceneKey == Routes.SpaceshipScene)
                destination = GameFlowPhase.Spaceship;
            else if (missionReady)
                destination = GameFlowPhase.Mission;
            else
                throw new InvalidOperationException("A Mission requires its actual local composition before adoption.");
            CurrentSceneKey = sceneKey;
            Phase = destination;
            LastFailure = null;
            Publish();
        }

        /// <summary>시작 버튼이 현재 MainMenu에서 호출합니다. 완료 시 우주선 Scene에 도착합니다.</summary>
        public Task EnterSpaceshipAsync(CancellationToken cancellation = default)
        {
            RequireAvailable();
            if (Phase != GameFlowPhase.MainMenu)
                throw new InvalidOperationException("Enter the spaceship from the main menu.");
            return TravelAsync(Routes.SpaceshipScene, GameFlowPhase.Spaceship, cancellation);
        }

        /// <summary>우주선의 출격 호출자가 선택한 Mission Scene으로 이동하고 실제 Match 준비까지 기다립니다.</summary>
        public Task LaunchMissionAsync(string missionScene, CancellationToken cancellation = default)
        {
            RequireAvailable();
            if (Phase != GameFlowPhase.Spaceship)
                throw new InvalidOperationException("Launch a mission from the spaceship.");
            if (missionScene == Routes.MainMenuScene || missionScene == Routes.SpaceshipScene)
                throw new ArgumentException("Choose a gameplay Scene for the mission.", nameof(missionScene));
            return TravelAsync(missionScene, GameFlowPhase.Mission, cancellation, MissionEntryKind.NewMission);
        }

        /// <summary>App의 저장 복원 조율부가 메타데이터 검증 후 호출합니다. 실제 복원이 끝나야 Mission 입장 성공입니다.</summary>
        public Task ResumeMissionAsync(string missionScene, CancellationToken cancellation = default)
        {
            RequireAvailable();
            if (Phase == GameFlowPhase.Startup || missionScene == Routes.MainMenuScene || missionScene == Routes.SpaceshipScene)
                throw new InvalidOperationException("Resume a gameplay Scene after startup initialization.");
            return TravelAsync(missionScene, GameFlowPhase.Mission, cancellation, MissionEntryKind.Resume);
        }

        /// <summary>결과 화면/명시 포기 호출자가 Match를 종료하고 허브로 귀환합니다. 진행 중 임시 허브 방문과 다릅니다.</summary>
        public Task ReturnToSpaceshipAsync(CancellationToken cancellation = default)
        {
            RequireAvailable();
            if (Phase != GameFlowPhase.Mission && Phase != GameFlowPhase.Faulted)
                throw new InvalidOperationException("Return after mission entry or a failed arrival.");
            return TravelAsync(Routes.SpaceshipScene, GameFlowPhase.Spaceship, cancellation);
        }

        /// <summary>결과 화면의 완전 재시작 요청입니다. 현재 Mission을 새 Match로 다시 열며 체크포인트 복원과 구분합니다.</summary>
        public Task RestartMissionAsync(CancellationToken cancellation = default)
        {
            RequireAvailable();
            if (Phase != GameFlowPhase.Mission)
                throw new InvalidOperationException("Restart the current ready mission.");
            return TravelAsync(CurrentSceneKey, GameFlowPhase.Mission, cancellation, MissionEntryKind.NewMission);
        }

        /// <summary>안전장치/기술 재시도 조율부가 같은 Mission 실행을 이어갑니다. 명시 새 게임과 달리 자원을 초기화하지 않습니다.</summary>
        public Task RetryMissionAsync(string missionScene, CancellationToken cancellation = default)
        {
            RequireAvailable();
            if ((Phase != GameFlowPhase.Mission && Phase != GameFlowPhase.Faulted) ||
                missionScene == Routes.MainMenuScene || missionScene == Routes.SpaceshipScene)
                throw new InvalidOperationException("Retry the current Mission after entry or a failed arrival.");
            return TravelAsync(missionScene, GameFlowPhase.Mission, cancellation, MissionEntryKind.Retry);
        }

        /// <summary>앱 내 시작 화면 복귀 호출자가 사용합니다. 실제 종료/전환 권한은 Host port가 검사합니다.</summary>
        public Task OpenMainMenuAsync(CancellationToken cancellation = default) => TravelAsync(Routes.MainMenuScene, GameFlowPhase.MainMenu, cancellation);

        private async Task TravelAsync(string sceneKey, GameFlowPhase destination, CancellationToken cancellation, MissionEntryKind? entry = null)
        {
            RequireAvailable();
            if (IsTransitioning)
                throw new InvalidOperationException("A game-flow transition is already running.");
            cancellation.ThrowIfCancellationRequested();
            if (!_host.CanLoad(sceneKey))
                throw new ArgumentException("Enable the authored Scene in Build Settings.", nameof(sceneKey));
            string restriction = _host.TravelRestriction;
            if (!string.IsNullOrEmpty(restriction))
                throw new InvalidOperationException(restriction);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _lifetime.Token);
            IsTransitioning = true;
            LastFailure = null;
            Publish();
            try
            {
                if (entry.HasValue) _host.PrepareMission(entry.Value);
                await _host.TravelAsync(sceneKey, linked.Token);
                if (_disposed)
                    throw new OperationCanceledException("The App stopped during travel.");
                if (destination == GameFlowPhase.Mission)
                {
                    using var ready = CancellationTokenSource.CreateLinkedTokenSource(linked.Token);
                    ready.CancelAfter(_missionTimeout);
                    await _host.WaitForMissionReadyAsync(sceneKey, ready.Token);
                }
                if (_disposed)
                    throw new OperationCanceledException("The App stopped during arrival.");
                CurrentSceneKey = sceneKey;
                Phase = destination;
            }
            catch (Exception error)
            {
                if (!_disposed)
                {
                    LastFailure = error;
                    CurrentSceneKey = _host.ActiveSceneKey;
                    Phase = GameFlowPhase.Faulted;
                }
                throw;
            }
            finally { IsTransitioning = false; if (!_disposed) Publish(); }
        }

        private void Publish()
        {
            if (_disposed || Changed == null)
                return;
            foreach (Action handler in Changed.GetInvocationList())
                try
                {
                    handler();
                }
                catch (Exception error) { LastObserverError = error; }
        }
        private void RequireAvailable()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(GameFlowController));
        }
        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            Changed = null;
            try
            {
                _lifetime.Cancel();
            }
            finally { _lifetime.Dispose(); }
        }
    }
}
