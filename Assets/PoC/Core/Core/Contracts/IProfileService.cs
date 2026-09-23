// Defines asynchronous profile load/save operations without choosing a storage backend.

using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IProfileService
    {
        // Returns null when the platform identity has no stored profile yet.
        Task<PlayerProfileSnapshot> LoadAsync(PlayerId playerId, CancellationToken cancellationToken);
        Task SaveAsync(PlayerProfileSnapshot profile, CancellationToken cancellationToken);
    }
}
