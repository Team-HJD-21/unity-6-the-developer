// Defines the policy boundary that authorizes commands against current match state.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IAuthority
    {
        CommandAuthorization Authorize(GameCommand command, MatchState state);
    }
}
