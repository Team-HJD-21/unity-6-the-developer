// D의 프로세스 로컬 InstanceId와 현재 Match의 터렛 논리 ID를 대응시킵니다.
// 존재하지 않거나 이미 해제된 등록은 조회 실패로 처리합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface ITurretEntityIdResolver
    {
        MatchId BoundMatchId { get; }
        bool TryResolveEntityId(int localInstanceId, out EntityId entityId);
        bool TryResolveLocalId(EntityId entityId, out int localInstanceId);
    }
}
