// 시작 화면과 우주선의 제작된 Scene 경로를 고정합니다. Mission 경로는 출격 요청이 전달합니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class GameFlowRoutes
    {
        public string MainMenuScene { get; }
        public string SpaceshipScene { get; }
        public GameFlowRoutes(string mainMenuScene, string spaceshipScene)
        {
            if (string.IsNullOrWhiteSpace(mainMenuScene) || string.IsNullOrWhiteSpace(spaceshipScene) || mainMenuScene == spaceshipScene)
                throw new ArgumentException("Provide distinct authored menu and spaceship Scene paths.");
            MainMenuScene = mainMenuScene; SpaceshipScene = spaceshipScene;
        }
    }
}
