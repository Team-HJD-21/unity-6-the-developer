// 이번 연결을 Match 참가자에 대응시킵니다. 플랫폼 인증/영구 계정 ID를 대신하지 않습니다.
// 연결 ID는 전송 어댑터가 검증한 값을 사용하며 Client payload를 그대로 신뢰하지 않습니다.
using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public sealed class MatchParticipantBinding
    {
        public ulong ConnectionId { get; }
        public PlayerId PlayerId { get; }
        public bool IsHost { get; }
        public MatchParticipantBinding(ulong connectionId, PlayerId playerId, bool isHost)
        {
            if (playerId.IsEmpty) throw new ArgumentException("A player id is required.", nameof(playerId));
            ConnectionId = connectionId;
            PlayerId = playerId;
            IsHost = isHost;
        }
    }
}
