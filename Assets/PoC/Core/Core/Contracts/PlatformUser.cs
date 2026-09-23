// Represents a platform identity without leaking a provider SDK into the Core.

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
