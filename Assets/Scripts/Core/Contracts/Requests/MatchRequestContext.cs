// 요청 상관관계와 Match/발행자 의도를 나타냅니다. 이 값 자체는 인증 증명이 아닙니다.
// 서버 어댑터는 실제 연결로 발행자를 확인한 뒤 Feature 처리 경계에 전달해야 합니다.
using System;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public sealed class MatchRequestContext
    {
        public MatchId MatchId { get; }
        public Guid RequestId { get; }
        public PlayerId IssuerId { get; }
        public MatchRequestContext(MatchId matchId, Guid requestId, PlayerId issuerId)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A match id is required.", nameof(matchId));
            if (requestId == Guid.Empty) throw new ArgumentException("A request id is required.", nameof(requestId));
            if (issuerId.IsEmpty) throw new ArgumentException("An issuer id is required.", nameof(issuerId));
            MatchId = matchId;
            RequestId = requestId;
            IssuerId = issuerId;
        }
    }
}
