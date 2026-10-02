// 로드된 Scene의 조립부가 App 범위 Match 기능을 명시적으로 전달받는 진입 계약입니다.

using TeamHJD.Game.Application;

namespace TeamHJD.Game.Bootstrap
{
    public interface ISceneCompositionRoot
    {
        /// <summary>활성 상태로 Scene에 배치해 두며, Scene 로드 때 App 범위 Match 기능을 받아 Scene 범위를 조립합니다.</summary>
        void Compose(IAppMatchHost appMatchHost);
    }
}
