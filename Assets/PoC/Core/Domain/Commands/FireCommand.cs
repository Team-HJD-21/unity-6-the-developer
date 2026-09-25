// 게임 좌표계의 목표 위치를 향한 플레이어 공격을 요청합니다.


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
