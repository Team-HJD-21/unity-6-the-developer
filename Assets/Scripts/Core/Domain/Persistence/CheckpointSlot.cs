// 저장 슬롯의 논리 이름입니다. 경로나 플랫폼 사용자 ID를 직접 받지 않습니다.
using System;
namespace TeamHJD.Game.Domain
{
    public readonly struct CheckpointSlot : IEquatable<CheckpointSlot>
    {
        public string Value { get; }
        public bool IsEmpty => string.IsNullOrEmpty(Value);
        public CheckpointSlot(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 64) throw new ArgumentException("Use a slot name of 1–64 ASCII characters.", nameof(value));
            foreach (char c in value)
                if (!(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '-' || c == '_'))
                    throw new ArgumentException("A slot accepts only ASCII letters, digits, '-' and '_'.", nameof(value));
            Value = value;
        }
        public bool Equals(CheckpointSlot other) => StringComparer.Ordinal.Equals(Value, other.Value);
        public override bool Equals(object other) => other is CheckpointSlot slot && Equals(slot);
        public override int GetHashCode() => Value == null ? 0 : StringComparer.Ordinal.GetHashCode(Value);
        public override string ToString() => Value ?? string.Empty;
    }
}
