// 로컬 화면의 포커스와 표현 자원 수명을 ScreenManager에 연결합니다.
// 실제 View·Canvas·Button은 Presentation이 소유합니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public interface IScreenPresenter : IDisposable
    {
        void SetFocused(bool focused);
    }
}
