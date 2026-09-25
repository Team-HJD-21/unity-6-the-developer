// 저장소 종류를 정하지 않고 비동기 프로필 읽기·쓰기 계약을 정의합니다.

using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IProfileService
    {
        // 해당 플랫폼 사용자에 저장된 프로필이 아직 없으면 null을 반환합니다.
        Task<PlayerProfileSnapshot> LoadAsync(PlayerId playerId, CancellationToken cancellationToken);
        Task SaveAsync(PlayerProfileSnapshot profile, CancellationToken cancellationToken);
    }
}
