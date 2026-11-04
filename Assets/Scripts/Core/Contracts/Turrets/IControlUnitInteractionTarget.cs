// CU 자동 활성화 요청의 실제 대상이 살아 있고 조작 가능한지 Host가 조회합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IControlUnitInteractionTarget
    {
        bool TryCaptureInteractionPosition(out BattlefieldPoint position);
    }
}
