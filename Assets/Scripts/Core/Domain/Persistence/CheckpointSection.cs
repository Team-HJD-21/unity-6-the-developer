// Feature가 캡처한 버전별 불변 바이너리 자료입니다. Unity 객체·delegate·SDK ID를 담지 않습니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class CheckpointSection
    {
        public const int MaximumPayloadBytes = 4 * 1024 * 1024;
        private readonly byte[] _payload;
        public string Key { get; }
        public int Version { get; }
        public CheckpointSectionKind Kind { get; }
        public int PayloadLength => _payload.Length;
        public CheckpointSection(string key, int version, CheckpointSectionKind kind, byte[] payload)
        {
            if (string.IsNullOrWhiteSpace(key) || key.Length > 128) throw new ArgumentException("A bounded Feature key is required.", nameof(key));
            foreach (char c in key) if (char.IsControl(c)) throw new ArgumentException("Feature keys cannot contain control characters.", nameof(key));
            if (version < 1) throw new ArgumentOutOfRangeException(nameof(version));
            if (!Enum.IsDefined(typeof(CheckpointSectionKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            if (payload == null || payload.Length > MaximumPayloadBytes) throw new ArgumentException("The Feature payload exceeds its storage limit.", nameof(payload));
            Key = key; Version = version; Kind = kind; _payload = (byte[])payload.Clone();
        }
        /// <summary>Feature 해석기/저장 codec에 독립적인 사본을 제공합니다. 원본 체크포인트는 변경되지 않습니다.</summary>
        public byte[] CopyPayload() => (byte[])_payload.Clone();
    }
}
