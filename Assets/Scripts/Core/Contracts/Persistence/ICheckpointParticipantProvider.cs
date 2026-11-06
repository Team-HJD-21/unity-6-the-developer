// Scene 밖에서 유지되는 Feature consumer가 실제 소유 writer를 명시적으로 제공합니다.
// 같은 Scene에 남은 테스트 생성기를 발견 순서로 선택하지 않습니다.
using System.Collections.Generic;
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointParticipantProvider
    {
        IEnumerable<ICheckpointParticipant> GetCheckpointParticipants();
    }
}
