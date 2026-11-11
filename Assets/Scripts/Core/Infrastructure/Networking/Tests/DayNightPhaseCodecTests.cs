// 낮밤 구간 기준의 v3 왕복과 기존 v2 읽기·잘못된 자료 거절을 검사합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class DayNightPhaseCodecTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void RoundTripPreservesOptionalPhaseAnchorAndLegacyVersion(bool hasAnchor)
        {
            var value = new MatchEnvironmentSnapshot(new MatchId("phase-codec"), 4, 110,
                new DefinitionId("rain"), new DefinitionId("night"), new[] { new DefinitionId("clear") },
                Guid.NewGuid(), hasAnchor ? new DayNightCycle(17, 31, 100, DayNightPhase.Night) : null);
            var frame = new EnvironmentReplicationFrame(Guid.NewGuid(), value);
            byte[] bytes = EnvironmentReplicationCodec.Encode(frame);
            Assert.That(BitConverter.ToInt32(bytes, 0), Is.EqualTo(hasAnchor ? 0x33454A48 : 0x32454A48));
            var copy = EnvironmentReplicationCodec.Decode(bytes);
            Assert.That(copy.DescriptorId, Is.EqualTo(frame.DescriptorId));
            Assert.That(copy.Snapshot.HasSameValues(value), Is.True);
            Assert.That(copy.Snapshot.Epoch, Is.EqualTo(value.Epoch));
            Assert.That(copy.Snapshot.PhaseAnchor == null, Is.EqualTo(!hasAnchor));
        }

        [Test]
        public void InvalidPhaseFutureAnchorTruncationAndTrailingBytesAreRejected()
        {
            var match = new MatchId("phase-codec");
            Assert.Throws<ArgumentException>(() => new MatchEnvironmentSnapshot(match, 1, 10, default,
                new DefinitionId("night"), null, Guid.NewGuid(), new DayNightCycle()));
            Assert.Throws<ArgumentException>(() => new MatchEnvironmentSnapshot(match, 1, 10, default,
                new DefinitionId("day"), null, Guid.NewGuid(), new DayNightCycle(300, 300, 11)));
            var frame = new EnvironmentReplicationFrame(Guid.NewGuid(), new MatchEnvironmentSnapshot(match, 1, 10,
                default, new DefinitionId("day"), null, Guid.NewGuid(), new DayNightCycle()));
            byte[] bytes = EnvironmentReplicationCodec.Encode(frame);
            Array.Resize(ref bytes, bytes.Length - 1);
            Assert.Throws<FormatException>(() => EnvironmentReplicationCodec.Decode(bytes));
            bytes = EnvironmentReplicationCodec.Encode(frame);
            bytes[bytes.Length - 1] = byte.MaxValue;
            Assert.Throws<FormatException>(() => EnvironmentReplicationCodec.Decode(bytes));
            bytes = EnvironmentReplicationCodec.Encode(frame);
            Array.Resize(ref bytes, bytes.Length + 1);
            Assert.Throws<FormatException>(() => EnvironmentReplicationCodec.Decode(bytes));
        }
    }
}
