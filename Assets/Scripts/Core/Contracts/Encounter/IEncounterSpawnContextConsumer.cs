// 기존 Enemy consumer에 source registry와 Match clock/취소 수명을 명시적으로 주입합니다.
using System.Threading;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterSpawnContextConsumer
    {
        void BindSpawnContext(MatchId match, IEncounterSpawnSourceRegistry sources, IMatchClock clock, int seed, CancellationToken gameplay);
        void UnbindSpawnContext(MatchId match);
    }
}
