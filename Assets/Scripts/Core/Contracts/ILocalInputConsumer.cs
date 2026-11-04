// 조립부가 로컬 입력 잠금의 읽기 Port를 actor에 명시적으로 주입합니다.
namespace TeamHJD.Game.Contracts
{
    public interface ILocalInputConsumer
    {
        void BindInputBlocker(IInputBlocker input);
    }
}
