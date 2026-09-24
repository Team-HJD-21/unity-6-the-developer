// 요청한 플레이어가 보유한 능력의 사용을 요청합니다.


using System;

namespace TeamHJD.Game.Domain
{
    public sealed class UseAbilityCommand : GameCommand
    {
        public DefinitionId AbilityDefinitionId { get; }

        public UseAbilityCommand(Guid commandId, PlayerId issuerId, DefinitionId abilityDefinitionId)
            : base(commandId, issuerId) => AbilityDefinitionId = abilityDefinitionId;
    }
}
