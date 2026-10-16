// 준비 응답·시작·종료를 같은 Match/설정 세대에 대응시킵니다.
// 발신자의 실제 연결은 DTO에 넣지 않고 수신 어댑터가 별도로 제공합니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public readonly struct MatchLifecycleSignal
    {
        public MatchId MatchId { get; }
        public Guid DescriptorId { get; }
        public MatchLifecycleSignal(MatchId matchId, Guid descriptorId)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A match id is required.", nameof(matchId));
            if (descriptorId == Guid.Empty) throw new ArgumentException("A descriptor id is required.", nameof(descriptorId));
            MatchId = matchId; DescriptorId = descriptorId;
        }
    }
}
