// 지역 Grid의 원본 상태·불변성·세대 격리와 알림 실패/종료를 검증합니다.
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class RegionGridRuntimeTests
    {
        private static RegionGridDefinition Definition() => new RegionGridDefinition(
            new BattlefieldGridConfiguration(new BattlefieldPoint(10, 20), 0, 8, 4, 4, 2));

        [Test]
        public void UnlockIsIndependentFromCaptureAndUsesRevisionValidation()
        {
            var definition = new RegionGridDefinition(Definition().Geometry, new[] { 1, 1, 1, 1, 2, 2, 2, 2 });
            using var runtime = new RegionRuntime(new MatchId("one"), definition);
            runtime.TryApply(runtime.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace);
            var before = runtime.Snapshot;
            Assert.That(runtime.TryUnlockThrough(before, 1), Is.EqualTo(RegionUpdateStatus.Applied));
            Assert.That(before.UnlockedRegionIds, Is.Empty);
            Assert.That(runtime.Snapshot.CapturedCellCount, Is.EqualTo(1));
            Assert.That(runtime.Snapshot.CapturedCells[1], Is.False);
            Assert.That(new RegionAccessPolicy(runtime).CanUseAt(new BattlefieldPoint(9, 19)), Is.True);
            Assert.That(runtime.TryUnlockThrough(before, 2), Is.EqualTo(RegionUpdateStatus.StaleRevision));
            runtime.TryApply(runtime.Snapshot, Array.Empty<int>(), RegionCaptureUpdateMode.Replace);
            Assert.That(runtime.Snapshot.IsRegionUnlocked(1), Is.True);
        }

        [Test]
        public void Definition_CopiesMembership_AndMapsCenteredNonSquareGrid()
        {
            int[] membership = { 1, 1, 2, 2, 1, 1, 2, 2 };
            var definition = new RegionGridDefinition(Definition().Geometry, membership);
            membership[0] = 9;
            Assert.That(definition.RegionIds[0], Is.EqualTo(1));
            Assert.That(definition.TryGetCellIndex(new BattlefieldPoint(6, 18), out int first), Is.True);
            Assert.That(first, Is.Zero);
            Assert.That(definition.TryGetCellIndex(new BattlefieldPoint(14, 22), out int last), Is.True);
            Assert.That(last, Is.EqualTo(7));
            Assert.That(definition.TryGetCellIndex(new BattlefieldPoint(14.1, 22), out int outside), Is.False);
            Assert.That(outside, Is.EqualTo(-1));
        }

        [Test]
        public void Definition_RejectsIncompleteNegativeAndOversizedMembership()
        {
            Assert.Throws<ArgumentException>(() => new RegionGridDefinition(Definition().Geometry, new[] { 1 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new RegionGridDefinition(Definition().Geometry, new[] { -1 }));
            Assert.Throws<ArgumentException>(() => new RegionGridDefinition(Definition().Geometry, new int[9]));
            Assert.Throws<ArgumentOutOfRangeException>(() => Definition().GetCellIndex(4, 0));
        }

        [Test]
        public void State_AppliesAtomically_AndPreservesOldSnapshot()
        {
            var state = new RegionGridState(new MatchId("one"), Guid.NewGuid(), Definition());
            RegionGridSnapshot old = state.Snapshot;
            Assert.That(old.Mode, Is.EqualTo(RegionPolicyMode.Static));
            Assert.That(state.Apply(new[] { 0, 0, 7 }, RegionCaptureUpdateMode.Replace), Is.True);
            Assert.That(state.Snapshot.CapturedCellCount, Is.EqualTo(2));
            Assert.That(old.CapturedCellCount, Is.Zero);
            Assert.That(old.CapturedCells[0], Is.False);
            Assert.That(state.Snapshot.Revision, Is.EqualTo(1));
            Assert.That(state.Apply(new[] { 7, 0 }, RegionCaptureUpdateMode.Replace), Is.False);
            RegionGridSnapshot committed = state.Snapshot;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Apply(new[] { 1, 8 }, RegionCaptureUpdateMode.Replace));
            Assert.That(state.Snapshot, Is.SameAs(committed));
            Assert.Throws<NotSupportedException>(() => ((IList<bool>)committed.CapturedCells)[0] = false);
        }

        [Test]
        public void State_UnionAndReplacementHaveExplicitDifferentSemantics()
        {
            var state = new RegionGridState(new MatchId("one"), Guid.NewGuid(), Definition(), RegionPolicyMode.Dynamic);
            state.Apply(new[] { 0 }, RegionCaptureUpdateMode.Replace);
            state.Apply(new[] { 1 }, RegionCaptureUpdateMode.Union);
            Assert.That(state.Snapshot.CapturedCellCount, Is.EqualTo(2));
            state.Apply(new[] { 1 }, RegionCaptureUpdateMode.Replace);
            Assert.That(state.Snapshot.CapturedCells[0], Is.False);
            state.Apply(Array.Empty<int>(), RegionCaptureUpdateMode.Replace);
            Assert.That(state.Snapshot.CapturedCellCount, Is.Zero);
        }

        [Test]
        public void Runtime_RejectsOtherMatchOtherGridAndOldRevision()
        {
            using var runtime = new RegionRuntime(new MatchId("one"), Definition());
            using var otherMatch = new RegionRuntime(new MatchId("two"), Definition());
            using var otherGrid = new RegionRuntime(new MatchId("one"), Definition());
            RegionGridSnapshot basis = runtime.Snapshot;
            Assert.That(runtime.TryApply(otherMatch.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace),
                Is.EqualTo(RegionUpdateStatus.DifferentMatch));
            Assert.That(runtime.TryApply(otherGrid.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace),
                Is.EqualTo(RegionUpdateStatus.DifferentGrid));
            Assert.That(runtime.TryApply(basis, new[] { 0 }, RegionCaptureUpdateMode.Replace),
                Is.EqualTo(RegionUpdateStatus.Applied));
            Assert.That(runtime.TryApply(basis, new[] { 1 }, RegionCaptureUpdateMode.Replace),
                Is.EqualTo(RegionUpdateStatus.StaleRevision));
            Assert.That(runtime.Snapshot.CapturedCells[0], Is.True);
        }

        [Test]
        public void Runtime_ResetIsExplicitAndInvalidatesOldGeneration()
        {
            using var runtime = new RegionRuntime(new MatchId("one"), Definition());
            runtime.TryApply(runtime.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace);
            RegionGridSnapshot old = runtime.Snapshot;
            int notifications = 0;
            runtime.Changed += _ => notifications++;
            runtime.Reset(Definition(), RegionPolicyMode.Dynamic);
            Assert.That(runtime.Snapshot.Mode, Is.EqualTo(RegionPolicyMode.Dynamic));
            Assert.That(runtime.Snapshot.CapturedCellCount, Is.Zero);
            Assert.That(runtime.Snapshot.GridId, Is.Not.EqualTo(old.GridId));
            Assert.That(runtime.TryApply(old, new[] { 0 }, RegionCaptureUpdateMode.Replace),
                Is.EqualTo(RegionUpdateStatus.DifferentGrid));
            Assert.That(notifications, Is.EqualTo(1));
            RegionGridSnapshot beforeFailure = runtime.Snapshot;
            Assert.Throws<ArgumentNullException>(() => runtime.Reset(null, RegionPolicyMode.Static));
            Assert.That(runtime.Snapshot, Is.SameAs(beforeFailure));
        }

        [Test]
        public void Runtime_NoOpDoesNotNotify_AndDisposalIsIdempotent()
        {
            var runtime = new RegionRuntime(new MatchId("one"), Definition());
            int notifications = 0;
            runtime.Changed += _ => notifications++;
            Assert.That(runtime.TryApply(runtime.Snapshot, Array.Empty<int>(), RegionCaptureUpdateMode.Replace),
                Is.EqualTo(RegionUpdateStatus.Unchanged));
            Assert.That(notifications, Is.Zero);
            runtime.Dispose();
            runtime.Dispose();
            Assert.Throws<ObjectDisposedException>(() => { _ = runtime.Snapshot; });
        }

        [Test]
        public void Runtime_ObserverFailureDoesNotRollbackOrSkipOtherObservers()
        {
            using var runtime = new RegionRuntime(new MatchId("one"), Definition());
            int observed = 0;
            runtime.Changed += _ => throw new InvalidOperationException("observer");
            runtime.Changed += snapshot => observed = snapshot.CapturedCellCount;
            Assert.Throws<AggregateException>(() => runtime.TryApply(runtime.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace));
            Assert.That(observed, Is.EqualTo(1));
            Assert.That(runtime.Snapshot.Revision, Is.EqualTo(1));
        }

        [Test]
        public void Runtime_DisposeInsideObserverStillCompletesNotification()
        {
            var runtime = new RegionRuntime(new MatchId("one"), Definition());
            int observed = 0;
            runtime.Changed += _ => runtime.Dispose();
            runtime.Changed += _ => observed++;
            Assert.That(runtime.TryApply(runtime.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace),
                Is.EqualTo(RegionUpdateStatus.Applied));
            Assert.That(observed, Is.EqualTo(1));
            Assert.Throws<ObjectDisposedException>(() => { _ = runtime.Snapshot; });
        }

        [Test]
        public void Runtime_ReentrantMutationIsRejectedAfterFirstCommit()
        {
            using var runtime = new RegionRuntime(new MatchId("one"), Definition());
            runtime.Changed += snapshot => runtime.TryApply(snapshot, new[] { 1 }, RegionCaptureUpdateMode.Replace);
            Assert.Throws<AggregateException>(() => runtime.TryApply(runtime.Snapshot, new[] { 0 }, RegionCaptureUpdateMode.Replace));
            Assert.That(runtime.Snapshot.CapturedCells[0], Is.True);
            Assert.That(runtime.Snapshot.CapturedCells[1], Is.False);
        }
    }
}
