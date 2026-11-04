// AppRoot의 기존 Scene 전환과 실제 Match 조립 상태를 게임 흐름 port로 연결합니다.
// Scene 파일만 로드되고 Feature 조립이 실패한 경우 입장 성공으로 보고하지 않습니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Bootstrap
{
    internal sealed class AppGameFlowHost : IGameFlowHost
    {
        private readonly AppRoot _app;
        public AppGameFlowHost(AppRoot app) { _app = app != null ? app : throw new ArgumentNullException(nameof(app)); }
        public string ActiveSceneKey => SceneManager.GetActiveScene().path;
        public string TravelRestriction => _app.GameFlowTravelRestriction;
        public bool CanLoad(string sceneKey) => _app.Services.SceneFlow.CanLoad(sceneKey);
        public bool IsLoaded(string sceneKey) => _app.Services.SceneFlow.IsLoaded(sceneKey);
        public Task TravelAsync(string sceneKey, CancellationToken cancellation) => _app.ReplaceGameplaySceneAsync(sceneKey, cancellation);
        public async Task WaitForMissionReadyAsync(string sceneKey, CancellationToken cancellation)
        {
            while (true)
            {
                cancellation.ThrowIfCancellationRequested();
                if (_app == null || _app.Services == null) throw new OperationCanceledException("The App stopped before Match readiness.");
                if (_app.TryGetCompositionFailure(sceneKey, out var error))
                    throw new InvalidOperationException("Mission Scene composition failed: " + error.Message, error);
                if (!IsLoaded(sceneKey)) throw new InvalidOperationException("The Mission Scene was unloaded before readiness.");
                if (_app.IsCurrentMatchRunning) return;
                await Task.Yield();
            }
        }
    }
}
