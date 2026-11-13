// 최근 점유 변화만 보관하고 초기 배치·복원·해금을 성장으로 오인하지 않는지 검사합니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class RegionCaptureHistoryTests
    {
        private static RegionRuntime Region() => new RegionRuntime(new MatchId("capture-history"),
            new RegionGridDefinition(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 100, 100, 10, 10)));
        private static RegionGridSnapshot Capture(RegionRuntime runtime, int cells)
        {
            runtime.TryApply(runtime.Snapshot, Enumerable.Range(0, cells), RegionCaptureUpdateMode.Replace);
            return runtime.Snapshot;
        }

        [Test]
        public void InitialCuAndUnchangedSamples_AreNotExpansionAndDoNotFillTheBuffer()
        {
            using var runtime = Region(); var history = new RegionCaptureHistory(); var epoch = Guid.NewGuid();
            history.Observe(Capture(runtime, 1), epoch, 0);
            for (int i = 1; i <= 1000; i++) history.Observe(runtime.Snapshot, epoch, i);
            Assert.That(history.RetainedSamples, Is.EqualTo(1));
            Assert.That(history.Current.CurrentRatio, Is.EqualTo(.01));
            Assert.That(history.Current.PositiveChanges, Is.Zero); Assert.That(history.Current.NetRatio, Is.Zero);
        }

        [Test]
        public void OneStaticRegionCompletion_IsOneEventRatherThanSustainedGrowth()
        {
            using var runtime = Region(); var history = new RegionCaptureHistory(); var epoch = Guid.NewGuid();
            history.Observe(Capture(runtime, 1), epoch, 0);
            var completed = history.Observe(Capture(runtime, 25), epoch, 60);
            history.Observe(runtime.Snapshot, epoch, 75);
            Assert.That(completed.PositiveChanges, Is.EqualTo(1));
            Assert.That(history.Current.PositiveChanges, Is.EqualTo(1)); Assert.That(history.Current.NetRatio, Is.EqualTo(.24));
        }

        [Test]
        public void MultipleChangesAndLoss_UseNetRatio_AndOldChangesExpire()
        {
            using var runtime = Region(); var history = new RegionCaptureHistory(); var epoch = Guid.NewGuid();
            history.Observe(Capture(runtime, 1), epoch, 0);
            history.Observe(Capture(runtime, 25), epoch, 40);
            var growth = history.Observe(Capture(runtime, 50), epoch, 80);
            Assert.That(growth.PositiveChanges, Is.EqualTo(2)); Assert.That(growth.NetRatio, Is.EqualTo(.49));
            var loss = history.Observe(Capture(runtime, 10), epoch, 90);
            Assert.That(loss.NetRatio, Is.EqualTo(.09));
            history.Observe(runtime.Snapshot, epoch, 211);
            Assert.That(history.Current.NetRatio, Is.Zero); Assert.That(history.Current.PositiveChanges, Is.Zero);
        }

        [Test]
        public void RestoreOrGridReset_StartsANewBaselineWithoutFalseGrowth()
        {
            using var runtime = Region(); var history = new RegionCaptureHistory(); var epoch = Guid.NewGuid();
            history.Observe(Capture(runtime, 1), epoch, 0); history.Observe(Capture(runtime, 40), epoch, 30);
            var restored = history.Observe(Capture(runtime, 70), Guid.NewGuid(), 10);
            Assert.That(restored.PositiveChanges, Is.Zero); Assert.That(restored.ObservedSeconds, Is.Zero);
            runtime.Reset(runtime.Snapshot.Definition, RegionPolicyMode.Static, Enumerable.Range(0, 80));
            var reset = history.Observe(runtime.Snapshot, epoch, 50);
            Assert.That(reset.CurrentRatio, Is.EqualTo(.8)); Assert.That(reset.NetRatio, Is.Zero);
        }

        [Test]
        public void BoundedSamplesAndSameTimestampCollapse_PreserveIndependentReadValues()
        {
            using var runtime = Region(); var history = new RegionCaptureHistory(capacity: 4); var epoch = Guid.NewGuid();
            var old = history.Observe(Capture(runtime, 1), epoch, 0);
            history.Observe(Capture(runtime, 10), epoch, 5); history.Observe(Capture(runtime, 20), epoch, 5);
            Assert.That(history.Current.PositiveChanges, Is.EqualTo(1));
            for (int i = 6; i <= 20; i++) history.Observe(Capture(runtime, i + 20), epoch, i);
            Assert.That(history.RetainedSamples, Is.EqualTo(4)); Assert.That(old.CurrentRatio, Is.EqualTo(.01));
            Assert.That(history.Current.CurrentRatio, Is.EqualTo(.4));
        }
    }
}
