// 실제 연결에서 검증한 PlayerId로 Encounter 요청을 제출하는 공용 경계입니다.
// payload의 IssuerId만으로 인증하지 않습니다. 로컬 조립부도 자신의 참가자 ID를 명시합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterRequestService
    {
        /// <summary>권위 측 게임 흐름/입력 어댑터가 현재 Match의 Encounter 실행을 요청합니다.</summary>
        /// <param name="verifiedIssuer">전송/조립부에서 검증한 실제 참가자 ID입니다. payload의 주장만으로 만들지 않습니다.</param>
        /// <returns>수락·거부·기존 확정 결과와 알려진 실제 실행 수량입니다. 상태를 확인한 뒤 게임 흐름을 진행합니다.</returns>
        /// <remarks>Unity 메인 스레드에서 호출합니다. 재전송은 같은 요청 ID와 내용을 유지하며 이 경계를 통해 최신 공간/환경을 전달합니다.</remarks>
        EncounterRequestResult Submit(EncounterRequest request, PlayerId verifiedIssuer);
    }
}
