// 준비 응답·시작·종료를 같은 Match/설정 세대에 대응시킵니다.
// 발신자의 실제 연결은 DTO에 넣지 않고 수신 어댑터가 별도로 제공합니다.
using System;
using System.Text;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public readonly struct MatchLifecycleSignal
    {
        public const int MaximumCurrencyEntries = 256;
        public const int MaximumCurrencyIdBytes = 1024;
        public MatchId MatchId { get; }
        public Guid DescriptorId { get; }
        public MatchCompletion Completion { get; }
        public MatchLifecycleSignal(MatchId matchId, Guid descriptorId, MatchCompletion completion = null)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A match id is required.", nameof(matchId));
            if (descriptorId == Guid.Empty) throw new ArgumentException("A descriptor id is required.", nameof(descriptorId));
            if (completion != null && completion.Result.MatchId != matchId)
                throw new ArgumentException("The completion belongs to another Match.", nameof(completion));
            if (completion != null)
            {
                if (completion.RewardReceipt.CurrencyDeltas.Count > MaximumCurrencyEntries)
                    throw new ArgumentException("Too many proposed currency entries.", nameof(completion));
                var utf8 = new UTF8Encoding(false, true);
                foreach (CurrencyDelta delta in completion.RewardReceipt.CurrencyDeltas)
                    if (utf8.GetByteCount(delta.CurrencyId.Value) > MaximumCurrencyIdBytes)
                        throw new ArgumentException("A currency ID exceeds the wire limit.", nameof(completion));
            }
            MatchId = matchId; DescriptorId = descriptorId; Completion = completion;
        }
    }
}
