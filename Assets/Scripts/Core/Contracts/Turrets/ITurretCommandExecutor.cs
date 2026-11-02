// Core 검증을 통과한 의도를 실제 터렛 Feature가 처리하는 경계입니다.
namespace TeamHJD.Game.Contracts
{
    public interface ITurretCommandExecutor
    {
        TurretCommandExecutionResult Execute(TurretCommandRequest request);
    }
}
