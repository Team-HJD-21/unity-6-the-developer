// 실제 적용 전에 Feature가 목표 자료와 현재 실행을 검증하고 백업을 준비하는 입력입니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class CheckpointRestoreContext
    {
        public CheckpointCaptureContext Current { get; }
        public CheckpointData Target { get; }
        public CheckpointRestorePolicy Policy { get; }
        public CheckpointRestoreContext(CheckpointCaptureContext current, CheckpointData target, CheckpointRestorePolicy policy)
        {
            Current = current ?? throw new ArgumentNullException(nameof(current)); Target = target ?? throw new ArgumentNullException(nameof(target));
            Policy = policy ?? throw new ArgumentNullException(nameof(policy));
        }
    }
}
