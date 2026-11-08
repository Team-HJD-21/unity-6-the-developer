// App Scene 조립부가 출입문에 이동 API를 주입하는 계약입니다.

namespace TeamHJD.Game.Presentation.Scene
{
    public interface ISpaceshipTravelConsumer
    {
        void BindSpaceshipTravel(ISpaceshipTravel travel);
    }
}
