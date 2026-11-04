// Host Match의 논리 source 등록과 origin 해석 경계입니다. 실제 나무/지형 객체는 Feature가 소유합니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterSpawnSourceRegistry
    {
        IEncounterSpawnSourceLease Register(MatchId match, DefinitionId sourceId, SpawnArea area);
        EncounterSpawnPlacement Resolve(EncounterSpawnOrigin origin);
        System.Threading.CancellationToken GetSourceCancellation(SpawnSourceReference source);
    }
}
