// 공용 시각과 Field 보류를 분리하고 정지·배속·복원 후 deadline이 보존되는지 확인합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class FieldSimulationClockTests
    {
        private sealed class Clock : IMatchTime
        {
            public double Now;
            public bool Paused;
            public double Rate = 1;
            public double? Anchor;
            public Guid Epoch = Guid.NewGuid();
            public double ElapsedSeconds => Now;
            public bool IsAvailable => true;
            public MatchTimeSnapshot Snapshot => new MatchTimeSnapshot(new MatchId("field-time"), Epoch, 0,
                Anchor ?? Now, Anchor ?? Now, Rate, Paused, true);
        }
        [Test]
        public void HoldingField_PreservesDeadlinesWhileCommonTimeAdvances()
        {
            var common = new Clock { Now = 10 }; var field = new FieldSimulationClock(common);
            double cooldown = field.ElapsedSeconds + 5;
            field.SetHeld(true); common.Now = 100;
            Assert.That(common.ElapsedSeconds, Is.EqualTo(100)); Assert.That(field.ElapsedSeconds, Is.EqualTo(10));
            Assert.That(field.Snapshot.IsPaused, Is.True); Assert.That(cooldown - field.ElapsedSeconds, Is.EqualTo(5));
            field.SetHeld(false); common.Now = 102;
            Assert.That(field.ElapsedSeconds, Is.EqualTo(12)); Assert.That(cooldown - field.ElapsedSeconds, Is.EqualTo(3));
        }
        [Test]
        public void RepeatedHold_IsIdempotent_AndCommonPauseAndRateRemainVisible()
        {
            var common = new Clock { Now = 10 }; var field = new FieldSimulationClock(common);
            field.SetHeld(true); common.Now = 20; field.SetHeld(true); field.SetHeld(false); field.SetHeld(false);
            Assert.That(field.ElapsedSeconds, Is.EqualTo(10));
            common.Paused = true; common.Rate = 2;
            Assert.That(field.Snapshot.IsPaused, Is.True); Assert.That(field.Snapshot.Rate, Is.EqualTo(2));
        }
        [Test]
        public void RestoringWorld_ReplacesOldOffset_AndFieldTimerUsesRemainingDelay()
        {
            var common = new Clock { Now = 100 }; var field = new FieldSimulationClock(common);
            field.Restore(20, true);
            using var timers = new MatchScheduler(field); int calls = 0;
            timers.Schedule(5, _ => calls++); common.Now = 200; timers.Pump(); Assert.That(calls, Is.Zero);
            field.SetHeld(false); common.Now = 204.9; timers.Pump(); Assert.That(calls, Is.Zero);
            common.Now = 205; timers.Pump(); Assert.That(calls, Is.EqualTo(1));
            common.Now = 40; common.Epoch = Guid.NewGuid(); field.Restore(12, false);
            Assert.That(field.ElapsedSeconds, Is.EqualTo(12)); Assert.That(field.IsHeld, Is.False);
            Assert.Throws<ArgumentOutOfRangeException>(() => field.Restore(41, false));
        }
        [Test]
        public void SnapshotReanchorsAfterAFieldHold_EvenWhenTheCommonAnchorIsOld()
        {
            var common = new Clock { Now = 100, Anchor = 0 }; var field = new FieldSimulationClock(common);
            field.Restore(20, false);
            Assert.That(field.Snapshot.GameSecondsAtAnchor, Is.EqualTo(20));
            Assert.That(field.Snapshot.SourceSecondsAtAnchor, Is.EqualTo(100));
            Assert.That(field.Snapshot.EstimateElapsedSeconds(102), Is.EqualTo(22));
        }

        [Test]
        public void EpochChange_WithoutSavedProjection_DropsThePreviousMatchOffset()
        {
            var common = new Clock { Now = 10 }; var field = new FieldSimulationClock(common);
            field.SetHeld(true); common.Now = 100; field.SetHeld(false);
            common.Epoch = Guid.NewGuid(); common.Now = 3;
            Assert.That(field.ElapsedSeconds, Is.EqualTo(3)); Assert.That(field.IsHeld, Is.False);
        }

        [Test]
        public void WorldEpochChangeNeverPublishesAnOlderFieldRevision()
        {
            var common = new Clock { Now = 10 }; var field = new FieldSimulationClock(common);
            for (int i = 0; i < 10; i++) { field.SetHeld(true); field.SetHeld(false); }
            long before = field.Snapshot.Revision;
            common.Epoch = Guid.NewGuid(); common.Now = 3;
            Assert.That(field.Snapshot.Revision, Is.GreaterThan(before));
            before = field.Snapshot.Revision; field.Restore(1, true);
            Assert.That(field.Snapshot.Revision, Is.GreaterThan(before));
        }
    }
}
