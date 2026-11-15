// Scene 조립 계층이 현재 살아있는 적의 위치를 불변 Battlefield 입력으로 수집할 때 사용하는 계약입니다.
// 사망 연출/제거 대기 객체는 밀도와 준비 완료의 생존 사실로 제출하지 않습니다.

using System.Collections.Generic;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IBattlefieldEnemyPositionSource
    {
        IReadOnlyList<EnemySpatialInput> CaptureEnemyPositions();
    }
}
