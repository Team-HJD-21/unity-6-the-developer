// Requests a player attack toward a domain-space target position.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class FireCommand : GameCommand
    {
        public float TargetX { get; }
        public float TargetY { get; }

        public FireCommand(Guid commandId, PlayerId issuerId, float targetX, float targetY)
            : base(commandId, issuerId)
        {
            TargetX = targetX;
            TargetY = targetY;
        }
    }
}
