// Host가 확정한 참가자와 설정 세대의 Scene/Feature 준비 응답을 수집합니다.
// 연결 자체를 시작하지 않으며 Host 단독 시작과 중복/과거 시작 방지를 지원합니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Application
{
    public sealed class MatchReadyBarrier
    {
        private readonly Dictionary<ulong, MatchParticipantBinding> _byConnection = new Dictionary<ulong, MatchParticipantBinding>();
        private readonly HashSet<ulong> _ready = new HashSet<ulong>();
        public MatchId MatchId { get; }
        public Guid DescriptorId { get; }
        public IReadOnlyList<MatchParticipantBinding> Participants { get; }
        public int RequiredParticipantCount { get; }
        public bool HasStarted { get; private set; }
        public bool CanStart => !HasStarted && Participants.Count >= RequiredParticipantCount && _ready.Count == Participants.Count;

        public MatchReadyBarrier(MatchConfig config, IEnumerable<MatchParticipantBinding> participants,
            int requiredParticipantCount = 1, Guid? descriptorId = null)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (participants == null) throw new ArgumentNullException(nameof(participants));
            if (requiredParticipantCount < 1) throw new ArgumentOutOfRangeException(nameof(requiredParticipantCount));
            MatchId = config.MatchId;
            DescriptorId = descriptorId ?? Guid.NewGuid();
            if (DescriptorId == Guid.Empty) throw new ArgumentException("A descriptor id is required.", nameof(descriptorId));
            RequiredParticipantCount = requiredParticipantCount;
            var registered = new List<MatchParticipantBinding>();
            var playerIds = new HashSet<PlayerId>();
            var configured = new HashSet<PlayerId>();
            foreach (PlayerLoadoutSnapshot player in config.Players)
                if (!configured.Add(player.PlayerId)) throw new ArgumentException("Duplicate configured player.", nameof(config));
            int hosts = 0;
            foreach (MatchParticipantBinding participant in participants)
            {
                if (participant == null) throw new ArgumentException("A participant cannot be null.", nameof(participants));
                if (!configured.Contains(participant.PlayerId)) throw new ArgumentException("Player is not in match config.", nameof(participants));
                if (!playerIds.Add(participant.PlayerId) || _byConnection.ContainsKey(participant.ConnectionId))
                    throw new ArgumentException("Duplicate participant or connection.", nameof(participants));
                _byConnection.Add(participant.ConnectionId, participant);
                registered.Add(participant);
                if (participant.IsHost) hosts++;
            }
            if (hosts != 1) throw new ArgumentException("Exactly one host participant is required.", nameof(participants));
            if (configured.Count != registered.Count)
                throw new ArgumentException("All configured players require connection bindings.", nameof(participants));
            if (requiredParticipantCount > registered.Count)
                throw new ArgumentOutOfRangeException(nameof(requiredParticipantCount), "Rebuild preparation after the required cohort connects.");
            Participants = new ReadOnlyCollection<MatchParticipantBinding>(registered);
        }

        public MatchReadinessStatus MarkReady(ulong verifiedConnectionId, MatchId matchId, Guid descriptorId)
        {
            if (matchId != MatchId) return MatchReadinessStatus.DifferentMatch;
            if (descriptorId != DescriptorId) return MatchReadinessStatus.DifferentDescriptor;
            if (!_byConnection.ContainsKey(verifiedConnectionId)) return MatchReadinessStatus.UnknownConnection;
            if (HasStarted) return MatchReadinessStatus.AlreadyStarted;
            return _ready.Add(verifiedConnectionId) ? MatchReadinessStatus.Accepted : MatchReadinessStatus.AlreadyReady;
        }

        /// <summary>Host 권한 진입점에서만 호출합니다. 이 메서드는 연결을 열거나 MatchSession을 생성하지 않습니다.</summary>
        public bool TryStart(ulong verifiedConnectionId)
        {
            if (!CanStart || !_byConnection.TryGetValue(verifiedConnectionId, out MatchParticipantBinding participant) || !participant.IsHost)
                return false;
            HasStarted = true;
            return true;
        }

        public bool TryResolvePlayer(ulong verifiedConnectionId, out PlayerId playerId)
        {
            if (_byConnection.TryGetValue(verifiedConnectionId, out MatchParticipantBinding participant))
            {
                playerId = participant.PlayerId;
                return true;
            }
            playerId = default;
            return false;
        }
    }
}
