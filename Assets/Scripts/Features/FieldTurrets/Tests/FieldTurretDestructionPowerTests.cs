using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.FieldTurrets.Tests
{
    public sealed class FieldTurretDestructionPowerTests
    {
        private readonly List<Object> _objects = new();
        private FieldTurretController _controller;

        [SetUp]
        public void SetUp()
        {
            ResetRegistry();
            _controller = Track(new GameObject("Destruction power test controller")).AddComponent<FieldTurretController>();
            Assert.That(_controller.TrySetMaximumPower(100), Is.True);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
                if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
            _objects.Clear();
            ResetRegistry();
        }

        [TestCase(0f, 0, 40)]
        [TestCase(25f, 10, 30)]
        [TestCase(50f, 20, 20)]
        [TestCase(100f, 40, 0)]
        public void ActiveDestructionReturnsConfiguredShareAndKeepsRemainder(float percent, int returned, int retained)
        {
            Assert.That(_controller.TrySetDestructionPowerReturnPercent(percent), Is.True);
            var turret = CreateTurret();

            Assert.That(turret.ApplyDamage(100), Is.True);

            Assert.That(turret.IsDestroyed, Is.True);
            Assert.That(turret.IsActivated, Is.False);
            Assert.That(turret.ReservedPower, Is.EqualTo(retained));
            AssertBudget(60, returned, retained);
            _controller.Budget.Tick(10f);
            AssertBudget(60 + returned, 0, retained);
        }

        [Test]
        public void DefaultIsFiftyPercentAndFractionalPowerIsRoundedDown()
        {
            Assert.That(_controller.DestructionPowerReturnPercent, Is.EqualTo(50f));
            var turret = CreateTurret(41);
            turret.ApplyDamage(100);

            Assert.That(turret.ReservedPower, Is.EqualTo(21));
            AssertBudget(59, 20, 21);
        }

        [TestCase(-1f)]
        [TestCase(101f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidPercentageDoesNotChangeConfiguration(float percent)
        {
            Assert.That(_controller.TrySetDestructionPowerReturnPercent(percent), Is.False);
            Assert.That(_controller.DestructionPowerReturnPercent, Is.EqualTo(50f));
        }

        [Test]
        public void RepeatedDamageAndOnOffRequestsCannotReleaseRetainedPower()
        {
            var turret = CreateTurret();
            turret.ApplyDamage(100);
            Assert.That(turret.ApplyDamage(1), Is.False);
            Assert.That(turret.RequestActivation(false), Is.EqualTo(FieldTurretActivationResult.Destroyed));
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Destroyed));
            Assert.That(_controller.TrySetDestructionPowerReturnPercent(100f), Is.True);
            Assert.That(turret.ApplyDamage(1), Is.False);
            AssertBudget(60, 20, 20);
        }

        [Test]
        public void RestoreReleasesRemainderOnceAndNextActivationReservesNormally()
        {
            var turret = CreateTurret();
            turret.ApplyDamage(100);
            Assert.That(turret.Restore(), Is.True);
            Assert.That(turret.IsActivated, Is.False);
            Assert.That(turret.CurrentHealth, Is.EqualTo(100));
            Assert.That(turret.ReservedPower, Is.Zero);
            AssertBudget(60, 40, 0);
            Assert.That(turret.Restore(), Is.False);
            AssertBudget(60, 40, 0);

            _controller.Budget.Tick(10f);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
            AssertBudget(60, 0, 40);
            turret.ApplyDamage(100);
            AssertBudget(60, 20, 20);
        }

        [Test]
        public void OrdinaryOffReturnsAllPowerAndLaterDestructionDoesNotReturnTwice()
        {
            var turret = CreateTurret();
            turret.RequestActivation(false);
            AssertBudget(60, 40, 0);
            turret.ApplyDamage(100);
            Assert.That(turret.ReservedPower, Is.Zero);
            AssertBudget(60, 40, 0);
            turret.Restore();
            AssertBudget(60, 40, 0);
        }

        [Test]
        public void PartialDamageAndLockedTurretDoNotReleasePower()
        {
            var turret = CreateTurret();
            turret.ApplyDamage(10);
            AssertBudget(60, 0, 40);
            var locked = CreateTurret(20, false, true);
            Assert.That(locked.ApplyDamage(100), Is.False);
            Assert.That(locked.IsDestroyed, Is.False);
            AssertBudget(60, 0, 40);
        }

        [Test]
        public void DisabledDestroyedTurretKeepsPowerUntilActualRemoval()
        {
            var turret = CreateTurret();
            turret.ApplyDamage(100);
            turret.gameObject.SetActive(false);
            AssertBudget(60, 20, 20);
            turret.gameObject.SetActive(true);
            AssertBudget(60, 20, 20);
            Object.DestroyImmediate(turret.gameObject);
            AssertBudget(60, 40, 0);
        }

        [Test]
        public void DestructionDoesNotReleaseAnotherTurretsReservation()
        {
            var destroyed = CreateTurret();
            var other = CreateTurret(30);
            destroyed.ApplyDamage(100);
            Assert.That(other.IsActivated, Is.True);
            Assert.That(other.ReservedPower, Is.EqualTo(30));
            AssertBudget(30, 20, 50);
            destroyed.Restore();
            Assert.That(other.ReservedPower, Is.EqualTo(30));
            AssertBudget(30, 40, 30);
        }

        [Test]
        public void AutoActivationCannotSpendOrReleaseDestroyedReservation()
        {
            var destroyed = CreateTurret();
            var other = CreateTurret(30);
            destroyed.ApplyDamage(100);
            Assert.That(_controller.TryStartAutoActivation(), Is.True);
            Assert.That(destroyed.ReservedPower, Is.EqualTo(20));
            Assert.That(destroyed.IsDestroyed, Is.True);
            Assert.That(other.ReservedPower, Is.EqualTo(30));
            AssertBudget(0, 50, 50);
        }

        [Test]
        public void DeathCallbacksCannotSwitchOffOrRestoreBeforeDestructionCompletes()
        {
            var turret = CreateTurret();
            int destroyedEvents = 0;
            FieldTurretInstanceRegistry.HealthChanged += (target, health, _) =>
            {
                if (target != turret || health != 0) return;
                Assert.That(turret.RequestActivation(false), Is.EqualTo(FieldTurretActivationResult.Destroyed));
                Assert.That(turret.Restore(), Is.False);
                AssertBudget(60, 20, 20);
            };
            FieldTurretInstanceRegistry.Destroyed += _ => destroyedEvents++;

            turret.ApplyDamage(100);

            Assert.That(destroyedEvents, Is.EqualTo(1));
            Assert.That(turret.IsDestroyed, Is.True);
            AssertBudget(60, 20, 20);
        }

        private InitializationProbeFieldTurret CreateTurret(int power = 40, bool activate = true, bool locked = false)
        {
            var definition = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
            SetField(definition, "_id", $"destruction-power-{_objects.Count}");
            SetField(definition, "_maxHealth", 100);
            SetField(definition, "_power", power);
            var go = Track(new GameObject("Destruction power test turret"));
            go.SetActive(false);
            var turret = go.AddComponent<InitializationProbeFieldTurret>();
            SetField(turret, "_definition", definition, typeof(FieldTurretBase));
            turret.PrepareInitialization(new FieldTurretInitializationData(definition, 100, isLocked: locked), _controller);
            Assert.That(turret.ConfigureForTest(), Is.True);
            go.SetActive(true);
            if (activate) Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
            return turret;
        }

        private void AssertBudget(int available, int pending, int reserved)
        {
            var budget = _controller.GetSnapshot();
            Assert.That(budget.AvailablePower, Is.EqualTo(available));
            Assert.That(budget.PendingRecoveryPower, Is.EqualTo(pending));
            Assert.That(budget.ReservedPower, Is.EqualTo(reserved));
            Assert.That(budget.AvailablePower + budget.PendingRecoveryPower + budget.ReservedPower,
                Is.EqualTo(budget.MaximumPower));
        }

        private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void SetField(object target, string name, object value, Type owner = null) =>
            (owner ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void ResetRegistry() => typeof(FieldTurretInstanceRegistry)
            .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    }
}
