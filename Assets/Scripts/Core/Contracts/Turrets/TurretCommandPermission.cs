// 제품별 요청 권한/거리 정책의 판정입니다. 실제 서버 실행 권한·참가자 검증과 분리합니다.
using System;
namespace TeamHJD.Game.Contracts
{
    public sealed class TurretCommandPermission
    {
        public bool IsAllowed { get; }
        public string Code { get; }
        public string Reason { get; }
        public static TurretCommandPermission Allowed { get; } = new TurretCommandPermission(true, "Allowed", "");
        private TurretCommandPermission(bool allowed, string code, string reason)
        { IsAllowed = allowed; Code = code; Reason = reason; }
        public static TurretCommandPermission Denied(string code, string reason = "")
        {
            if (string.IsNullOrWhiteSpace(code) || code.Length > 128 || (reason?.Length ?? 0) > 512)
                throw new ArgumentException("A bounded permission code is required.", nameof(code));
            return new TurretCommandPermission(false, code, reason ?? string.Empty);
        }
    }
}
