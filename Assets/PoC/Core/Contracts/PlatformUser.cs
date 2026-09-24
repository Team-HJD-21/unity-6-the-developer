// 플랫폼 SDK 타입을 Core에 노출하지 않고 플랫폼 사용자 정보를 표현합니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public sealed class PlatformUser
    {
        public PlayerId PlayerId { get; }
        public string DisplayName { get; }

        public PlatformUser(PlayerId playerId, string displayName)
        {
            PlayerId = playerId;
            DisplayName = displayName ?? string.Empty;
        }
    }
}
