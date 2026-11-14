// 시작 우주선의 출격을 App 게임 흐름으로 전달합니다. 진행 중 전장의 내부 방문은 허용하지 않습니다.
// 0.3초 암전·최소0.3초 전환·0.3초 복귀를 사용하고 실제 PlayerStart에서 새 actor를 만듭니다.
// 결과 뒤 허브 귀환은 GameFlow가 소유합니다. 임시 Additive 방문·순간이동·위치 보고는 만들지 않습니다.
using System;
using System.IO;
using System.Threading.Tasks;
using TeamHJD.Game.Application;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace TeamHJD.Game.Bootstrap
{
    internal sealed class SpaceshipTravelService : ISpaceshipTravel
    {
        private readonly AppRoot _app;

        public bool IsTransitioning { get; private set; }
        internal bool IsLaunchingMission { get; private set; }

        public SpaceshipTravelService(AppRoot app) => _app = app;

        public Task EnterAsync(Transform player)
        {
            RequireIdle(player);
            GameAlerts.Show("출격 중에는 우주선 내부로 진입할 수 없습니다. 미션 결과 화면에서 귀환하세요.",
                AlertTone.Info, "spaceship/mission-entry-blocked");
            return Task.CompletedTask;
        }

        public async Task ExitAsync(Transform player, string fieldScene)
        {
            RequireIdle(player);
            if (!_app.CurrentMatchId.IsEmpty || _app.NetworkMatches?.Descriptor != null)
                throw new InvalidOperationException("이미 출격한 Match에서 우주선 임시 방문 복귀를 실행할 수 없습니다.");
            if (_app.GameFlow == null) throw new InvalidOperationException("Startup에서 구성한 게임 진입 경로가 필요합니다.");
            string key = ResolveScene(fieldScene);
            var lifetime = _app.destroyCancellationToken;
            using var input = _app.LocalInput.Acquire("SpaceshipTravel");
            IsTransitioning = true;
            try
            {
                await _app.FadeSpaceshipTravelAsync(async () =>
                {
                    IsLaunchingMission = true;
                    try { await _app.GameFlow.LaunchMissionAsync(key, lifetime); }
                    finally { IsLaunchingMission = false; }
                });
            }
            catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
            finally { IsTransitioning = false; }
        }

        private void RequireIdle(Transform player)
        {
            if (player == null) throw new ArgumentNullException(nameof(player));
            if (IsTransitioning || _app.GameFlow?.IsTransitioning == true || _app.SceneTransitions.IsTransitioning)
                throw new InvalidOperationException("이미 Scene 이동 중입니다.");
        }

        private static string ResolveScene(string name)
        {
            string found = null;
            for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            {
                string path = SceneUtility.GetScenePathByBuildIndex(i);
                if (path != name && Path.GetFileNameWithoutExtension(path) != name) continue;
                if (found != null) throw new InvalidOperationException("중복 Scene 이름입니다. 전체 경로를 사용하세요: " + name);
                found = path;
            }
            return found ?? throw new InvalidOperationException("Build Settings에 Scene이 없습니다: " + name);
        }

    }
}
