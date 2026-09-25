// HUD Presenter가 사용하는 읽기 전용 화면 표현 인터페이스를 정의합니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Presentation.UI
{
    public interface IHudView
    {
        void Render(MatchSnapshot snapshot);
    }
}
