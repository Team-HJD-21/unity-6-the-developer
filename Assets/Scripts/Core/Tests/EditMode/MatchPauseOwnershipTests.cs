// 참가자 정책과 중첩 임시 정지의 독립 수명·준비 중 복원·종료/스레드 경계를 검증합니다.
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchPauseOwnershipTests
    {
        private sealed class Source : ITimestampSource
        { public double Now; public bool TryReadSeconds(out double seconds) { seconds = Now; return true; } }
        private static MatchSession Session()
        {
            var match = new MatchId("pause"); var player = new PlayerId("host");
            return new MatchSessionFactory(new LocalAuthority()).Create(new MatchConfig(match, MatchMode.Story,
                new StageId("stage"), new DefinitionId("normal"), "content", 1, new[] { new PlayerLoadoutSnapshot(player, null) }),
                new MatchState(match, MatchPhase.Preparing, new[] { new PlayerState(player, 100, 100, true) },
                    new ControlUnitState(100, 100, 100, 100), new WaveState(0, 1, 0, 0), Array.Empty<TurretState>(),
                    Array.Empty<EnemyState>(), Array.Empty<SectorState>()), new StoryModeRules());
        }
        [Test] public void ReleasingCaptureCannotOverrideSpaceshipPolicyOrAnotherLease()
        {
            using var session = Session(); session.Start(); var source = new Source();
            using var time = new MatchTimeService(session, source, () => true);
            time.SetPaused(true); var first = time.AcquirePause("capture"); var second = time.AcquirePause("capture");
            first.Dispose(); Assert.That(time.PauseOwners.Count, Is.EqualTo(1));
            Assert.That(time.Clock.Snapshot.IsPaused, Is.True);
            time.SetPaused(false); Assert.That(time.Clock.Snapshot.IsPaused, Is.True);
            source.Now = 100; time.Pump(1); Assert.That(time.Clock.ElapsedSeconds, Is.Zero);
            second.Dispose(); source.Now = 101; time.Pump(1); Assert.That(time.Clock.ElapsedSeconds, Is.EqualTo(1));
        }
        [Test] public void PreparingRestoreRemainsPausedUntilItsOwnerFinishes()
        {
            using var session = Session(); var source = new Source();
            using var time = new MatchTimeService(session, source, () => true);
            source.Now = 10; time.Pump(1);
            Assert.That(time.Clock.ElapsedSeconds, Is.Zero, "Preparing time must not count as gameplay.");
            using var pause = time.AcquirePause("restore"); time.Restore(60);
            source.Now = 20; time.Pump(1); Assert.That(time.Clock.ElapsedSeconds, Is.EqualTo(60));
            Assert.That(time.Clock.Snapshot.IsPaused, Is.True);
            session.Start(); pause.Dispose(); source.Now = 21; time.Pump(1);
            Assert.That(time.Clock.ElapsedSeconds, Is.EqualTo(61));
        }
        [Test] public void CompletedGameplayCannotResumeWhenAnOldLeaseIsReleased()
        {
            using var session = Session(); session.Start();
            using var time = new MatchTimeService(session, new Source(), () => true); var pause = time.AcquirePause("restore");
            session.Complete(MatchOutcome.Defeat, 1000); pause.Dispose();
            Assert.That(time.Clock.Snapshot.IsPaused, Is.True); Assert.That(time.PauseOwners, Is.Empty);
            Assert.Throws<InvalidOperationException>(() => time.AcquirePause("stale"));
        }
        [Test] public void WorkerThreadCannotResumeTheClockAndTheLeaseCanStillBeReleasedByItsOwner()
        {
            using var session = Session(); session.Start();
            using var time = new MatchTimeService(session, new Source(), () => true); var pause = time.AcquirePause("capture");
            Assert.Throws<InvalidOperationException>(() => Task.Run(pause.Dispose).GetAwaiter().GetResult());
            Assert.That(time.Clock.Snapshot.IsPaused, Is.True); pause.Dispose(); Assert.That(time.Clock.Snapshot.IsPaused, Is.False);
        }
    }
}
