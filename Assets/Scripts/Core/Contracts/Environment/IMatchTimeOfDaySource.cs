// 실제 시간대 source를 위한 별도 읽기 축입니다. source 부재를 임의의 낮으로 해석하지 않습니다.
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IMatchTimeOfDaySource : IMatchEnvironmentFeature
    {
        DefinitionId TimeOfDayId { get; }
    }
}
