// 권한 판정 결과와 코드에서 처리할 거부 사유를 전달합니다.

namespace TeamHJD.Game.Contracts
{
    public readonly struct CommandAuthorization
    {
        public bool IsAuthorized { get; }
        public string Reason { get; }

        private CommandAuthorization(bool isAuthorized, string reason)
        {
            IsAuthorized = isAuthorized;
            Reason = reason;
        }

        public static CommandAuthorization Allow() => new CommandAuthorization(true, string.Empty);
        public static CommandAuthorization Deny(string reason) => new CommandAuthorization(false, reason ?? string.Empty);
    }
}
