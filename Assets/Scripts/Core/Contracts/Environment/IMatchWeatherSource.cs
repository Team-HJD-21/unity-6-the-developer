// 실제 날씨 원본의 현재 값/예고를 복사해 읽습니다. 시간대와는 독립된 축입니다.
using System.Collections.Generic;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public interface IMatchWeatherSource : IMatchEnvironmentFeature
    {
        DefinitionId WeatherId { get; }
        IReadOnlyList<DefinitionId> Forecast { get; }
    }
}
