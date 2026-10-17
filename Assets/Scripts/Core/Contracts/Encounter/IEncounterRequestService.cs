// 실제 연결에서 검증한 PlayerId로 Encounter 요청을 제출하는 공용 경계입니다.
// payload의 IssuerId만으로 인증하지 않습니다. 로컬 조립부도 자신의 참가자 ID를 명시합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterRequestService
    {
        EncounterRequestResult Submit(EncounterRequest request, PlayerId verifiedIssuer);
    }
}
