// MatchCoordinator가 정규 생산을 소유할 때 Scene의 독립 첫 요청을 끄는 연결입니다.
// 네트워크 개설 소유권이나 수동/디버그 요청 권한은 변경하지 않습니다.
namespace TeamHJD.Game.Contracts
{
    public interface IEncounterProductionConsumer
    {
        /// <summary>Core 조립부가 BindMatch 전에 호출합니다. 이후 정규 요청은 Match의 Scheduler가 담당합니다.</summary>
        void UseMatchProduction();
    }
}
