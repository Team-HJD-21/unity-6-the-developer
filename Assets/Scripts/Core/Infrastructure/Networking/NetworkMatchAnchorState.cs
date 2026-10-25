// 최소 Match 네트워크 객체가 표시할 확정 메타데이터를 원자적으로 전달합니다.
// 지역 Grid의 원본이나 전체 Match 상태가 아니며 Client는 이 값으로 정책을 재계산하지 않습니다.
using System;
using Unity.Collections;
using Unity.Netcode;

namespace TeamHJD.Game.Infrastructure
{
    public struct NetworkMatchAnchorState : INetworkSerializable, IEquatable<NetworkMatchAnchorState>
    {
        public FixedString128Bytes MatchId;
        public FixedString64Bytes DescriptorId;
        public FixedString128Bytes StageId;
        public FixedString64Bytes RegionGridId;
        public int Seed;
        public long RegionRevision;
        public int CapturedCells;
        public int RegionMode;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MatchId); serializer.SerializeValue(ref DescriptorId);
            serializer.SerializeValue(ref StageId); serializer.SerializeValue(ref Seed);
            serializer.SerializeValue(ref RegionGridId);
            serializer.SerializeValue(ref RegionRevision); serializer.SerializeValue(ref CapturedCells);
            serializer.SerializeValue(ref RegionMode);
        }
        public bool Equals(NetworkMatchAnchorState other) => MatchId.Equals(other.MatchId) &&
            DescriptorId.Equals(other.DescriptorId) && StageId.Equals(other.StageId) && Seed == other.Seed &&
            RegionGridId.Equals(other.RegionGridId) &&
            RegionRevision == other.RegionRevision && CapturedCells == other.CapturedCells && RegionMode == other.RegionMode;
        public override bool Equals(object other) => other is NetworkMatchAnchorState value && Equals(value);
        public override int GetHashCode() => MatchId.GetHashCode() ^ DescriptorId.GetHashCode() ^ RegionRevision.GetHashCode();
    }
}
