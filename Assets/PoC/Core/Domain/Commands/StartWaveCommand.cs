// Match의 다음 웨이브 시작을 요청합니다.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class StartWaveCommand : GameCommand
    {
        public StartWaveCommand(Guid commandId, PlayerId issuerId) : base(commandId, issuerId) { }
    }
}
