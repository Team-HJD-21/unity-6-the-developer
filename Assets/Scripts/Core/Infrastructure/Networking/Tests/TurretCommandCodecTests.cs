// 명령 wire 왕복과 잘못된 입력/길이/세대/역할별 payload를 검사합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class TurretCommandCodecTests
    {
        [Test]
        public void AllCommandsAndResultsRoundTripWithCorrelationAndUnicode()
        {
            Guid descriptor = Guid.NewGuid(); var context = new MatchRequestContext(new MatchId("match"), Guid.NewGuid(), new PlayerId("player"));
            foreach (TurretCommandKind kind in Enum.GetValues(typeof(TurretCommandKind)))
            {
                var target = kind == TurretCommandKind.StartAutoActivation ? default : new EntityId("turret/대상");
                var upgrade = kind == TurretCommandKind.ApplyUpgrade || kind == TurretCommandKind.DowngradeUpgrade ? new DefinitionId("강화") : default;
                var request = new TurretCommandRequest(context, kind, target, kind == TurretCommandKind.SetActivation, upgrade);
                TurretCommandCodec.Decode(TurretCommandCodec.Encode(descriptor, request), out var copyDescriptor, out var copy, out var none);
                Assert.That(copyDescriptor, Is.EqualTo(descriptor)); Assert.That(none, Is.Null);
                Assert.That(copy.Context.RequestId, Is.EqualTo(context.RequestId)); Assert.That(copy.Context.IssuerId, Is.EqualTo(context.IssuerId));
                Assert.That(copy.GetPayloadKey(), Is.EqualTo(request.GetPayloadKey()));
                var result = new TurretCommandResult(context.MatchId, context.RequestId, kind, target, 12, TurretCommandStatus.FeatureRejected, "D거부", "조건 미충족", true);
                TurretCommandCodec.Decode(TurretCommandCodec.Encode(descriptor, result), out _, out var noRequest, out var response);
                Assert.That(noRequest, Is.Null); Assert.That(response.Status, Is.EqualTo(result.Status));
                Assert.That(response.TargetId, Is.EqualTo(target)); Assert.That(response.ExecutionSequence, Is.EqualTo(12));
                Assert.That(response.IsReplay, Is.True); Assert.That(response.Reason, Is.EqualTo("조건 미충족"));
            }
        }
        [Test]
        public void MalformedTruncatedTrailingAndOversizedMessagesAreRejected()
        {
            var request = new TurretCommandRequest(new MatchRequestContext(new MatchId("m"), Guid.NewGuid(), new PlayerId("p")),
                TurretCommandKind.SetActivation, new EntityId("turret/id"), true);
            Assert.Throws<ArgumentException>(() => TurretCommandCodec.Encode(Guid.Empty, request));
            byte[] original = TurretCommandCodec.Encode(Guid.NewGuid(), request);
            for (int length = 0; length < original.Length; length++)
            {
                var truncated = new byte[length]; Array.Copy(original, truncated, length);
                Assert.Throws<FormatException>(() => TurretCommandCodec.Decode(truncated, out _, out _, out _));
            }
            var trailing = new byte[original.Length + 1]; Array.Copy(original, trailing, original.Length);
            Assert.Throws<FormatException>(() => TurretCommandCodec.Decode(trailing, out _, out _, out _));
            Assert.Throws<FormatException>(() => TurretCommandCodec.Decode(new byte[TurretCommandCodec.MaximumMessageBytes + 1], out _, out _, out _));
            original[4] = 255;
            Assert.Throws<FormatException>(() => TurretCommandCodec.Decode(original, out _, out _, out _));
        }
    }
}
