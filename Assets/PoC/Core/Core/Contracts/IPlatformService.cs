// Defines platform sign-in and identity access without referencing Steam SDK types.

using System.Threading;
using System.Threading.Tasks;

namespace TeamHJD.Game.Contracts
{
    public interface IPlatformService
    {
        Task<PlatformUser> SignInAsync(CancellationToken cancellationToken);
    }
}
