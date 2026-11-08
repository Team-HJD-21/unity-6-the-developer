// 플랫폼 SDK 타입을 Core에 노출하지 않고 플랫폼 사용자 정보를 표현합니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public sealed class PlatformUser
    {
        // 초기 개발 API의 호환 식별자입니다. 실제 Match 슬롯을 이 값으로 자동 결정하지 않습니다.
        public PlayerId PlayerId { get; }
        public string Provider { get; }
        public string PlatformId { get; }
        public string DisplayName { get; }

        public PlatformUser(string provider, string platformId, string displayName)
        {
            if (string.IsNullOrWhiteSpace(provider) || string.IsNullOrWhiteSpace(platformId))
                throw new System.ArgumentException("Platform provider and user ID are required.");
            Provider = provider; PlatformId = platformId; DisplayName = displayName ?? string.Empty;
            PlayerId = new PlayerId(provider + ":" + platformId);
        }

        public PlatformUser(PlayerId playerId, string displayName)
        {
            Provider = "development"; PlatformId = playerId.Value;
            PlayerId = playerId;
            DisplayName = displayName ?? string.Empty;
        }
    }
}
