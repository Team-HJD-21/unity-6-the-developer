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
            var issuerIsInMatch = false;
            for (var index = 0; index < state.Players.Count; index++)
            {
                if (state.Players[index].PlayerId != command.IssuerId) continue;
                issuerIsInMatch = true;
                break;
            }
            if (!issuerIsInMatch) return CommandAuthorization.Deny("issuer-not-in-match");
            return CommandAuthorization.Allow();
        }
    }
}
