// 저장 자료와 별개로 복원 요청에 적용하는 정책입니다. 호출 상황을 결정하지 않습니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class CheckpointRestorePolicy
    {
        public static readonly CheckpointRestorePolicy Default = new();
        public CurrencyRestorePolicy Currency { get; }
        public CheckpointRestorePolicy(CurrencyRestorePolicy currency = CurrencyRestorePolicy.PreserveCurrent)
        { if (!Enum.IsDefined(typeof(CurrencyRestorePolicy), currency)) throw new ArgumentOutOfRangeException(nameof(currency)); Currency = currency; }
        /// <summary>복원 조율부가 writer를 실행하기 전에 확인합니다. 경험치 등 성장 자료는 항상 현재 값을 보존합니다.</summary>
        public bool ShouldRestore(CheckpointSectionKind kind)
        {
            if (!Enum.IsDefined(typeof(CheckpointSectionKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
            return kind == CheckpointSectionKind.World || kind == CheckpointSectionKind.Currency && Currency == CurrencyRestorePolicy.RestoreCheckpoint;
        }
    }
}
