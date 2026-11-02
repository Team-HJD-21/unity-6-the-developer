// 최대 전체 표시 투영의 왕복과 잘림·거대 길이·잘못된 bool·후행 데이터를 확인합니다.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class TurretPresentationCodecTests
    {
        private static TurretPresentationFrame Frame(int count, bool maximumText = false)
        {
            var rows = new List<TurretPresentationSnapshot>();
            for (int index = 0; index < count; index++)
                rows.Add(new TurretPresentationSnapshot(new EntityId(maximumText ? index.ToString("D4") + new string('가', 124) : "turret/" + index),
                    new DefinitionId(maximumText ? new string('나', 128) : "canon"), maximumText ? new string('다', 256) : "캐논",
                    new BattlefieldPoint(index, -index), 3, 91, 100, 10, 12, 14.5, 2.3,
                    new DefinitionId(maximumText ? new string('라', 128) : "power"), 5,
                    TurretPresentationFlags.Initialized | TurretPresentationFlags.Activated | TurretPresentationFlags.Operational));
            return new TurretPresentationFrame(new MatchId("match"), Guid.NewGuid(), 9,
                new TurretControllerPresentationSnapshot(100, 60, 30, 10, 900, 1000, new BattlefieldPoint(1, 2), true, 12.5, RegionPolicyMode.Dynamic, false, Guid.NewGuid(), 6), rows, 17);
        }
        [Test]
        public void MaximumCollectionAndTextFitWireAndPreserveEveryField()
        {
            var frame = Frame(TurretPresentationFrame.MaximumTurrets, true); byte[] bytes = TurretPresentationCodec.Encode(frame);
            Assert.That(bytes.Length, Is.GreaterThan(1000000)); Assert.That(bytes.Length, Is.LessThanOrEqualTo(TurretPresentationCodec.MaximumMessageBytes));
            var copy = TurretPresentationCodec.Decode(bytes); Assert.That(copy.HasSameContent(frame), Is.True);
            Assert.That(copy.Revision, Is.EqualTo(9)); Assert.That(copy.ConfirmedCommandSequence, Is.EqualTo(17));
            Assert.That(copy.Turrets.Count, Is.EqualTo(TurretPresentationFrame.MaximumTurrets));
            Assert.Throws<ArgumentException>(() => Frame(TurretPresentationFrame.MaximumTurrets + 1));
        }
        [Test]
        public void MalformedLengthsTruncationBoolAndTrailingDataFailClosed()
        {
            byte[] bytes = TurretPresentationCodec.Encode(Frame(3));
            foreach (int length in new[] { 0, 1, 4, 30, 80, bytes.Length - 1, bytes.Length / 2 })
            {
                var prefix = new byte[length]; Array.Copy(bytes, prefix, length);
                Assert.Throws<FormatException>(() => TurretPresentationCodec.Decode(prefix));
            }
            var trailing = new byte[bytes.Length + 1]; Array.Copy(bytes, trailing, bytes.Length);
            Assert.Throws<FormatException>(() => TurretPresentationCodec.Decode(trailing));
            Assert.Throws<FormatException>(() => TurretPresentationCodec.Decode(new byte[TurretPresentationCodec.MaximumMessageBytes + 1]));
            int firstBoolean = 4 + 2 + 5 + 16 + 8 + 8 + 24 + 16;
            bytes[firstBoolean] = 2; Assert.Throws<FormatException>(() => TurretPresentationCodec.Decode(bytes));
            bytes = TurretPresentationCodec.Encode(Frame(3)); bytes[0] ^= 0xff;
            Assert.Throws<FormatException>(() => TurretPresentationCodec.Decode(bytes));
        }
    }
}
