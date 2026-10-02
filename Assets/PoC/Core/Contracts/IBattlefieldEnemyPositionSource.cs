// Scene 조립 계층이 현재 적들의 위치를 불변 Battlefield 입력으로 수집할 때 사용하는 계약입니다.

using System.Collections.Generic;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IBattlefieldEnemyPositionSource
    {
        IReadOnlyList<EnemySpatialInput> CaptureEnemyPositions();
    }
}
