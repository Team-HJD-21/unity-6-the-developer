using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerPlacementPresentationTests
{
    [UnitySetUp] public IEnumerator Enter() { yield return new EnterPlayMode(); }
    [UnityTearDown] public IEnumerator Exit() { yield return new ExitPlayMode(); }

    [TestCase("PlayerTurret")]
    [TestCase("EnergyBarrier")]
    [TestCase("LureBeacon")]
    [TestCase("bomb")]
    [TestCase("PlayerPortal")]
    public void AuthoredDisplayMovesWithoutMovingCollisionAndDisableRestoresIt(string name)
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Player/{name}.prefab"));
        try
        {
            actor.transform.position = new Vector3(40000, 40000);
            if (actor.TryGetComponent<PlayerFieldDevice>(out var device))
            {
                var position = actor.transform.position;
                device.Initialize(name == "EnergyBarrier" ? PlayerFieldDeviceKind.Barrier : PlayerFieldDeviceKind.Beacon,
                    position, position + (name == "EnergyBarrier" ? Vector3.right * 4 : Vector3.zero), .3f, 100, 8, 3);
            }
            Physics2D.SyncTransforms();
            var colliders = actor.GetComponentsInChildren<Collider2D>();
            var bounds = colliders.Select(c => c.bounds).ToArray();
            Vector3 origin = actor.transform.position, scale = actor.transform.localScale;
            var originals = actor.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name != "Placement Display").ToArray();
            var sourceBounds = originals.Select(r => r.bounds).ToArray();
            var displays = actor.GetComponentsInChildren<SpriteRenderer>(true).Where(r => r.name == "Placement Display").ToArray();
            var presentation = actor.GetComponent<PlayerPlacementPresentation>();
            Assert.That(displays, Is.Not.Empty);
            Assert.That(presentation.IsPlaying, Is.False, "Instantiation/restoration alone must not replay placement.");
            Assert.That(displays.All(r => !r.enabled), Is.True);
            presentation.Play(); presentation.Tick(.12f);
            Assert.That(presentation.IsPlaying, Is.True);
            Assert.That(displays.Any(r => r.enabled && (r.transform.localPosition.sqrMagnitude > .0001f || r.transform.localScale != Vector3.one)), Is.True);
            Physics2D.SyncTransforms();
            Assert.That(actor.transform.position, Is.EqualTo(origin));
            Assert.That(actor.transform.localScale, Is.EqualTo(scale));
            for (int i = 0; i < colliders.Length; i++) Assert.That(colliders[i].bounds, Is.EqualTo(bounds[i]), "Placement must not shift a contact or damage collider.");
            for (int i = 0; i < originals.Length; i++) Assert.That(originals[i].bounds, Is.EqualTo(sourceBounds[i]));
            presentation.enabled = false;
            Assert.That(presentation.IsPlaying, Is.False);
            Assert.That(originals.All(r => !r.forceRenderingOff), Is.True);
            Assert.That(displays.All(r => !r.enabled), Is.True);
            presentation.enabled = true;
            Assert.That(presentation.IsPlaying, Is.False, "Reactivation must not replay installation.");
            presentation.Play(); presentation.Tick(1);
            Assert.That(presentation.IsPlaying, Is.False);
            Assert.That(displays.All(r => !r.enabled && r.transform.localPosition == Vector3.zero && r.transform.localScale == Vector3.one), Is.True);
            Assert.That(originals.All(r => !r.forceRenderingOff), Is.True);
        }
        finally { Object.DestroyImmediate(actor); }
    }

    [Test]
    public void MatchPauseFreezesInstallationAndResumeFinishesWithoutMovingThePortal()
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerPortal.prefab"));
        try
        {
            var clock = new Clock(); FieldSimulationActor.Attach(actor, clock, destroyOnCompletion: false);
            var presentation = actor.GetComponent<PlayerPlacementPresentation>();
            presentation.Play(); presentation.Tick(.1f);
            float progress = presentation.Progress;
            clock.Paused = true; presentation.Tick(2);
            Assert.That(presentation.Progress, Is.EqualTo(progress));
            Assert.That(presentation.IsPlaying, Is.True);
            clock.Paused = false; presentation.Tick(1);
            Assert.That(presentation.IsPlaying, Is.False);
            Assert.That(actor.transform.position, Is.EqualTo(Vector3.zero));
        }
        finally { Object.DestroyImmediate(actor); }
    }

    private sealed class Clock : IMatchTime
    {
        public bool Paused;
        private readonly Guid _epoch = Guid.NewGuid();
        public bool IsAvailable => true;
        public double ElapsedSeconds => 0;
        public MatchTimeSnapshot Snapshot => new(new MatchId("placement-presentation"), _epoch, 0, 0, 0, Paused ? 0 : 1, Paused, true);
    }
}
