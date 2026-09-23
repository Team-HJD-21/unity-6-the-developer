// Returns a configured platform user without requiring external SDK initialization.

using System;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Infrastructure.Fakes
{
    public sealed class FakePlatformService : IPlatformService
    {
        private readonly PlatformUser _user;

        public FakePlatformService(PlatformUser user)
        {
            _user = user ?? throw new ArgumentNullException(nameof(user));
        }

        public Task<PlatformUser> SignInAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(_user);
        }
    }
}
