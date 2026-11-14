// 공용 시간과 Field 투영을 각각 전송했을 때 정지·배속·복원 세대가 분리되는지 검사합니다.
// 실제 두 실행의 NetworkVariable 수신·Player 복제 인수를 대신하지 않습니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Infrastructure.Tests
{
    public sealed class FieldTimeReplicationTests
    {
        private sealed class Source : ITimestampSource
        {
            public double Now = 100;
            public bool TryReadSeconds(out double seconds) { seconds = Now; return true; }
        }
        [Test]
        public void PausedFieldReplicaKeepsCooldownWhileCommonReplicaContinuesAndResumesAtItsOwnDeadline()
        {
            var timestamp = new Source(); var match = new MatchId("field-replication"); var descriptor = Guid.NewGuid();
            using var common = new MatchClock(match, timestamp); var field = new FieldSimulationClock(common);
            timestamp.Now = 110; common.Pump(); field.SetHeld(true);
            using var commonReplica = new MatchTimeReplica(match, descriptor, timestamp);
            using var fieldReplica = new MatchTimeReplica(match, descriptor, timestamp);
            Apply(commonReplica, descriptor, common.Snapshot); Apply(fieldReplica, descriptor, field.Snapshot);
            timestamp.Now = 150; common.Pump(); commonReplica.Pump(); fieldReplica.Pump();
            Assert.That(commonReplica.ElapsedSeconds, Is.EqualTo(50));
            Assert.That(fieldReplica.ElapsedSeconds, Is.EqualTo(10));
            Assert.That(fieldReplica.Snapshot.IsPaused, Is.True);
            field.SetHeld(false); Apply(fieldReplica, descriptor, field.Snapshot);
            timestamp.Now = 153; common.Pump(); commonReplica.Pump(); fieldReplica.Pump();
            Assert.That(commonReplica.ElapsedSeconds, Is.EqualTo(53)); Assert.That(fieldReplica.ElapsedSeconds, Is.EqualTo(13));
        }
        [Test]
        public void FieldWorldRestoreIsAcceptedAfterManyHoldsAndCannotReplayAnOldAnchor()
        {
            var timestamp = new Source(); var match = new MatchId("field-replication"); var descriptor = Guid.NewGuid();
            using var common = new MatchClock(match, timestamp); var field = new FieldSimulationClock(common);
            for (int i = 0; i < 10; i++) { field.SetHeld(true); field.SetHeld(false); }
            using var replica = new MatchTimeReplica(match, descriptor, timestamp);
            var previous = field.Snapshot; Apply(replica, descriptor, previous);
            timestamp.Now = 140; common.Pump(); common.Restore(20); field.Restore(5, true);
            Assert.That(field.Snapshot.Revision, Is.GreaterThan(previous.Revision));
            Apply(replica, descriptor, field.Snapshot);
            Assert.That(replica.ElapsedSeconds, Is.EqualTo(5)); Assert.That(replica.Snapshot.Epoch, Is.EqualTo(common.Snapshot.Epoch));
            Assert.That(replica.TryApply(descriptor, previous, true), Is.False);
            replica.Pump(false); Assert.That(replica.IsAvailable, Is.False);
        }
        [Test]
        public void CommonPauseAndRateApplyToBothAnchorsWithoutAddingASecondAccumulatedClock()
        {
            var timestamp = new Source(); var match = new MatchId("field-replication"); var descriptor = Guid.NewGuid();
            using var common = new MatchClock(match, timestamp); var field = new FieldSimulationClock(common);
            common.SetRate(2); field.Restore(0, false);
            using var replica = new MatchTimeReplica(match, descriptor, timestamp); Apply(replica, descriptor, field.Snapshot);
            timestamp.Now = 103; common.Pump(); replica.Pump(); Assert.That(replica.ElapsedSeconds, Is.EqualTo(6));
            common.SetPaused(true); Apply(replica, descriptor, field.Snapshot);
            timestamp.Now = 120; common.Pump(); replica.Pump(); Assert.That(replica.ElapsedSeconds, Is.EqualTo(6));
        }
        private static void Apply(MatchTimeReplica replica, Guid descriptor, MatchTimeSnapshot snapshot)
        {
            // 실제 anchor가 사용하는 NetworkMatchTimeState 표현을 거쳐 적용합니다.
            var state = NetworkMatchTimeState.From(descriptor, snapshot);
            Assert.That(state.TryRead(out var receivedDescriptor, out var received), Is.True);
            Assert.That(replica.TryApply(receivedDescriptor, received, true), Is.True);
        }
    }
}
