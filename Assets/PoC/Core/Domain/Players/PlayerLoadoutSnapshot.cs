// Match 시작 전에 확정되는 플레이어별 장비 구성 입력입니다.


using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace TeamHJD.Game.Domain
{
    public sealed class PlayerLoadoutSnapshot
    {
        public PlayerId PlayerId { get; }
        public IReadOnlyDictionary<DefinitionId, int> UpgradeLevels { get; }

        public PlayerLoadoutSnapshot(PlayerId playerId, IDictionary<DefinitionId, int> upgradeLevels)
        {
            if (playerId.IsEmpty) throw new System.ArgumentException("Player loadout requires a valid player ID.", nameof(playerId));
            PlayerId = playerId;
            UpgradeLevels = new ReadOnlyDictionary<DefinitionId, int>(
                new Dictionary<DefinitionId, int>(upgradeLevels ?? new Dictionary<DefinitionId, int>()));
        }
    }
}
