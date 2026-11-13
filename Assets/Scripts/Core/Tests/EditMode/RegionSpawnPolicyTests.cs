// 일반 스폰의 Region 그룹 제외와 반경·여유 거리의 경계 조건을 확인합니다.
// 실제 생성/NGO 검사는 Scene 통합 단계에서 별도로 수행합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Tests
{
    public sealed class RegionSpawnPolicyTests
    {
        private static RegionGridSnapshot Snapshot(RegionPolicyMode mode, params int[] cells)
        {
            var geometry = new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 4, 1, 4, 1);
            var definition = new RegionGridDefinition(geometry, new[] { 1, 1, 2, 2 });
            return RegionGridSnapshot.FromCapturedCells(new MatchId("spawn-policy"), Guid.NewGuid(), 0, mode, definition, cells);
        }

        [Test]
        public void PartialInitialCapture_DoesNotExcludeTheWholeFirstRegion()
        {
            var policy = new RegionSpawnPolicy(Snapshot(RegionPolicyMode.Static, 0));
            Assert.That(policy.IsRegionCaptured(1), Is.False);
            Assert.That(policy.GetExclusionReason(new BattlefieldPoint(-.5, 0), 0), Is.Empty);
            Assert.That(policy.GetExclusionReason(new BattlefieldPoint(-1.5, 0), 0), Is.EqualTo("CapturedBoundary"));
        }

        [Test]
        public void SecuredRegion_ExcludesItsEntireGroupBeforeAllocation()
        {
            var policy = new RegionSpawnPolicy(Snapshot(RegionPolicyMode.Static, 0, 1));
            Assert.That(policy.IsRegionCaptured(1), Is.True);
            Assert.That(policy.IsRegionCaptured(2), Is.False);
            Assert.That(policy.GetExclusionReason(new BattlefieldPoint(-.5, 0), 0), Is.EqualTo("CapturedRegion"));
            Assert.That(policy.GetExclusionReason(new BattlefieldPoint(.5, 0), 0), Is.Empty);
        }

        [TestCase(.49, 0, false)]
        [TestCase(.5, 0, true)]
        [TestCase(.25, .25, true)]
        [TestCase(.25, .24, false)]
        public void NeighboringUncapturedRegion_UsesRadiusAndWorldMargin(double radius, double margin, bool blocked)
        {
            var policy = new RegionSpawnPolicy(Snapshot(RegionPolicyMode.Static, 0, 1), margin);
            Assert.That(policy.GetExclusionReason(new BattlefieldPoint(.5, 0), radius),
                Is.EqualTo(blocked ? "CapturedBoundary" : string.Empty));
        }

        [Test]
        public void CaptureLoss_ReenablesCandidatesFromTheNewSnapshot()
        {
            var secured = new RegionSpawnPolicy(Snapshot(RegionPolicyMode.Static, 0, 1));
            var lost = new RegionSpawnPolicy(Snapshot(RegionPolicyMode.Static, 0));
            var point = new BattlefieldPoint(-.5, 0);
            Assert.That(secured.GetExclusionReason(point, 0), Is.EqualTo("CapturedRegion"));
            Assert.That(lost.GetExclusionReason(point, 0), Is.Empty);
        }

        [Test]
        public void DynamicMode_UsesCapturedCellsWithoutStaticGroupOwnership()
        {
            var policy = new RegionSpawnPolicy(Snapshot(RegionPolicyMode.Dynamic, 0, 1));
            Assert.That(policy.IsRegionCaptured(1), Is.False);
            Assert.That(policy.GetExclusionReason(new BattlefieldPoint(-.5, 0), 0), Is.EqualTo("CapturedBoundary"));
        }

        [Test]
        public void FullyCapturedGrid_HasNoNormalSpawnCandidates()
        {
            var policy = new RegionSpawnPolicy(Snapshot(RegionPolicyMode.Static, 0, 1, 2, 3));
            Assert.That(policy.IsRegionCaptured(1) && policy.IsRegionCaptured(2), Is.True);
            Assert.That(policy.GetExclusionReason(new BattlefieldPoint(.5, 0), 0), Is.EqualTo("CapturedRegion"));
        }

        [Test]
        public void OutsideGrid_DoesNotBecomeAnAlternativeSpawnLocation()
        {
            var policy = new RegionSpawnPolicy(Snapshot(RegionPolicyMode.Static));
            Assert.That(policy.GetExclusionReason(new BattlefieldPoint(3, 0), 0), Is.EqualTo("OutsideGrid"));
        }

        [Test]
        public void InvalidDistances_AreRejectedBeforePlanning()
        {
            var snapshot = Snapshot(RegionPolicyMode.Static);
            Assert.Throws<ArgumentOutOfRangeException>(() => new RegionSpawnPolicy(snapshot, -1));
            var policy = new RegionSpawnPolicy(snapshot);
            Assert.Throws<ArgumentOutOfRangeException>(() => policy.GetExclusionReason(new BattlefieldPoint(0, 0), double.NaN));
        }
    }
}
