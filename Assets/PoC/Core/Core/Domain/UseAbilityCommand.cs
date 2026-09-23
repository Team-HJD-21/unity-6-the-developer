// Requests activation of an ability owned by the issuing player.


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
