// Host가 확정한 한 번의 Match 준비 설정을 전달합니다.
// 접속 수명과 분리된 세대이며 Client가 독립 Match ID/지역 설정을 만들지 않습니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class NetworkMatchDescriptor
    {
        public Guid DescriptorId { get; }
        public long PreparationSequence { get; }
        public MatchConfig Config { get; }
        public RegionPolicyMode RegionMode { get; }
        public RegionGridDefinition RegionGrid { get; }
        public IReadOnlyList<MatchParticipantBinding> Participants { get; }
        public NetworkMatchDescriptor(Guid descriptorId, long preparationSequence, MatchConfig config,
            RegionPolicyMode regionMode, RegionGridDefinition regionGrid, IEnumerable<MatchParticipantBinding> participants)
        {
            if (descriptorId == Guid.Empty) throw new ArgumentException("A descriptor id is required.", nameof(descriptorId));
            if (preparationSequence < 1) throw new ArgumentOutOfRangeException(nameof(preparationSequence));
            Config = config ?? throw new ArgumentNullException(nameof(config));
            RegionGrid = regionGrid ?? throw new ArgumentNullException(nameof(regionGrid));
            if (!Enum.IsDefined(typeof(RegionPolicyMode), regionMode)) throw new ArgumentOutOfRangeException(nameof(regionMode));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            var players = new HashSet<PlayerId>();
            foreach (PlayerLoadoutSnapshot player in config.Players)
                if (!players.Add(player.PlayerId)) throw new ArgumentException("Duplicate configured player.", nameof(config));
            var connections = new HashSet<ulong>(); var boundPlayers = new HashSet<PlayerId>();
            var bindings = new List<MatchParticipantBinding>(); int hosts = 0;
            foreach (MatchParticipantBinding binding in participants)
            {
                if (binding == null || !players.Contains(binding.PlayerId) ||
                    !connections.Add(binding.ConnectionId) || !boundPlayers.Add(binding.PlayerId))
                    throw new ArgumentException("Invalid or duplicate participant binding.", nameof(participants));
                bindings.Add(binding); if (binding.IsHost) hosts++;
            }
            if (hosts != 1 || players.Count != bindings.Count)
                throw new ArgumentException("Exactly one host and all configured participants are required.", nameof(participants));
            DescriptorId = descriptorId; PreparationSequence = preparationSequence; RegionMode = regionMode;
            // Config의 Player 순서를 슬롯 기준으로 고정해 전송 전후의 actor 배정 순서를 보존합니다.
            var ordered = new List<MatchParticipantBinding>();
            foreach (PlayerLoadoutSnapshot player in config.Players)
                foreach (MatchParticipantBinding binding in bindings)
                    if (binding.PlayerId == player.PlayerId) { ordered.Add(binding); break; }
            Participants = new ReadOnlyCollection<MatchParticipantBinding>(ordered);
        }
        public bool TryGetSlot(ulong verifiedConnectionId, out int slotIndex)
        {
            for (int index = 0; index < Participants.Count; index++)
                if (Participants[index].ConnectionId == verifiedConnectionId) { slotIndex = index; return true; }
            slotIndex = -1; return false;
        }
    }
}
