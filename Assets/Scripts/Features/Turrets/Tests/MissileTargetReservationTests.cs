using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Turrets.Tests
{
    public sealed class MissileTargetReservationTests
    {
        private GameObject _target;
        private readonly List<IDisposable> _reservations = new();

        [SetUp]
        public void SetUp() => _target = new GameObject("Missile reservation target");

        [TearDown]
        public void TearDown()
        {
            foreach (IDisposable reservation in _reservations) reservation.Dispose();
            _reservations.Clear();
            if (_target != null) Object.DestroyImmediate(_target);
        }

        [Test]
        public void DifferentVolleyCannotReserveAnAlreadyClaimedTarget()
        {
            Acquire();
            Assert.That(MissileTargetReservation.TryAcquire(_target.transform, out _), Is.False);
        }

        [Test]
        public void SharedVolleyKeepsTargetReservedUntilLastMissileReleases()
        {
            var first = Acquire();
            var second = Track(first.Retain());
            first.Dispose();
            Assert.That(MissileTargetReservation.TryAcquire(_target.transform, out _), Is.False);
            second.Dispose();
            Acquire();
        }

        [Test]
        public void TransferPreventsPreviousOwnerFromReleasingTheMissilesClaim()
        {
            var turret = Acquire();
            var missile = Track(turret.Transfer());
            Assert.That(turret.IsValid, Is.False);
            turret.Dispose();
            Assert.That(missile.IsValid, Is.True);
            Assert.That(MissileTargetReservation.TryAcquire(_target.transform, out _), Is.False);
            missile.Dispose();
            Acquire();
        }

        [Test]
        public void RepeatedReleaseCannotEraseANewerReservation()
        {
            var old = Acquire();
            old.Dispose();
            var current = Acquire();
            old.Dispose();
            Assert.That(current.IsValid, Is.True);
            Assert.That(MissileTargetReservation.TryAcquire(_target.transform, out _), Is.False);
        }

        [Test]
        public void DestroyedTargetCanStillBeReleasedSafely()
        {
            var reservation = Acquire();
            Object.DestroyImmediate(_target);
            Assert.That(reservation.IsValid, Is.False);
            Assert.DoesNotThrow(reservation.Dispose);
        }

        [Test]
        public void InactiveTargetCannotBeReservedOrRetained()
        {
            var reservation = Acquire();
            _target.SetActive(false);
            Assert.That(reservation.IsValid, Is.False);
            Assert.Throws<InvalidOperationException>(() => reservation.Retain());
            Assert.That(MissileTargetReservation.TryAcquire(_target.transform, out _), Is.False);
        }

        [Test]
        public void PreviousPlaySessionCannotReleaseANewSessionsClaim()
        {
            var old = Acquire();
            typeof(MissileTargetReservation).GetMethod("ResetStatics",
                BindingFlags.NonPublic | BindingFlags.Static).Invoke(null, null);
            Assert.That(old.IsValid, Is.False);
            var current = Acquire();
            old.Dispose();
            Assert.That(current.IsValid, Is.True);
            Assert.That(MissileTargetReservation.TryAcquire(_target.transform, out _), Is.False);
        }

        private MissileTargetReservation Acquire()
        {
            Assert.That(MissileTargetReservation.TryAcquire(_target.transform, out var reservation), Is.True);
            return Track(reservation);
        }

        private MissileTargetReservation Track(MissileTargetReservation reservation)
        {
            _reservations.Add(reservation);
            return reservation;
        }
    }
}
