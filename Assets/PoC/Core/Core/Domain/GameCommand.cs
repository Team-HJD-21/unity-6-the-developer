// 게임 상태 변경 요청의 공통 데이터를 정의합니다.


using System;

namespace TeamHJD.Game.Domain
{
    public abstract class GameCommand
    {
        public Guid CommandId { get; }
        public PlayerId IssuerId { get; }

        protected GameCommand(Guid commandId, PlayerId issuerId)
        {
            if (commandId == Guid.Empty) throw new ArgumentException("A command requires a unique command ID.", nameof(commandId));
            if (issuerId.IsEmpty) throw new ArgumentException("A command requires an issuer ID.", nameof(issuerId));
            CommandId = commandId;
            IssuerId = issuerId;
        }
    }
}
