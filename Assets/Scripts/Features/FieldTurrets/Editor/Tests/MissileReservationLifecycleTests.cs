using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.FieldTurrets;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

public sealed class MissileReservationLifecycleTests
{
    private readonly List<GameObject> _objects = new();
    private readonly List<IDisposable> _reservations = new();
    private Transform _target;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        _target = Track(new GameObject("Reserved target")).transform;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        for (int index = _objects.Count - 1; index >= 0; index--)
            if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
        _objects.Clear();
        foreach (IDisposable reservation in _reservations) reservation.Dispose();
        _reservations.Clear();
        yield return new ExitPlayMode();
    }

    [Test]
    public void FirstExplosionDoesNotReleaseSecondMissilesReservation()
    {
        var firstClaim = Acquire();
        var secondClaim = firstClaim.Retain();
        _reservations.Add(secondClaim);
        var first = MakeMissile(firstClaim);
        var second = MakeMissile(secondClaim);
        Invoke(first, "BeginExplosion");
        Assert.That(MissileTargetReservation.TryAcquire(_target, out _), Is.False);
        second.enabled = false;
        Acquire();
    }

    [TestCase(true)]
    [TestCase(false)]
    public void DisablingObjectOrComponentReleasesItsClaim(bool disableObject)
    {
        var missile = MakeMissile(Acquire());
        if (disableObject) missile.gameObject.SetActive(false);
        else missile.enabled = false;
        Acquire();
    }

    [Test]
    public void DestroyAfterDisableDoesNotReleaseAnotherMissilesNewClaim()
    {
        var missile = MakeMissile(Acquire());
        missile.enabled = false;
        var current = Acquire();
        Object.DestroyImmediate(missile.gameObject);
        Assert.That(current.IsValid, Is.True);
        Assert.That(MissileTargetReservation.TryAcquire(_target, out _), Is.False);
    }

    [Test]
    public void DirectDestructionReleasesItsClaim()
    {
        var missile = MakeMissile(Acquire());
        Object.DestroyImmediate(missile.gameObject);
        Acquire();
    }

    [Test]
    public void FailedInitializationLeavesReservationWithCaller()
    {
        var claim = Acquire();
        var missile = MakeUninitializedMissile();
        Assert.Throws<ArgumentOutOfRangeException>(() => missile.Initialize(claim, -1));
        Assert.That(claim.IsValid, Is.True);
        claim.Dispose();
        Acquire();
    }

    [Test]
    public void InactiveMissileCannotTakeOwnership()
    {
        var claim = Acquire();
        var missile = MakeUninitializedMissile();
        missile.gameObject.SetActive(false);
        Assert.Throws<InvalidOperationException>(() => missile.Initialize(claim, 10));
        Assert.That(claim.IsValid, Is.True);
    }

    [Test]
    public void ExplosionReleasesImmediatelyBeforeDelayedDamage()
    {
        var missile = MakeMissile(Acquire());
        Invoke(missile, "BeginExplosion");
        Assert.That(GetField<bool>(missile, "_hasAppliedExplosion"), Is.False);
        Acquire();
    }

    [UnityTest]
    public IEnumerator StoppingCoroutinesCannotCancelLifetimeExpiryOrReservationCleanup()
    {
        var missile = MakeMissile(Acquire());
        missile.StopAllCoroutines();
        SetField(missile, "_expiresAt", Time.timeAsDouble - 1);
        float deadline = Time.realtimeSinceStartup + 3f;
        while (!GetField<bool>(missile, "_isExploding") && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(GetField<bool>(missile, "_isExploding"), Is.True);
        var nextClaim = Acquire();
        while (missile != null && Time.realtimeSinceStartup < deadline)
            yield return null;
        Assert.That(missile == null, Is.True, "Expired missiles finish delayed damage and despawn.");
        Assert.That(nextClaim.IsValid, Is.True, "Delayed destruction cannot release a newer claim.");
    }

    [Test]
    public void InactiveTargetIsReleasedBeforeSearchingAgain()
    {
        var missile = MakeMissile(Acquire());
        _target.gameObject.SetActive(false);
        Invoke(missile, "FixedUpdate");
        _target.gameObject.SetActive(true);
        Acquire();
    }

    private FieldTurretMissile MakeMissile(MissileTargetReservation reservation)
    {
        var missile = MakeUninitializedMissile();
        missile.Initialize(reservation, 10);
        reservation.Dispose(); // The caller no longer owns the transferred handle.
        return missile;
    }

    private FieldTurretMissile MakeUninitializedMissile()
    {
        var gameObject = Track(new GameObject("Reservation test missile", typeof(Rigidbody2D), typeof(SpriteRenderer)));
        var missile = gameObject.AddComponent<FieldTurretMissile>();
        SetField(missile, "rb", gameObject.GetComponent<Rigidbody2D>());
        return missile;
    }

    private MissileTargetReservation Acquire()
    {
        Assert.That(MissileTargetReservation.TryAcquire(_target, out var reservation), Is.True);
        _reservations.Add(reservation);
        return reservation;
    }

    private GameObject Track(GameObject gameObject)
    {
        _objects.Add(gameObject);
        return gameObject;
    }

    private static void Invoke(FieldTurretMissile missile, string method) =>
        typeof(FieldTurretMissile).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(missile, null);

    private static void SetField(FieldTurretMissile missile, string field, object value) =>
        typeof(FieldTurretMissile).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(missile, value);

    private static T GetField<T>(FieldTurretMissile missile, string field) =>
        (T)typeof(FieldTurretMissile).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(missile);
}
