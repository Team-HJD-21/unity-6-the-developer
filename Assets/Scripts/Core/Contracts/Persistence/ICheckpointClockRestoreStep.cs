// actor의 남은 deadline을 복원하기 전에 적용해야 하는 시각 기준 복원입니다.
// 실패 rollback에서도 이 기준을 먼저 되돌리고 나머지 Feature의 백업을 적용합니다.
namespace TeamHJD.Game.Contracts
{
    public interface ICheckpointClockRestoreStep : ICheckpointRestoreStep { }
}
