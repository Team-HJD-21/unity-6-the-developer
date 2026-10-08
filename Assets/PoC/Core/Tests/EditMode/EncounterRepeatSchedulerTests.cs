// 연속 요청의 간격·횟수·일시정지·중복 방지와 자동 중지 정책을 검증합니다.
// 실제 스폰은 공통 Encounter 계약의 통합 테스트에서 별도로 확인합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Editor;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class EncounterRepeatSchedulerTests
    {
        private static bool Success(out string result) { result = "spawned"; return true; }
        private static bool Failure(out string result) { result = "server unavailable"; return false; }
        private static void Tick(EncounterRepeatScheduler scheduler, double delta, bool paused = false,
            string match = "A", bool playing = true, bool ready = true,
            EncounterRepeatScheduler.Request request = null)
            => scheduler.Tick(delta, match, playing, paused, ready, "not ready", request ?? Success);

        [Test]
        public void DefaultOff_ThenWaitsFullIntervalBeforeFirstRequest()
        {
            var scheduler = new EncounterRepeatScheduler();
            Tick(scheduler, 100);
            Assert.That(scheduler.IsRunning, Is.False);
            Assert.That(scheduler.AttemptedRequests, Is.Zero);
            Assert.That(scheduler.Start("A", 2, 10), Is.True);
            Tick(scheduler, 1.9);
            Assert.That(scheduler.AttemptedRequests, Is.Zero);
            Tick(scheduler, 0.2);
            Assert.That(scheduler.CompletedRequests, Is.EqualTo(1));
            Assert.That(scheduler.RemainingSeconds, Is.EqualTo(2));
        }

        [Test]
        public void LongFrame_DoesNotCatchUpOrBurst()
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 1, 0);
            Tick(scheduler, 500);
            Assert.That(scheduler.AttemptedRequests, Is.EqualTo(1));
            Tick(scheduler, 0.01);
            Assert.That(scheduler.AttemptedRequests, Is.EqualTo(1));
            Tick(scheduler, 1);
            Assert.That(scheduler.AttemptedRequests, Is.EqualTo(2));
        }

        [Test]
        public void Pause_DoesNotAccumulateElapsedTime()
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 2, 0);
            Tick(scheduler, 1);
            Tick(scheduler, 100, paused: true);
            Assert.That(scheduler.RemainingSeconds, Is.EqualTo(1));
            Tick(scheduler, 0.5);
            Assert.That(scheduler.CompletedRequests, Is.Zero);
            Tick(scheduler, 0.5);
            Assert.That(scheduler.CompletedRequests, Is.EqualTo(1));
        }

        [Test]
        public void FiniteCount_StopsExactlyAtConfiguredRequests()
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 1, 2);
            Tick(scheduler, 1); Tick(scheduler, 1); Tick(scheduler, 100);
            Assert.That(scheduler.IsRunning, Is.False);
            Assert.That(scheduler.CompletedRequests, Is.EqualTo(2));
            Assert.That(scheduler.StopReason, Does.Contain("횟수"));
        }

        [Test]
        public void ZeroCount_IsExplicitUnlimited()
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 1, 0);
            for (int index = 0; index < 11; index++) Tick(scheduler, 1);
            Assert.That(scheduler.IsRunning, Is.True);
            Assert.That(scheduler.CompletedRequests, Is.EqualTo(11));
        }

        [TestCase("B", true, true)]
        [TestCase(null, true, true)]
        [TestCase("A", false, true)]
        [TestCase("A", true, false)]
        public void InvalidRuntimeState_StopsEvenWhenPaused(string match, bool playing, bool ready)
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 1, 0);
            Tick(scheduler, 100, paused: true, match: match, playing: playing, ready: ready);
            Assert.That(scheduler.IsRunning, Is.False);
            Assert.That(scheduler.AttemptedRequests, Is.Zero);
            Assert.That(scheduler.StopReason, Is.Not.Empty);
        }

        [Test]
        public void Failure_StopsAndPreservesReason()
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 1, 0);
            Tick(scheduler, 1, request: Failure); Tick(scheduler, 1);
            Assert.That(scheduler.IsRunning, Is.False);
            Assert.That(scheduler.AttemptedRequests, Is.EqualTo(1));
            Assert.That(scheduler.CompletedRequests, Is.Zero);
            Assert.That(scheduler.StopReason, Does.Contain("server unavailable"));
        }

        [Test]
        public void Exception_StopsAndPreservesReason()
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 1, 0);
            bool Throw(out string result) { result = null; throw new InvalidOperationException("broken"); }
            Tick(scheduler, 1, request: Throw);
            Assert.That(scheduler.IsRunning, Is.False);
            Assert.That(scheduler.StopReason, Does.Contain("InvalidOperationException: broken"));
        }

        [Test]
        public void ReentryAndDoubleStart_DoNotDuplicateRequests()
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 1, 0);
            Assert.That(scheduler.Start("A", 1, 0), Is.False);
            bool Reenter(out string result)
            {
                Tick(scheduler, 100);
                Assert.That(scheduler.Start("A", 1, 0), Is.False);
                return Success(out result);
            }
            Tick(scheduler, 1, request: Reenter);
            Assert.That(scheduler.AttemptedRequests, Is.EqualTo(1));
        }

        [Test]
        public void ManualStopAndRestart_ResetCounts()
        {
            var scheduler = new EncounterRepeatScheduler(); scheduler.Start("A", 1, 0);
            Tick(scheduler, 1); scheduler.Stop("manual"); Tick(scheduler, 100);
            Assert.That(scheduler.CompletedRequests, Is.EqualTo(1));
            Assert.That(scheduler.Start("B", 2, 5), Is.True);
            Assert.That(scheduler.CompletedRequests, Is.Zero);
            Assert.That(scheduler.RemainingSeconds, Is.EqualTo(2));
        }

        [Test]
        public void InvalidSettingsAndClock_DoNotRunOrRequest()
        {
            var scheduler = new EncounterRepeatScheduler();
            Assert.That(scheduler.Start("A", 0, 1), Is.False);
            Assert.That(scheduler.Start("A", double.NaN, 1), Is.False);
            Assert.That(scheduler.Start("A", double.PositiveInfinity, 1), Is.False);
            Assert.That(scheduler.Start(null, 1, 1), Is.False);
            Assert.That(scheduler.Start("A", 1, -1), Is.False);
            Assert.That(scheduler.Start("A", 1, 10001), Is.False);
            scheduler.Start("A", 1, 1);
            Tick(scheduler, double.NaN); Tick(scheduler, double.PositiveInfinity); Tick(scheduler, -1); Tick(scheduler, 0);
            Assert.That(scheduler.AttemptedRequests, Is.Zero);
            Assert.That(scheduler.RemainingSeconds, Is.EqualTo(1));
        }
    }
}
