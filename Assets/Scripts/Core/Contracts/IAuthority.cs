// 현재 Match 상태를 기준으로 명령의 실행 권한을 판정하는 정책 경계를 정의합니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IAuthority
    {
        CommandAuthorization Authorize(GameCommand command, MatchState state);
    }
}
