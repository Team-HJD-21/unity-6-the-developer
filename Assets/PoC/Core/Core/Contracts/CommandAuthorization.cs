// Carries the authority decision and a machine-readable denial reason.

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
