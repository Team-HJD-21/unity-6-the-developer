// 현재 Match/epoch와 원본별 증가 sequence에 묶인 불변 저장·복원 요청입니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class CheckpointRequest
    {
        public MatchId MatchId { get; } public Guid Epoch { get; } public Guid RequestId { get; }
        public long Sequence { get; } public CheckpointRequestKind Kind { get; } public CheckpointSlot Slot { get; }
        public CheckpointRestorePolicy Policy { get; }
        public CheckpointRequest(MatchId match, Guid epoch, Guid requestId, long sequence, CheckpointRequestKind kind,
            CheckpointSlot slot, CheckpointRestorePolicy policy = null)
        {
            if (match.IsEmpty || epoch == Guid.Empty || requestId == Guid.Empty || slot.IsEmpty) throw new ArgumentException("A current request identity and slot are required.");
            if (sequence < 1) throw new ArgumentOutOfRangeException(nameof(sequence));
            if (!Enum.IsDefined(typeof(CheckpointRequestKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            MatchId = match; Epoch = epoch; RequestId = requestId; Sequence = sequence; Kind = kind; Slot = slot;
            Policy = policy ?? CheckpointRestorePolicy.Default;
        }
    }
}
