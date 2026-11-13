// 정적 확보의 성과·참가자·압력·이탈·복원 경계를 게임 시각으로 검사합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests
{
    public sealed class StaticRegionCaptureTests
    {
        private static readonly StaticRegionCaptureSettings Settings = new StaticRegionCaptureSettings();

        [Test]
        public void WaitingWithoutCombatNeverCaptures()
        {
            var state = new StaticRegionCapture(1);
            state.Tick(0, true, 1, 0, Settings);
            state.Tick(600, true, 1, 0, Settings);
            Assert.That(state.HoldSeconds, Is.Zero);
            Assert.That(state.Reason, Is.EqualTo(StaticRegionCaptureReason.NeedsCombat));
        }

        [TestCase(1)]
        [TestCase(2)]
        public void ClearedAreaAfterTeamCombatCompletesInSixtySeconds(int participants)
        {
            var state = new StaticRegionCapture(1);
            state.RecordDamage(20);
            state.Tick(0, true, participants, 0, Settings);
            Assert.That(state.Tick(59, true, participants, 0, Settings), Is.False);
            Assert.That(state.Tick(60, true, participants, 0, Settings), Is.True);
            Assert.That(state.HoldSeconds, Is.EqualTo(60));
            Assert.That(state.IsSecured, Is.True);
        }

        [Test]
        public void EnemyPressurePausesHoldAndDoesNotEraseCombat()
        {
            var state = new StaticRegionCapture(1);
            state.RecordDamage(20);
            state.Tick(0, true, 1, 0, Settings);
            state.Tick(20, true, 1, 0, Settings);
            state.Tick(40, true, 1, 3, Settings);
            Assert.That(state.HoldSeconds, Is.EqualTo(20));
            Assert.That(state.TeamDamage, Is.EqualTo(20));
            Assert.That(state.Tick(80, true, 1, 0, Settings), Is.False);
            Assert.That(state.Tick(120, true, 1, 0, Settings), Is.True);
        }

        [Test]
        public void LeavingPausesAndOnlyAbandonsAfterGrace()
        {
            var state = new StaticRegionCapture(1);
            state.RecordDamage(20);
            state.Tick(0, true, 1, 0, Settings);
            state.Tick(30, true, 1, 0, Settings);
            state.Tick(30, true, 0, 0, Settings);
            state.Tick(209, true, 0, 0, Settings);
            Assert.That(state.HoldSeconds, Is.EqualTo(30));
            state.Tick(210, true, 0, 0, Settings);
            Assert.That(state.HoldSeconds, Is.Zero);
            Assert.That(state.TeamDamage, Is.Zero);
            Assert.That(state.Reason, Is.EqualTo(StaticRegionCaptureReason.Abandoned));
        }

        [Test]
        public void EmptySecuredRegionStaysSecuredButThirtySecondsOfSuperiorityLosesIt()
        {
            var state = new StaticRegionCapture(1);
            state.Restore(60, 20, 0, 0, true);
            state.Tick(0, true, 0, 0, Settings);
            state.Tick(200, true, 0, 0, Settings);
            Assert.That(state.IsSecured, Is.True);
            state.Tick(200, true, 0, 1, Settings);
            state.Tick(229, true, 0, 1, Settings);
            Assert.That(state.IsSecured, Is.True);
            Assert.That(state.Tick(230, true, 0, 1, Settings), Is.True);
            Assert.That(state.IsSecured, Is.False);
        }

        [Test]
        public void RestoringProgressDoesNotAccumulatePreviousClockGap()
        {
            var state = new StaticRegionCapture(1);
            state.Restore(35, 20, 0, 0, false);
            state.Tick(1000, true, 1, 0, Settings);
            Assert.That(state.HoldSeconds, Is.EqualTo(35));
            state.Tick(1025, true, 1, 0, Settings);
            Assert.That(state.IsSecured, Is.True);
        }

        [Test]
        public void NewCombatDoesNotRetroactivelyCountAnUnobservedInterval()
        {
            var state = new StaticRegionCapture(1);
            state.Tick(0, true, 1, 0, Settings);
            state.RecordDamage(20);
            state.Tick(300, true, 1, 0, Settings);
            Assert.That(state.HoldSeconds, Is.Zero);
            Assert.That(state.Tick(360, true, 1, 0, Settings), Is.True);
        }

        [Test]
        public void ProgressSnapshotStaysUnchangedAfterTheStateAdvances()
        {
            var state = new StaticRegionCapture(1);
            var before = state.Snapshot;
            state.RecordDamage(20);
            state.Tick(0, true, 1, 0, Settings);
            state.Tick(60, true, 1, 0, Settings);
            Assert.That(before.TeamDamage, Is.Zero);
            Assert.That(before.HoldSeconds, Is.Zero);
            Assert.That(before.IsSecured, Is.False);
            Assert.That(state.Snapshot.IsSecured, Is.True);
        }

        [Test]
        public void EligibleMaskAndUnlockDoNotTurnUnlockIntoCapture()
        {
            var grid = new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 2, 2, 2, 2);
            var definition = new RegionGridDefinition(grid, new[] { 1, 1, 2, 2 }, new[] { true, false, true, false });
            var state = new RegionGridState(new MatchId("test"), Guid.NewGuid(), definition);
            state.UnlockThrough(1);
            Assert.That(state.Snapshot.IsRegionUnlocked(1), Is.True);
            Assert.That(state.Snapshot.IsRegionUnlocked(2), Is.False);
            Assert.That(state.Snapshot.CapturedCellCount, Is.Zero);
            Assert.Throws<ArgumentException>(() => state.Apply(new[] { 0, 1 }, RegionCaptureUpdateMode.Union));
            Assert.That(state.Snapshot.CapturedCellCount, Is.Zero);
            state.Apply(new[] { 0 }, RegionCaptureUpdateMode.Union);
            Assert.That(state.Snapshot.CapturedCellCount, Is.EqualTo(1));
            Assert.That(state.Snapshot.CaptureRatio, Is.EqualTo(.5));
            Assert.That(state.Snapshot.IsRegionUnlocked(1), Is.True);
        }
    }
}
