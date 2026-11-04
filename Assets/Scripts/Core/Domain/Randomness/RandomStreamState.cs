// 하나의 이름별 난수 흐름이 다음 추첨을 이어갈 내부 상태입니다.
// 표시용 값이 아니라 저장/복원용 원본이며 Feature가 임의로 적용하지 않습니다.

using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct RandomStreamState
    {
        public string StreamId { get; }
        public ulong State { get; }

        /// <summary>Core 캡처/바이너리 해석에서 이름과 내부 상태의 불변 복사본을 만듭니다.</summary>
        public RandomStreamState(string streamId, ulong state)
        {
            ValidateId(streamId);
            StreamId = streamId;
            State = state;
        }

        /// <summary>Core 조립·저장 해석이 난수 흐름의 고정 이름 형식을 검사합니다.</summary>
        public static void ValidateId(string streamId)
        {
            if (string.IsNullOrEmpty(streamId) || streamId.Length > 128)
                throw new ArgumentException("Use a stable stream ID of 1..128 characters.", nameof(streamId));
            foreach (char c in streamId)
                if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '/' && c != '-' && c != '_' && c != '.')
                    throw new ArgumentException(
                        "Stream IDs use lowercase ASCII letters, digits, /, -, _ or .", nameof(streamId));
        }
    }
}
