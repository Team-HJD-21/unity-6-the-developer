// Feature 처리기의 실행 사실만 반환합니다. 요청 상관관계와 중복 기록은 Core 서비스가 붙입니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public sealed class TurretCommandExecutionResult
    {
        public TurretCommandStatus Status { get; }
        public string Code { get; }
        public string Reason { get; }
        public TurretCommandExecutionResult(TurretCommandStatus status, string code, string reason = "")
        {
            if (status != TurretCommandStatus.Applied && status != TurretCommandStatus.Accepted && status != TurretCommandStatus.Unchanged &&
                status != TurretCommandStatus.FeatureRejected && status != TurretCommandStatus.TargetUnavailable)
                throw new ArgumentOutOfRangeException(nameof(status));
            if (string.IsNullOrWhiteSpace(code) || code.Length > 128 || (reason?.Length ?? 0) > 512)
                throw new ArgumentException("A bounded Feature result code is required.", nameof(code));
            Status = status; Code = code; Reason = reason ?? string.Empty;
        }
    }
}
