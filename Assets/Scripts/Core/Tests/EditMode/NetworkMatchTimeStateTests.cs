// 실제 NGO serializer에서 시간 anchor를 왕복하고 빈/잘못된 수신 값을 거부하는지 확인합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using Unity.Collections;
using Unity.Netcode;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class NetworkMatchTimeStateTests
    {
        [Test]
        public void NgoSerializerPreservesAuthorityAnchorAndPreparationGeneration()
        {
            var descriptor = Guid.NewGuid(); var epoch = Guid.NewGuid();
            var snapshot = new MatchTimeSnapshot(new MatchId("match"), epoch, 9, 200, 10000000, .5, true, true);
            var state = NetworkMatchTimeState.From(descriptor, snapshot, 2, 3);
            using var writer = new FastBufferWriter(512, Allocator.Temp);
            writer.WriteNetworkSerializable(state);
            using var reader = new FastBufferReader(writer, Allocator.Temp);
            reader.ReadNetworkSerializable(out NetworkMatchTimeState received);
            Assert.That(received.PresenceRevision, Is.EqualTo(2)); Assert.That(received.SpaceshipMask, Is.EqualTo(3));
            Assert.That(received.TryRead(out Guid generation, out var time), Is.True);
            Assert.That(generation, Is.EqualTo(descriptor)); Assert.That(time.Epoch, Is.EqualTo(epoch));
            Assert.That(time.GameSecondsAtAnchor, Is.EqualTo(200)); Assert.That(time.SourceSecondsAtAnchor, Is.EqualTo(10000000));
            Assert.That(time.Rate, Is.EqualTo(.5)); Assert.That(time.IsPaused, Is.True); Assert.That(time.Revision, Is.EqualTo(9));
        }
        [Test]
        public void EmptyInitialNetworkValueAndInvalidNumericAnchorAreNotReadyTime()
        {
            Assert.That(default(NetworkMatchTimeState).TryRead(out _, out _), Is.False);
            var state = NetworkMatchTimeState.From(Guid.NewGuid(), new MatchTimeSnapshot(new MatchId("match"), Guid.NewGuid(), 1, 0, 0, 1, false, true));
            state.Rate = double.NaN; Assert.That(state.TryRead(out _, out _), Is.False);
            state.Rate = 1; state.Epoch = "invalid"; Assert.That(state.TryRead(out _, out _), Is.False);
        }
    }
}
