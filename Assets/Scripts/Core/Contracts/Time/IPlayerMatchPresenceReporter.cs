// 출입 Feature가 실제 이동 완료 후 한 Player의 확정 위치를 보고하는 계약입니다.
// 정지 명령이 아니며 참가자 전체의 필드 정지/재개 판단은 Core가 소유합니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IPlayerMatchPresenceReporter
    {
        MatchId MatchId { get; }
        PlayerId PlayerId { get; }
        bool CanReport { get; }
        MatchPlayerLocation Location { get; }

        /// <summary>Host의 이동 완료 코드가 해당 actor의 우주선/필드 도착 사실을 보고합니다.</summary>
        /// <returns>적용·중복 또는 거부 사유입니다. Accepted/Unchanged만 정상 접수입니다.</returns>
        /// <remarks>Client RPC가 아닙니다. Host에서 검증한 실제 이동 뒤에만 호출하며 Time.timeScale을 변경하지 않습니다.</remarks>
        MatchPresenceReportStatus TryReportLocation(MatchPlayerLocation location);
    }
}
