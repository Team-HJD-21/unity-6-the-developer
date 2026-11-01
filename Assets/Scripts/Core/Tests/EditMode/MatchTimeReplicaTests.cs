// Client 시간의 준비·권위/준비 세대·revision·정지/복원·원본 손실을 검증합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchTimeReplicaTests
    {
        private sealed class Source : ITimestampSource
        { public double Now; public bool Ready = true; public bool TryReadSeconds(out double seconds) { seconds = Now; return Ready; } }
        private static MatchTimeSnapshot Anchor(long revision, double game = 10, double rate = 1, bool paused = false, Guid epoch = default)
            => new MatchTimeSnapshot(new MatchId("match"), epoch == Guid.Empty ? Guid.NewGuid() : epoch, revision, game, 100, rate, paused, true);
        [Test]
        public void ReadinessAndLateAnchorUseSharedSourceWithoutAnIndependentGameClock()
        {
            var generation = Guid.NewGuid(); var source = new Source { Now = 103 };
            using var replica = new MatchTimeReplica(new MatchId("match"), generation, source);
            Assert.That(replica.IsAvailable, Is.False); Assert.That(replica.HasSnapshot, Is.False);
            replica.Pump(); Assert.That(replica.IsAvailable, Is.False);
            Assert.That(replica.TryApply(generation, Anchor(1, rate: 2), true), Is.True);
            Assert.That(replica.ElapsedSeconds, Is.EqualTo(16));
            source.Now = 104; replica.Pump(); Assert.That(replica.ElapsedSeconds, Is.EqualTo(18));
        }
        [Test]
        public void WrongAuthorityGenerationAndPastRevisionCannotReplaceTime()
        {
            var generation = Guid.NewGuid(); using var replica = new MatchTimeReplica(new MatchId("match"), generation, new Source());
            Assert.That(replica.TryApply(generation, Anchor(2), true), Is.True);
            Assert.That(replica.TryApply(generation, Anchor(3), false), Is.False);
            Assert.That(replica.TryApply(Guid.NewGuid(), Anchor(3), true), Is.False);
            Assert.That(replica.TryApply(generation, Anchor(1), true), Is.False);
            var other = new MatchTimeSnapshot(new MatchId("other"), Guid.NewGuid(), 3, 0, 0, 1, false, true);
            Assert.That(replica.TryApply(generation, other, true), Is.False);
            Assert.That(replica.Snapshot.Revision, Is.EqualTo(2));
        }
        [Test]
        public void NewPauseAndRestoreAnchorsCorrectPredictionIncludingBackwardGameTime()
        {
            var generation = Guid.NewGuid(); var source = new Source { Now = 200 };
            using var replica = new MatchTimeReplica(new MatchId("match"), generation, source);
            replica.TryApply(generation, Anchor(1), true); Assert.That(replica.ElapsedSeconds, Is.EqualTo(110));
            replica.TryApply(generation, Anchor(2, 20, paused: true), true);
            Assert.That(replica.ElapsedSeconds, Is.EqualTo(20)); source.Now += 50; replica.Pump();
            Assert.That(replica.ElapsedSeconds, Is.EqualTo(20));
            var epoch = Guid.NewGuid(); replica.TryApply(generation, Anchor(3, 5, paused: true, epoch: epoch), true);
            Assert.That(replica.ElapsedSeconds, Is.EqualTo(5)); Assert.That(replica.Snapshot.Epoch, Is.EqualTo(epoch));
        }
        [Test]
        public void LostSourceFreezesAndSameAnchorSourceCorrectionDoesNotReverseDisplay()
        {
            var generation = Guid.NewGuid(); var source = new Source { Now = 110 };
            using var replica = new MatchTimeReplica(new MatchId("match"), generation, source);
            replica.TryApply(generation, Anchor(1), true);
            source.Now = 109; replica.Pump(); Assert.That(replica.ElapsedSeconds, Is.EqualTo(20));
            source.Ready = false; replica.Pump(); Assert.That(replica.IsAvailable, Is.False);
            source.Ready = true; source.Now = 120; replica.Pump(false); Assert.That(replica.IsAvailable, Is.False);
            replica.Pump(); Assert.That(replica.ElapsedSeconds, Is.EqualTo(30));
            replica.Dispose(); Assert.That(replica.IsAvailable, Is.False);
            Assert.Throws<ObjectDisposedException>(() => replica.Pump());
        }
    }
}
