// Creates a match-scoped object graph and transfers its lifetime to the caller.

using System;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchSessionFactory
    {
        private readonly IAuthority _authority;

        public MatchSessionFactory(IAuthority authority)
        {
            _authority = authority ?? throw new ArgumentNullException(nameof(authority));
        }

        public MatchSession Create(MatchConfig config, MatchState initialState, IModeRules modeRules)
        {
            return new MatchSession(config, initialState, new MatchSimulation(), modeRules, _authority);
        }
    }
}
