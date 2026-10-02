// Debug Window가 Scene의 Encounter 실행 어댑터를 찾고 Match snapshot을 전달하는 계약입니다.
// Core Editor는 Enemy 구현을 직접 참조하지 않으며, 요청 결과를 상태 문자열로 돌려받습니다.

using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public interface IEncounterDebugCommandTarget
    {
        bool CanRequestEncounter(out string reason);
        bool TryRequestEncounter(
            BattlefieldSpatialSnapshot snapshot,
            string squadOrder,
            int maxEnemyCount,
            out string result);
    }
}
