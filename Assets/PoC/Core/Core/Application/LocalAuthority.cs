// Authorizes commands in the local simulation without depending on a network transport.

using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class LocalAuthority : IAuthority
    {
        public CommandAuthorization Authorize(GameCommand command, MatchState state)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (state == null) throw new ArgumentNullException(nameof(state));
            if (state.Phase != MatchPhase.Running) return CommandAuthorization.Deny("match-not-running");
            return CommandAuthorization.Allow();
        }
    }
}
