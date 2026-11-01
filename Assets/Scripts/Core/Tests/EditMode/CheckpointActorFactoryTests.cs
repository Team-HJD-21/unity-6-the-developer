// actor 복원 Factory의 사전 ID 검증과 소유 Scene·배치 보존을 확인합니다.
using System;
using NUnit.Framework;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Tests.EditMode
{
    public sealed class CheckpointActorFactoryTests
    {
        [Test]
        public void ConflictingAuthoredIdentityRejectsBeforeCreatingNativeActors()
        {
            var template = new GameObject("checkpoint template");
            try
            {
                var identity = template.AddComponent<SceneEntityIdentity>(); identity.Initialize(new EntityId("authored"));
                Assert.Throws<ArgumentException>(() => CheckpointActorFactory.ValidatePrefab(template, new EntityId("different")));
                Assert.That(identity.EntityId.Value, Is.EqualTo("authored"));
                Assert.Throws<ArgumentException>(() => CheckpointActorFactory.ValidatePrefab(template, default));
            }
            finally { Object.DestroyImmediate(template); }
        }
        [Test]
        public void RestoreUsesOwnerSceneAndSavedPose()
        {
            var owner = SceneManager.GetActiveScene(); var template = new GameObject("checkpoint template");
            GameObject restored = null;
            try
            {
                var pose = new CheckpointTransformState(new Vector3(3, 7, 2), Quaternion.Euler(0, 0, 35), new Vector3(2, 3, 1));
                restored = CheckpointActorFactory.Instantiate(template, owner, pose, new EntityId("saved"));
                Assert.That(restored.scene, Is.EqualTo(owner));
                Assert.That(restored.transform.parent, Is.Null);
                Assert.That(restored.GetComponent<SceneEntityIdentity>().EntityId.Value, Is.EqualTo("saved"));
                Assert.That(restored.transform.position, Is.EqualTo(pose.Position)); Assert.That(restored.transform.localScale, Is.EqualTo(pose.LocalScale));
                Assert.That(Quaternion.Angle(restored.transform.rotation, pose.Rotation), Is.LessThan(.01f));
            }
            finally
            {
                if (restored != null) Object.DestroyImmediate(restored);
                Object.DestroyImmediate(template);
            }
        }
    }
}
