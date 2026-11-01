// 공용 시계·예약의 정지/배속·시간 불연속·취소·callback 재진입 경계를 검증합니다.
// 실제 SDK·Scene 바인딩 검사는 별도이며 이 검사는 수동 timestamp로 계약을 확인합니다.
using System;
using System.Collections.Generic;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class MatchTimeTests
    {
        private sealed class Source : ITimestampSource
        {
            public double Now = 10000000;
            public bool Ready = true;
            public bool TryReadSeconds(out double seconds) { seconds = Now; return Ready; }
        }
        private static MatchClock Clock(Source source) => new MatchClock(new MatchId("time-test"), source);

        [Test]
        public void AnchorPreservesElapsedAcrossRateAndPauseWithoutFrameAccumulation()
        {
            var source = new Source(); using var clock = Clock(source);
            source.Now += 3600; clock.Pump(); Assert.That(clock.ElapsedSeconds, Is.EqualTo(3600));
            clock.SetRate(2); source.Now += 5; clock.Pump();
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(3610));
            clock.SetPaused(true); source.Now += 300; clock.Pump();
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(3610));
            clock.SetPaused(false); source.Now += 3; clock.Pump();
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(3616));
            Assert.That(clock.Snapshot.EstimateElapsedSeconds(source.Now), Is.EqualTo(clock.ElapsedSeconds));
        }

        [Test]
        public void UnavailableSourceDoesNotTurnAValidZeroIntoReadinessOrCatchUpTheGap()
        {
            var source = new Source { Now = 0, Ready = false }; using var clock = Clock(source);
            Assert.That(clock.IsAvailable, Is.False);
            source.Ready = true; Assert.That(clock.Pump(), Is.True);
            source.Now = 5; clock.Pump(); source.Ready = false; clock.Pump();
            source.Now = 100; source.Ready = true; clock.Pump();
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(5));
            source.Now = 101; clock.Pump(); Assert.That(clock.ElapsedSeconds, Is.EqualTo(6));
        }

        [Test]
        public void BackwardsTimestampFreezesUntilExplicitRestoreCreatesANewEpoch()
        {
            var source = new Source(); using var clock = Clock(source);
            Guid before = clock.Snapshot.Epoch;
            source.Now += 5; clock.Pump(); source.Now -= 1;
            Assert.That(clock.Pump(), Is.False); Assert.That(clock.RequiresReset, Is.True);
            Assert.That(clock.ElapsedSeconds, Is.EqualTo(5));
            clock.Restore(2); Assert.That(clock.Snapshot.Epoch, Is.Not.EqualTo(before));
            source.Now += 1; clock.Pump(); Assert.That(clock.ElapsedSeconds, Is.EqualTo(3));
        }

        [Test]
        public void DueOrderIsStableAndCallbackCreatedWorkWaitsForNextPump()
        {
            var source = new Source(); using var clock = Clock(source); using var scheduler = new MatchScheduler(clock);
            var calls = new List<int>();
            scheduler.Schedule(1, _ => { calls.Add(1); scheduler.Schedule(0, __ => calls.Add(3)); });
            scheduler.Schedule(1, _ => calls.Add(2)); source.Now += 1; clock.Pump();
            scheduler.Pump(); Assert.That(calls, Is.EqualTo(new[] { 1, 2 }));
            scheduler.Pump(); Assert.That(calls, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [Test]
        public void LongFrameCoalescesRepeatsAndKeepsTheOriginalIntervalPhase()
        {
            var source = new Source(); using var clock = Clock(source); using var scheduler = new MatchScheduler(clock);
            var ticks = new List<MatchTimerTick>(); scheduler.ScheduleRepeating(2, ticks.Add);
            source.Now += 9; clock.Pump(); scheduler.Pump();
            Assert.That(ticks.Count, Is.EqualTo(1)); Assert.That(ticks[0].ScheduledAt, Is.EqualTo(2));
            Assert.That(ticks[0].MissedIntervals, Is.EqualTo(3));
            source.Now += 1; clock.Pump(); scheduler.Pump();
            Assert.That(ticks.Count, Is.EqualTo(2)); Assert.That(ticks[1].ScheduledAt, Is.EqualTo(10));
        }

        [Test]
        public void CancellationAndLeaseDisposalReleaseCapacityAndNeverInvokeCallbacks()
        {
            var source = new Source(); using var clock = Clock(source); using var scheduler = new MatchScheduler(clock, 2);
            using var token = new CancellationTokenSource(); int calls = 0;
            scheduler.Schedule(1, _ => calls++, token.Token);
            IDisposable lease = scheduler.Schedule(1, _ => calls++);
            token.Cancel(); lease.Dispose(); Assert.That(scheduler.PendingCount, Is.Zero);
            scheduler.Schedule(1, _ => calls++); source.Now += 1; clock.Pump(); scheduler.Pump();
            Assert.That(calls, Is.EqualTo(1));
        }

        [Test]
        public void PumpBudgetDefersWorkAndCallbackFailureDoesNotSkipOtherDueCallbacks()
        {
            var source = new Source(); using var clock = Clock(source); using var scheduler = new MatchScheduler(clock, 10, 2);
            int calls = 0;
            scheduler.Schedule(0, _ => throw new InvalidOperationException("feature failure"));
            scheduler.Schedule(0, _ => calls++); scheduler.Schedule(0, _ => calls++);
            Assert.Throws<AggregateException>(scheduler.Pump); Assert.That(calls, Is.EqualTo(1));
            scheduler.Pump(); Assert.That(calls, Is.EqualTo(2));
        }

        [Test]
        public void PauseInsideCallbackRetainsTheRemainingDueWorkAndRestoreCancelsTheOldEpoch()
        {
            var source = new Source(); using var clock = Clock(source); using var scheduler = new MatchScheduler(clock);
            int calls = 0;
            scheduler.Schedule(0, _ => clock.SetPaused(true)); scheduler.Schedule(0, _ => calls++);
            scheduler.Pump(); Assert.That(calls, Is.Zero); Assert.That(scheduler.PendingCount, Is.EqualTo(1));
            clock.SetPaused(false); scheduler.Pump(); Assert.That(calls, Is.EqualTo(1));
            scheduler.Schedule(0, _ => calls++); clock.Restore(0); scheduler.Pump();
            Assert.That(calls, Is.EqualTo(1)); Assert.That(scheduler.PendingCount, Is.Zero);
        }

        [Test]
        public void CallbackDisposalAndEpochResetDoNotExecuteDetachedWork()
        {
            var source = new Source(); using var clock = Clock(source); using var scheduler = new MatchScheduler(clock);
            int calls = 0;
            scheduler.Schedule(0, _ => clock.Restore(0)); scheduler.Schedule(0, _ => calls++);
            scheduler.Pump(); Assert.That(calls, Is.Zero);
            scheduler.Schedule(0, _ => scheduler.Dispose()); scheduler.Schedule(0, _ => calls++);
            Assert.DoesNotThrow(scheduler.Pump); Assert.That(calls, Is.Zero);
        }

        [Test]
        public void InvalidDurationsAndAnOverflowingDeadlineAreRejectedWithoutRegisteringWork()
        {
            var source = new Source(); using var clock = Clock(source); using var scheduler = new MatchScheduler(clock);
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(double.NaN, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.ScheduleRepeating(0, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetRate(double.PositiveInfinity));
            clock.Restore(double.MaxValue);
            Assert.Throws<ArgumentOutOfRangeException>(() => scheduler.Schedule(1, _ => { }));
            Assert.That(scheduler.PendingCount, Is.Zero);
        }

        [Test]
        public void MatchLifetimeCancellationInsideCallbackStopsRemainingAndFutureWork()
        {
            var source = new Source(); using var clock = Clock(source); using var lifetime = new CancellationTokenSource();
            using var scheduler = new MatchScheduler(clock, lifetime: lifetime.Token);
            int calls = 0;
            scheduler.Schedule(0, _ => lifetime.Cancel()); scheduler.Schedule(0, _ => calls++);
            scheduler.Pump(); Assert.That(calls, Is.Zero); Assert.That(scheduler.PendingCount, Is.Zero);
            Assert.Throws<OperationCanceledException>(() => scheduler.Schedule(0, _ => calls++));
        }
    }
}
