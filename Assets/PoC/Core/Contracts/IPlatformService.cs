// Steam SDK 타입을 참조하지 않고 플랫폼 로그인과 사용자 식별 계약을 정의합니다.

using System.Threading;
using System.Threading.Tasks;

namespace TeamHJD.Game.Contracts
{
    public interface IPlatformService
    {
        Task<PlatformUser> SignInAsync(CancellationToken cancellationToken);
    }
}
