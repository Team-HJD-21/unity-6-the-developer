// 환경 wire의 미제공 축 보존과 잘린/후행 payload 거부를 검증합니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
public sealed class EnvironmentReplicationCodecTests
{
    [Test]
    public void RoundTripRetainsGenerationForecastAndUnknownTime_RejectsMalformedPayload()
    {
        var frame = new EnvironmentReplicationFrame(Guid.NewGuid(), new MatchEnvironmentSnapshot(new MatchId("match"), 3, 5,
            new DefinitionId("rain"), default, new[] { new DefinitionId("sunny") }));
        byte[] bytes = EnvironmentReplicationCodec.Encode(frame);
        var copy = EnvironmentReplicationCodec.Decode(bytes);
        Assert.That(copy.DescriptorId, Is.EqualTo(frame.DescriptorId));
        Assert.That(copy.Snapshot.HasSameValues(frame.Snapshot), Is.True);
        Assert.That(copy.Snapshot.HasTimeOfDay, Is.False);
        Assert.That(copy.Snapshot.Revision, Is.EqualTo(3));
        Assert.Throws<FormatException>(() => EnvironmentReplicationCodec.Decode(bytes.Take(bytes.Length - 1).ToArray()));
        Assert.Throws<FormatException>(() => EnvironmentReplicationCodec.Decode(bytes.Concat(new byte[] { 0 }).ToArray()));
    }
}
