// 로컬 시점 lease·삭제/교체·출력과 리스너의 단일 선택을 검사합니다.
// Editor 검사는 컴포넌트 수명 진입점을 명시적으로 호출하고 실제 프레임 검사는 별도로 둡니다.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CameraManagerTests
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private readonly List<CameraManager> _managers = new List<CameraManager>();
        private MatchCameraFollow _follow;
        private Transform _player;

        [SetUp]
        public void SetUp()
        {
            _player = Target("Player", new Vector3(1, 2, 0));
            _follow = Camera("Player output");
            _follow.Bind(_player);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _managers.Count - 1; index >= 0; index--)
                if (_managers[index] != null) Invoke(_managers[index], "OnDisable");
            foreach (GameObject item in _objects) if (item != null) Object.DestroyImmediate(item);
            _managers.Clear(); _objects.Clear();
        }

        [Test]
        public void ImmediateViewRestoresSameCameraDepthAndPlayerFraming()
        {
            var manager = _follow.Manager;
            Camera output = manager.OutputCamera;
            var target = Target("Cutscene", new Vector3(20, 30, 0));
            target.rotation = Quaternion.Euler(0, 0, 90);
            using (manager.PushView(target, 0, 12))
            {
                Assert.That(manager.OutputCamera, Is.SameAs(output));
                Assert.That(output.transform.position, Is.EqualTo(new Vector3(20, 30, -10)));
                Assert.That(output.orthographicSize, Is.EqualTo(12));
                Assert.That(Quaternion.Angle(output.transform.rotation, target.rotation), Is.LessThan(.001));
            }
            Assert.That(manager.ActiveTarget, Is.SameAs(_player));
            Assert.That(output.transform.position, Is.EqualTo(new Vector3(1, 2, -10)));
            Assert.That(output.orthographicSize, Is.EqualTo(18));
            Assert.That(output.transform.rotation, Is.EqualTo(Quaternion.identity));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void NestedAndOutOfOrderLeasesRestoreOnlyLiveSelections(bool newestFirst)
        {
            var manager = _follow.Manager;
            var firstTarget = Target("First", Vector3.one);
            var secondTarget = Target("Second", Vector3.one * 2);
            IDisposable first = manager.PushView(firstTarget, 0);
            IDisposable second = manager.PushView(secondTarget, 0);
            if (newestFirst)
            {
                second.Dispose(); Assert.That(manager.ActiveTarget, Is.SameAs(firstTarget)); first.Dispose();
            }
            else
            {
                first.Dispose(); Assert.That(manager.ActiveTarget, Is.SameAs(secondTarget)); second.Dispose();
            }
            second.Dispose(); first.Dispose();
            Assert.That(manager.ActiveTarget, Is.SameAs(_player));
        }

        [Test]
        public void OldLeaseCannotChangeReplacementActorOrItsNewView()
        {
            var manager = _follow.Manager;
            IDisposable old = manager.PushView(Target("Old", Vector3.one), 0);
            var replacement = Target("Next player", Vector3.right * 10);
            _follow.Bind(replacement);
            using (manager.PushView(Target("New view", Vector3.up * 10), 0))
            {
                Transform selected = manager.ActiveTarget;
                old.Dispose();
                Assert.That(manager.ActiveTarget, Is.SameAs(selected));
            }
            Assert.That(manager.ActiveTarget, Is.SameAs(replacement));
        }

        [Test]
        public void DestroyedTemporaryTargetReturnsToPlayerWithoutRequiringLeaseDisposal()
        {
            var manager = _follow.Manager;
            Transform target = Target("Destroyed", Vector3.one);
            using var lease = manager.PushView(target, 0, 8);
            Object.DestroyImmediate(target.gameObject);
            Invoke(manager, "LateUpdate");
            Assert.That(manager.HasOverride, Is.False);
            Assert.That(manager.OutputCamera.orthographicSize, Is.EqualTo(18));
            Assert.That(manager.ActiveTarget, Is.SameAs(_player));
        }

        [Test]
        public void OwnedOutputsSelectOneListenerAndRestorePreviousOnRemoval()
        {
            var first = _follow.Manager;
            var other = Camera("Other Scene output");
            other.Bind(Target("Other Player", Vector3.right));
            Assert.That(CameraManager.Current, Is.SameAs(other.Manager));
            Assert.That(first.OutputCamera.enabled, Is.False);
            Assert.That(first.GetComponent<AudioListener>().enabled, Is.False);
            Assert.That(other.GetComponent<AudioListener>().enabled, Is.True);
            Invoke(other.Manager, "OnDisable");
            Assert.That(CameraManager.Current, Is.SameAs(first));
            Assert.That(first.OutputCamera.enabled, Is.True);
            Assert.That(first.GetComponent<AudioListener>().enabled, Is.True);
            Assert.That(other.GetComponent<AudioListener>().enabled, Is.False);
        }

        [Test]
        public void InvalidRequestsDoNotCreateAnOverride()
        {
            Transform target = Target("View", Vector3.zero);
            Assert.Throws<ArgumentException>(() => _follow.Manager.PushView(null));
            Assert.Throws<ArgumentOutOfRangeException>(() => _follow.Manager.PushView(target, float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => _follow.Manager.PushView(target, 0, 0));
            Assert.That(_follow.Manager.HasOverride, Is.False);
        }

        [Test]
        public void RebindingAnExistingOutputPromotesItAndPreservesFallbackOrder()
        {
            var other = Camera("Another output");
            other.Bind(Target("Another Player", Vector3.up));
            _follow.Bind(_player);
            Assert.That(CameraManager.Current, Is.SameAs(_follow.Manager));
            Assert.That(other.Manager.OutputCamera.enabled, Is.False);
            Invoke(_follow.Manager, "OnDisable");
            Assert.That(CameraManager.Current, Is.SameAs(other.Manager));
            Assert.That(other.Manager.OutputCamera.enabled, Is.True);
        }

        [Test]
        public void RebindAfterStaticResetRegistersAgainWithoutAnExtraOwnershipFlag()
        {
            typeof(CameraManager).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
            Assert.That(CameraManager.Current, Is.Null);
            _follow.Bind(_player);
            Assert.That(CameraManager.Current, Is.SameAs(_follow.Manager));
            Assert.That(_follow.Manager.OutputCamera.enabled, Is.True);
        }

        [Test]
        public void LostPlayerClearsTemporaryViews()
        {
            var manager = _follow.Manager;
            using var lease = manager.PushView(Target("View", Vector3.one), 0, 8);
            Object.DestroyImmediate(_player.gameObject);
            Invoke(manager, "LateUpdate");
            Assert.That(manager.HasOverride, Is.False);
            Assert.That(manager.OutputCamera.orthographicSize, Is.EqualTo(18));
        }

        [Test]
        public void FailedViewObserverDoesNotStrandAnUnreturnedLease()
        {
            var manager = _follow.Manager;
            void Fail(Transform _) => throw new InvalidOperationException("camera observer test");
            manager.ViewChanged += Fail;
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: camera observer test");
            var lease = manager.PushView(Target("View", Vector3.one), 0);
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: camera observer test");
            lease.Dispose();
            manager.ViewChanged -= Fail;
            Assert.That(manager.HasOverride, Is.False);
            Assert.That(manager.ActiveTarget, Is.SameAs(_player));
        }

        private Transform Target(string name, Vector3 position)
        {
            var item = new GameObject(name); _objects.Add(item); item.transform.position = position; return item.transform;
        }

        private MatchCameraFollow Camera(string name)
        {
            var item = new GameObject(name); _objects.Add(item);
            item.transform.position = Vector3.back * 10;
            var camera = item.AddComponent<Camera>(); camera.orthographic = true; camera.orthographicSize = 18;
            item.AddComponent<AudioListener>();
            var follow = item.AddComponent<MatchCameraFollow>();
            if (follow.Manager == null) Invoke(follow, "Awake");
            if (follow.Manager.OutputCamera == null) Invoke(follow.Manager, "Awake");
            _managers.Add(follow.Manager);
            return follow;
        }

        private static void Invoke(object target, string method) => target.GetType()
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, null);
    }
}
