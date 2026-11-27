// 시작 화면과 우주선의 제작된 Scene 경로를 고정합니다. Mission 경로는 출격 요청이 전달합니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class GameFlowRoutes
    {
        // 직접 Scene 실행과 기본 출격이 공유하는 Build Settings 경로입니다.
        public const string DefaultStage1Scene = "Assets/Scenes/Gameplay/Stage_1.unity";
        public const string DefaultHubScene = "Assets/Scenes/Spaceship/SpaceShipGrayBox.unity";

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
