// 환경 snapshot을 네트워크 Match 준비 세대에 연결합니다. 다른 준비의 늦은 상태를 격리합니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class EnvironmentReplicationFrame
    {
        public Guid DescriptorId { get; }
        public MatchEnvironmentSnapshot Snapshot { get; }
        public EnvironmentReplicationFrame(Guid descriptorId, MatchEnvironmentSnapshot snapshot)
        {
            if (descriptorId == Guid.Empty) throw new ArgumentException("A preparation descriptor is required.", nameof(descriptorId));
            DescriptorId = descriptorId; Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }
    }
}
