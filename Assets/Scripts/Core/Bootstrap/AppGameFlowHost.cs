// AppRoot의 기존 Scene 전환과 실제 Match 조립 상태를 게임 흐름 port로 연결합니다.
// Scene 파일만 로드되고 Feature 조립이 실패한 경우 입장 성공으로 보고하지 않습니다.
using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Bootstrap.SceneComposition;
using UnityEngine.SceneManagement;

namespace TeamHJD.Game.Bootstrap
{
    internal sealed class AppGameFlowHost : IGameFlowHost
    {
        private readonly AppRoot _app;
        public AppGameFlowHost(AppRoot app)
        {
            _app = app != null ? app : throw new ArgumentNullException(nameof(app));
        }
        public string ActiveSceneKey => SceneManager.GetActiveScene().path;
        public string TravelRestriction => _app.GameFlowTravelRestriction;
        public bool CanLoad(string sceneKey) => _app.Services.SceneFlow.CanLoad(sceneKey);
        public bool IsLoaded(string sceneKey) => _app.Services.SceneFlow.IsLoaded(sceneKey);
        public async Task TravelAsync(string sceneKey, CancellationToken cancellation)
        {
            await _app.ReplaceGameplaySceneAsync(sceneKey, cancellation);
            if (_app.TryGetCompositionFailure(sceneKey, out var error))
                throw new InvalidOperationException("Scene composition failed: " + error.Message, error);
        }
        public async Task WaitForMissionReadyAsync(string sceneKey, CancellationToken cancellation)
        {
            // Scene 파일 동기화와 Feature 준비 장벽은 별개입니다. 출격 호출 안에서 둘 다 완료합니다.
            var enteringMatch = _app.CurrentMatchId;
            try
            {
                var loaded = SceneManager.GetSceneByPath(sceneKey);
                InitialStageNetworkBinding binding = null;
                foreach (var root in loaded.GetRootGameObjects())
                    foreach (var candidate in root.GetComponentsInChildren<InitialStageNetworkBinding>())
                    {
                        if (binding != null)
                            throw new InvalidOperationException("A Mission has multiple network entry bindings.");
                        binding = candidate;
                    }
                if (binding != null)
                {
                    await binding.StartAfterSceneTravelAsync(cancellation);
                    enteringMatch = binding.Matches?.Descriptor?.Config.MatchId ?? enteringMatch;
                }
                while (true)
                {
                    cancellation.ThrowIfCancellationRequested();
                    if (_app == null || _app.Services == null)
                        throw new OperationCanceledException("The App stopped before Match readiness.");
                    if (_app.TryGetCompositionFailure(sceneKey, out var error))
                        throw new InvalidOperationException("Mission Scene composition failed: " + error.Message, error);
                    if (!IsLoaded(sceneKey))
                        throw new InvalidOperationException("The Mission Scene was unloaded before readiness.");
                    if (_app.IsCurrentMatchRunning)
                        return;
                    await Task.Yield();
                }
            }
            catch (Exception error)
            {
                // Scene 교체는 이미 끝났어도, 준비 도중 시작된 이번 Match는 취소와 함께 정리합니다.
                try { _app.EndFailedMissionArrival(enteringMatch); }
                catch (Exception cleanup) { throw new AggregateException("Mission arrival cleanup failed.", error, cleanup); }
                throw;
            }
        }
    }
}
