// Level이 지정한 점유 제외 영역이 셀 분모에만 적용되고 Region 소속/기존 지형 마스크는 보존되는지 검사합니다.
// 선 접촉·유효하지 않은 제작값을 포함하며 실제 NavMesh·보스 콘텐츠 인수를 대신하지 않습니다.
using System;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using UnityEngine;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class RegionCaptureExclusionTests
    {
        private GameObject _object;
        private RegionSceneBinding _binding;
        private static readonly BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Instance;

        [SetUp]
        public void SetUp()
        {
            _object = new GameObject("Capture exclusion test");
            _binding = _object.AddComponent<RegionSceneBinding>();
        }

        [TearDown]
        public void TearDown() => UnityEngine.Object.DestroyImmediate(_object);

        private RegionGridDefinition Apply(Bounds[] areas, bool[] mask = null)
        {
            typeof(RegionSceneBinding).GetField("_captureExcludedAreas", Private).SetValue(_binding, areas);
            var definition = new RegionGridDefinition(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 20, 20, 2, 2),
                new[] { 1, 1, 2, 2 }, mask);
            return (RegionGridDefinition)typeof(RegionSceneBinding).GetMethod("WithCaptureExclusions", Private)
                .Invoke(_binding, new object[] { definition });
        }

        [Test]
        public void PositiveOverlapExcludesOnlyItsCellAndPreservesRegionIdsAndLandMask()
        {
            var result = Apply(new[] { new Bounds(new Vector3(-5, -5), new Vector3(10, 10, 1)) },
                new[] { true, true, false, true });
            Assert.That(result.CapturableCells, Is.EqualTo(new[] { false, true, false, true }));
            Assert.That(result.CapturableCellCount, Is.EqualTo(2));
            Assert.That(result.RegionIds, Is.EqualTo(new[] { 1, 1, 2, 2 }));
        }

        [Test]
        public void RectangleOutsideMapThatOnlyTouchesBoundaryDoesNotExcludeLand()
        {
            var result = Apply(new[] { new Bounds(new Vector3(15, 0), new Vector3(10, 20, 1)) });
            Assert.That(result.CapturableCellCount, Is.EqualTo(4));
        }

        [Test]
        public void EmptyOrNonFiniteAuthoringIsRejected()
        {
            Assert.Throws<TargetInvocationException>(() => Apply(new[] { new Bounds(Vector3.zero, Vector3.zero) }));
            Assert.Throws<TargetInvocationException>(() => Apply(new[] { new Bounds(new Vector3(float.NaN, 0), Vector3.one) }));
        }

        [Test]
        public void CannotRemoveTheEntireCaptureObjective()
        {
            Assert.Throws<TargetInvocationException>(() => Apply(new[] { new Bounds(Vector3.zero, new Vector3(40, 40, 1)) }));
        }
    }
}
