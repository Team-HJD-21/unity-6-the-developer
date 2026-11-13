using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Presentation.Scene
{
    /// <summary>Player 생성자가 App 연출 난수를 전달합니다. 소비자는 actor 수명의 바인딩을 해제합니다.</summary>
    public interface IPlayerPresentationRandomBinding
    {
        void BindPresentationRandom(IRandomStreams streams);
    }
}
