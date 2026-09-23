// Exposes match facts to consumers without granting them event publication ownership.

using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IMatchEventStream
    {
        IDisposable Subscribe(Action<MatchEvent> listener);
    }
}
