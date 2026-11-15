// 점유 확정과 해금·초기화의 의미를 구분하고 실제 원본 revision을 검사합니다.
// UI 애니메이션이나 게임 규칙을 다시 구현하지 않고 읽기 계약의 경계를 검증합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class RegionGridChangeTests
    {
        private static RegionGridDefinition Definition() => new RegionGridDefinition(
            new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 20, 10, 4, 1), new[] { 1, 1, 2, 2 });

        [Test]
        public void CaptureThenUnlock_PreservesTheSingleCompletionIdentity()
        {
            using var runtime = new RegionRuntime(new MatchId("change"), Definition());
            var before = runtime.Snapshot;
            Assert.That(runtime.TryApply(before, new[] { 0, 1 }, RegionCaptureUpdateMode.Replace,
                RegionChangeKind.StaticSecured, new[] { 1 }), Is.EqualTo(RegionUpdateStatus.Applied));
            var captured = runtime.Snapshot;
            runtime.TryUnlockThrough(captured, 2);
            Assert.That(runtime.Snapshot.Change, Is.SameAs(captured.Change));
            Assert.That(runtime.Snapshot.Revision, Is.GreaterThan(captured.Change.Revision));
            Assert.That(captured.Change.PreviousRevision, Is.EqualTo(before.Revision));
            Assert.That(captured.Change.SecuredRegionIds, Is.EqualTo(new[] { 1 }));
            Assert.That(before.CapturedCellCount, Is.Zero, "과거 Snapshot은 변하지 않아야 합니다.");
        }

        [Test]
        public void ResetAndInitialUnlock_AreNotNewCaptureSuccesses()
        {
            using var runtime = new RegionRuntime(new MatchId("reset"), Definition());
            runtime.TryUnlockThrough(runtime.Snapshot, 1);
            Assert.That(runtime.Snapshot.Change.Kind, Is.EqualTo(RegionChangeKind.Unlock));
            var oldGrid = runtime.Snapshot.GridId;
            runtime.Reset(Definition(), RegionPolicyMode.Static, new[] { 0, 1 }, new[] { 1 });
            Assert.That(runtime.Snapshot.GridId, Is.Not.EqualTo(oldGrid));
            Assert.That(runtime.Snapshot.Change.Kind, Is.EqualTo(RegionChangeKind.Baseline));
            Assert.That(runtime.Snapshot.Change.SecuredRegionIds, Is.Empty);
        }

        [Test]
        public void TinyCuRegion_CanReportRealCompletionWithoutChangingItsMask()
        {
            var definition = new RegionGridDefinition(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 1, 1, 1, 1), new[] { 1 });
            using var runtime = new RegionRuntime(new MatchId("tiny"), definition);
            runtime.TryApply(runtime.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace);
            long initial = runtime.Snapshot.Revision;
            runtime.TryApply(runtime.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace,
                RegionChangeKind.StaticSecured, new[] { 1 });
            Assert.That(runtime.Snapshot.Revision, Is.GreaterThan(initial));
            Assert.That(runtime.Snapshot.Change.Kind, Is.EqualTo(RegionChangeKind.StaticSecured));
        }

        [Test]
        public void IncompleteOrWrongModeSuccess_DoesNotMutateTheSource()
        {
            using var runtime = new RegionRuntime(new MatchId("invalid"), Definition());
            var before = runtime.Snapshot;
            Assert.Throws<ArgumentException>(() => runtime.TryApply(before, new[] { 0 }, RegionCaptureUpdateMode.Replace,
                RegionChangeKind.StaticSecured, new[] { 1 }));
            Assert.That(runtime.Snapshot, Is.SameAs(before));
            Assert.Throws<ArgumentException>(() => runtime.TryApply(before, new[] { 0 }, RegionCaptureUpdateMode.Replace,
                RegionChangeKind.DynamicCapture));
            Assert.That(runtime.Snapshot, Is.SameAs(before));
        }

        [Test]
        public void UnchangedDynamicInput_DoesNotRepeatTheTransition()
        {
            using var runtime = new RegionRuntime(new MatchId("dynamic"), Definition(), RegionPolicyMode.Dynamic);
            runtime.TryApply(runtime.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace, RegionChangeKind.DynamicCapture);
            var before = runtime.Snapshot;
            Assert.That(runtime.TryApply(before, new[] { 0 }, RegionCaptureUpdateMode.Replace, RegionChangeKind.DynamicCapture),
                Is.EqualTo(RegionUpdateStatus.Unchanged));
            Assert.That(runtime.Snapshot, Is.SameAs(before));
        }
    }
}
