// Resolves authored content selection into immutable match configuration.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IContentCatalog
    {
        MatchConfig ResolveMatchConfig(MatchSelection selection);
    }
}
