// Defines app-owned scene transitions requested by application composition.

using System.Threading;
using System.Threading.Tasks;

namespace TeamHJD.Game.Contracts
{
    public interface ISceneFlow
    {
        Task LoadAsync(string sceneKey, CancellationToken cancellationToken);
        Task UnloadAsync(string sceneKey, CancellationToken cancellationToken);
    }
}
