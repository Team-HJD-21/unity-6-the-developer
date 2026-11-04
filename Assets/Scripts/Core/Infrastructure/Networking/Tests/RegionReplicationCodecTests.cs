// 지역 정의·점령 bitset의 실제 wire 표현과 손상·최대 크기를 검증합니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class RegionReplicationCodecTests
    {
        private static RegionReplicationFrame Frame(int x = 3, int y = 3)
        {
            var grid = new BattlefieldGridConfiguration(new BattlefieldPoint(40, -12), 2, 21, 35, x, y);
            var definition = new RegionGridDefinition(grid, Enumerable.Range(0, x * y).Select(i => i % 3).ToArray());
            return new RegionReplicationFrame(Guid.NewGuid(), 2,
                RegionGridSnapshot.FromCapturedCells(new MatchId("match"), Guid.NewGuid(), 11, RegionPolicyMode.Dynamic,
                    definition, new[] { 0, x * y - 1 }));
        }
        [Test]
        public void RoundTripPreservesFullDefinitionCaptureAndEpoch()
        {
            var frame = Frame(); var copy = RegionReplicationCodec.Decode(RegionReplicationCodec.Encode(frame));
            Assert.That(copy.MatchDescriptorId, Is.EqualTo(frame.MatchDescriptorId));
            Assert.That(copy.GridGeneration, Is.EqualTo(2));
            Assert.That(copy.Snapshot.GridId, Is.EqualTo(frame.Snapshot.GridId));
            Assert.That(copy.Snapshot.Revision, Is.EqualTo(11));
            Assert.That(copy.Snapshot.Definition.HasSameDefinition(frame.Snapshot.Definition), Is.True);
            Assert.That(copy.Snapshot.CapturedCells, Is.EqualTo(frame.Snapshot.CapturedCells));
        }
        [Test]
        public void RejectsTruncationTrailingBytesAndOutOfGridBits()
        {
            var frame = Frame(); byte[] bytes = RegionReplicationCodec.Encode(frame);
            Array.Resize(ref bytes, bytes.Length - 1);
            Assert.Throws<FormatException>(() => RegionReplicationCodec.Decode(bytes));
            bytes = RegionReplicationCodec.Encode(frame); Array.Resize(ref bytes, bytes.Length + 1);
            Assert.Throws<FormatException>(() => RegionReplicationCodec.Decode(bytes));
            bytes = RegionReplicationCodec.Encode(frame); bytes[bytes.Length - 1] |= 128;
            Assert.Throws<FormatException>(() => RegionReplicationCodec.Decode(bytes));
        }
        [Test]
        public void MaximumSupportedGridRemainsWithinBoundedWireFormat()
        {
            var frame = Frame(512, 512); byte[] bytes = RegionReplicationCodec.Encode(frame);
            Assert.That(bytes.Length, Is.LessThan(NetworkMessageLimits.MaximumMessageBytes));
            var copy = RegionReplicationCodec.Decode(bytes);
            Assert.That(copy.Snapshot.CapturedCellCount, Is.EqualTo(2));
            Assert.That(copy.Snapshot.Definition.RegionIds, Is.EqualTo(frame.Snapshot.Definition.RegionIds));
        }
    }
}
