// App이 현재 보여주는 게임 진입 위치입니다. Match의 전투 Phase와 별개입니다.
namespace TeamHJD.Game.Domain
{
    public enum GameFlowPhase { Startup, MainMenu, Spaceship, Mission, Faulted }
}
