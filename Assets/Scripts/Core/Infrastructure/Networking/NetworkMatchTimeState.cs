// 서버 전용 NetworkVariable로 보내는 시간 anchor입니다. 프레임별 경과 초는 전송하지 않습니다.
// Match/준비 세대·epoch·revision을 함께 보내 과거 연결의 시간과 혼동하지 않게 합니다.
using System;
using TeamHJD.Game.Domain;
using Unity.Collections;
using Unity.Netcode;

namespace TeamHJD.Game.Infrastructure
{
    public struct NetworkMatchTimeState : INetworkSerializable, IEquatable<NetworkMatchTimeState>
    {
        public FixedString128Bytes MatchId;
        public FixedString64Bytes DescriptorId, Epoch;
        public long Revision, PresenceRevision;
        public byte SpaceshipMask;
        public double GameSeconds, SourceSeconds, Rate;
        public bool Paused, Available;
        public static NetworkMatchTimeState From(Guid descriptor, MatchTimeSnapshot time, long presenceRevision = 0, byte spaceshipMask = 0)
        {
            if (descriptor == Guid.Empty || time == null || presenceRevision < 0 || spaceshipMask > 3) throw new ArgumentException("A prepared authority time is required.");
            return new NetworkMatchTimeState { MatchId = time.MatchId.Value, DescriptorId = descriptor.ToString("N"),
                Epoch = time.Epoch.ToString("N"), Revision = time.Revision, PresenceRevision = presenceRevision, SpaceshipMask = spaceshipMask, GameSeconds = time.GameSecondsAtAnchor,
                SourceSeconds = time.SourceSecondsAtAnchor, Rate = time.Rate, Paused = time.IsPaused, Available = time.IsAvailable };
        }
        /// <summary>서버 anchor의 수신 값을 검증하여 순수 시간 자료로 변환합니다. 빈 초기값/손상 값은 false입니다.</summary>
        public bool TryRead(out Guid descriptor, out MatchTimeSnapshot time)
        {
            descriptor = default; time = null;
            if (PresenceRevision < 0 || SpaceshipMask > 3) return false;
            if (!Guid.TryParseExact(DescriptorId.ToString(), "N", out descriptor) || descriptor == Guid.Empty ||
                !Guid.TryParseExact(Epoch.ToString(), "N", out Guid epoch) || epoch == Guid.Empty) return false;
            try
            {
                time = new MatchTimeSnapshot(new MatchId(MatchId.ToString()), epoch, Revision,
                    GameSeconds, SourceSeconds, Rate, Paused, Available); return true;
            }
            catch (ArgumentException) { return false; }
        }
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref MatchId); serializer.SerializeValue(ref DescriptorId); serializer.SerializeValue(ref Epoch);
            serializer.SerializeValue(ref PresenceRevision); serializer.SerializeValue(ref SpaceshipMask);
            serializer.SerializeValue(ref Revision); serializer.SerializeValue(ref GameSeconds); serializer.SerializeValue(ref SourceSeconds);
            serializer.SerializeValue(ref Rate); serializer.SerializeValue(ref Paused); serializer.SerializeValue(ref Available);
        }
        public bool Equals(NetworkMatchTimeState other) => MatchId.Equals(other.MatchId) && DescriptorId.Equals(other.DescriptorId) &&
            Epoch.Equals(other.Epoch) && Revision == other.Revision && PresenceRevision == other.PresenceRevision && SpaceshipMask == other.SpaceshipMask && GameSeconds.Equals(other.GameSeconds) &&
            SourceSeconds.Equals(other.SourceSeconds) && Rate.Equals(other.Rate) && Paused == other.Paused && Available == other.Available;
        public override bool Equals(object other) => other is NetworkMatchTimeState value && Equals(value);
        public override int GetHashCode() => MatchId.GetHashCode() ^ Epoch.GetHashCode() ^ Revision.GetHashCode();
    }
}
