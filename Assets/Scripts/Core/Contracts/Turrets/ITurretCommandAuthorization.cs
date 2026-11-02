// 제품 정책을 명시적으로 주입합니다. payload의 IssuerId/출처 자체를 인증으로 신뢰하지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretCommandAuthorization
    {
        TurretCommandPermission Authorize(TurretCommandRequest request, PlayerId verifiedIssuer);
    }
}
