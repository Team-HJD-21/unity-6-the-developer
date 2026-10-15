// Match·발신자·논리 대상과 의도만 전달합니다. 실제 프리팹/상태/로컬 핸들은 입력하지 않습니다.
// Source 같은 표시 정보로 권한을 우회하지 않으며 요청 ID는 재전송에 그대로 사용합니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class TurretCommandRequest
    {
        public MatchRequestContext Context { get; }
        public TurretCommandKind Kind { get; }
        public EntityId TargetId { get; }
        public bool ShouldActivate { get; }
        public DefinitionId UpgradeId { get; }
        public TurretCommandRequest(MatchRequestContext context, TurretCommandKind kind,
            EntityId targetId = default, bool shouldActivate = false, DefinitionId upgradeId = default)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (!Enum.IsDefined(typeof(TurretCommandKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (kind == TurretCommandKind.StartAutoActivation ? !targetId.IsEmpty : targetId.IsEmpty)
                throw new ArgumentException("Only automatic activation is a controller-wide command.", nameof(targetId));
            bool upgrade = kind == TurretCommandKind.ApplyUpgrade || kind == TurretCommandKind.DowngradeUpgrade;
            if (upgrade == upgradeId.IsEmpty) throw new ArgumentException("Only a spec upgrade requires its definition ID.", nameof(upgradeId));
            if (kind != TurretCommandKind.SetActivation && shouldActivate)
                throw new ArgumentException("Only activation accepts an ON/OFF flag.", nameof(shouldActivate));
            if ((!targetId.IsEmpty && targetId.Value.Length > 128) || (!upgradeId.IsEmpty && upgradeId.Value.Length > 128))
                throw new ArgumentException("Command identifiers are limited to 128 characters.");
            Kind = kind; TargetId = targetId; ShouldActivate = shouldActivate; UpgradeId = upgradeId;
        }
        public string GetPayloadKey()
        {
            string target = TargetId.Value ?? string.Empty, upgrade = UpgradeId.Value ?? string.Empty;
            return (int)Kind + ":" + target.Length + ":" + target + ":" + (ShouldActivate ? 1 : 0) + ":" + upgrade.Length + ":" + upgrade;
        }
    }
}
