// 한 Match의 논리 개체와 Feature 로컬 핸들의 대응을 나타내는 불변 등록 토큰입니다.
// 재등록 때 새 토큰을 발급해 이전 객체의 늦은 해제가 현재 등록을 제거하지 못하게 합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Application
{
    public sealed class EntityRegistration<THandle>
    {
        public MatchId MatchId { get; }
        public EntityId EntityId { get; }
        public THandle Handle { get; }
        public long Generation { get; }
        internal EntityRegistration(MatchId matchId, EntityId entityId, THandle handle, long generation)
        { MatchId = matchId; EntityId = entityId; Handle = handle; Generation = generation; }
    }
}
