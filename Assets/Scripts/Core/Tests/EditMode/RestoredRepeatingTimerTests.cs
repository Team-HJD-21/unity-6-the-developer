// 남은 첫 대기와 반복 주기의 분리 및 복원 세대의 취소 경계를 확인합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class RestoredRepeatingTimerTests
    {
        private sealed class Source : ITimestampSource
        { public double Now; public bool TryReadSeconds(out double seconds) { seconds = Now; return true; } }
        [Test]
        public void RestoredFirstDelayKeepsSubsequentIntervalAndCoalescing()
        {
            var source = new Source(); using var clock = new MatchClock(new MatchId("restore"), source);
            using var timers = new MatchScheduler(clock);
            int calls = 0; MatchTimerTick last = default;
            timers.ScheduleRepeating(2, 5, tick => { calls++; last = tick; });
            source.Now = 1; clock.Pump(); timers.Pump(); Assert.That(calls, Is.Zero);
            source.Now = 2; clock.Pump(); timers.Pump(); Assert.That(calls, Is.EqualTo(1));
            source.Now = 17; clock.Pump(); timers.Pump();
            Assert.That(calls, Is.EqualTo(2)); Assert.That(last.ScheduledAt, Is.EqualTo(7));
            Assert.That(last.MissedIntervals, Is.EqualTo(2));
            clock.Restore(1); Assert.That(timers.PendingCount, Is.Zero);
        }
        [Test]
        public void ZeroRemainingWaitsForPumpAndInvalidIntervalsAreRejected()
        {
            using var clock = new MatchClock(new MatchId("restore"), new Source());
            using var timers = new MatchScheduler(clock);
            int calls = 0; timers.ScheduleRepeating(0, 5, _ => calls++);
            Assert.That(calls, Is.Zero); timers.Pump(); Assert.That(calls, Is.EqualTo(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => timers.ScheduleRepeating(0, double.NaN, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => timers.ScheduleRepeating(0, double.PositiveInfinity, _ => { }));
            Assert.Throws<ArgumentOutOfRangeException>(() => timers.ScheduleRepeating(-1, 5, _ => { }));
        }
    }
}
