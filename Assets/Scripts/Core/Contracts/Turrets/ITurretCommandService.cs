// 검증된 발신자와 정식 요청을 받아 확정 결과를 반환하는 Match 범위 API입니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretCommandService
    {
        TurretCommandResult Submit(TurretCommandRequest request, PlayerId verifiedIssuer);
    }
}
