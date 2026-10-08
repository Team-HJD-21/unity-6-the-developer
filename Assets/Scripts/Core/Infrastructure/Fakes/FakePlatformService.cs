// 외부 SDK를 초기화하지 않고 미리 지정한 플랫폼 사용자를 반환합니다.

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
