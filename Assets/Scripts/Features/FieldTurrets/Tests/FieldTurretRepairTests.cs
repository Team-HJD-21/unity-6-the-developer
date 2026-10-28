using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.FieldTurrets.Tests
{
    public sealed class FieldTurretRepairTests
    {
        private readonly List<Object> _objects = new();
        private FieldTurretDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            ResetRegistry();
            _definition = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
            SetField(_definition, "_id", "repair-test");
            SetField(_definition, "_maxHealth", 100);
            SetField(_definition, "_power", 20);
        }

        [TearDown]
        public void TearDown()
        {
            for (int index = _objects.Count - 1; index >= 0; index--)
                if (_objects[index] != null) Object.DestroyImmediate(_objects[index]);
            _objects.Clear();
            ResetRegistry();
        }

        [TestCase(30)]
        [TestCase(50)]
        [TestCase(60)]
        [TestCase(120)]
        public void OneSecondAtDifferentTickRatesRepairsTheSameHealth(int ticksPerSecond)
        {
            var turret = CreateTurret(40);
            for (int tick = 0; tick < ticksPerSecond; tick++)
                turret.Repair(10f, 1f / ticksPerSecond);
            Assert.That(turret.CurrentHealth, Is.EqualTo(50));
        }

        [UnityTest]
        public IEnumerator HoldingProgressAccumulatesButReleasingDoesNotHealOnItsOwn()
        {
            var turret = CreateTurret(40);
            Assert.That(turret.Repair(2f, 0.25f), Is.EqualTo(FieldTurretRepairResult.Accumulating));
            yield return new WaitForSeconds(0.05f);
            Assert.That(turret.CurrentHealth, Is.EqualTo(40));
            Assert.That(turret.Repair(2f, 0.25f), Is.EqualTo(FieldTurretRepairResult.Repaired));
            Assert.That(turret.CurrentHealth, Is.EqualTo(41));
        }

        [Test]
        public void FullHealthClampsHugeFiniteInputsWithoutBankingExcessRepair()
        {
            var turret = CreateTurret(95);
            Assert.That(turret.Repair(float.MaxValue, float.MaxValue), Is.EqualTo(FieldTurretRepairResult.Repaired));
            Assert.That(turret.CurrentHealth, Is.EqualTo(100));
            Assert.That(turret.CanRepair, Is.False);
            Assert.That(turret.Repair(10f, 1f), Is.EqualTo(FieldTurretRepairResult.FullHealth));
            turret.ApplyDamage(5);
            Assert.That(turret.Repair(1f, 0.5f), Is.EqualTo(FieldTurretRepairResult.Accumulating));
            Assert.That(turret.CurrentHealth, Is.EqualTo(95));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidRatesDoNotChangeHealthOrPoisonLaterRepair(float rate)
        {
            var turret = CreateTurret(50);
            Assert.That(turret.Repair(rate, 1f), Is.EqualTo(FieldTurretRepairResult.InvalidRepairRate));
            Assert.That(turret.CurrentHealth, Is.EqualTo(50));
            Assert.That(turret.Repair(1f, 1f), Is.EqualTo(FieldTurretRepairResult.Repaired));
            Assert.That(turret.CurrentHealth, Is.EqualTo(51));
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        [TestCase(float.NegativeInfinity)]
        public void InvalidDurationsDoNotChangeHealthOrPoisonLaterRepair(float duration)
        {
            var turret = CreateTurret(50);
            Assert.That(turret.Repair(10f, duration), Is.EqualTo(FieldTurretRepairResult.InvalidDeltaTime));
            Assert.That(turret.CurrentHealth, Is.EqualTo(50));
            Assert.That(turret.Repair(1f, 1f), Is.EqualTo(FieldTurretRepairResult.Repaired));
            Assert.That(turret.CurrentHealth, Is.EqualTo(51));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepairDuringCoolingPreservesPowerAndActivation(bool activated)
        {
            var power = new FakePower();
            var turret = CreateTurret(50, power);
            if (activated) turret.RequestActivation(true);
            turret.BeginCoolingForTest(10f);
            float remaining = turret.CoolingDownRemaining;
            Assert.That(turret.CanRepair, Is.True);
            Assert.That(turret.Repair(10f, 0.5f), Is.EqualTo(FieldTurretRepairResult.Repaired));
            Assert.That(turret.CurrentHealth, Is.EqualTo(55));
            Assert.That(turret.IsActivated, Is.EqualTo(activated));
            Assert.That(turret.IsCoolingDown, Is.True);
            Assert.That(turret.CoolingDownRemaining, Is.EqualTo(remaining).Within(0.001f));
            Assert.That(power.ConsumeCalls, Is.EqualTo(activated ? 1 : 0));
            Assert.That(power.ReleaseCalls, Is.Zero);
        }

        [Test]
        public void IntegerRepairReachingFullHealthDiscardsTimedRepairFraction()
        {
            var turret = CreateTurret(99);
            Assert.That(turret.Repair(1f, 0.8f), Is.EqualTo(FieldTurretRepairResult.Accumulating));
            Assert.That(turret.Repair(1), Is.True);
            Assert.That(turret.CurrentHealth, Is.EqualTo(100));

            turret.ApplyDamage(5);

            Assert.That(turret.Repair(1f, 0.3f), Is.EqualTo(FieldTurretRepairResult.Accumulating));
            Assert.That(turret.CurrentHealth, Is.EqualTo(95), "Full health must clear progress from either repair API.");
        }

        [Test]
        public void HealthEventsAndSnapshotsChangeOnlyWhenWholeHealthIsAdded()
        {
            var turret = CreateTurret(98);
            int healthEvents = 0, snapshotEvents = 0, restoredEvents = 0;
            FieldTurretInstanceRegistry.HealthChanged += (_, _, _) => healthEvents++;
            FieldTurretInstanceRegistry.SnapshotChanged += _ => snapshotEvents++;
            FieldTurretInstanceRegistry.Restored += _ => restoredEvents++;
            FieldTurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out var before);
            turret.Repair(1f, 0.5f);
            Assert.That(healthEvents, Is.Zero);
            Assert.That(snapshotEvents, Is.Zero);
            turret.Repair(1f, 0.5f);
            Assert.That(healthEvents, Is.EqualTo(1));
            Assert.That(snapshotEvents, Is.EqualTo(1));
            FieldTurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out var after);
            Assert.That(before.CurrentHealth, Is.EqualTo(98));
            Assert.That(after.CurrentHealth, Is.EqualTo(99));
            turret.Repair(10f, 1f);
            turret.Repair(10f, 1f);
            Assert.That(healthEvents, Is.EqualTo(2));
            Assert.That(snapshotEvents, Is.EqualTo(2));
            Assert.That(restoredEvents, Is.Zero, "Repair must not publish the destroyed-turret restore event.");
        }

        [Test]
        public void LockedRepairIsRejectedWithoutAccumulatingHiddenProgress()
        {
            var turret = CreateTurret(50, locked: true);
            Assert.That(turret.CanRepair, Is.False);
            Assert.That(turret.Repair(1f, 0.5f), Is.EqualTo(FieldTurretRepairResult.Locked));
            turret.SetLocked(false);
            Assert.That(turret.Repair(1f, 0.5f), Is.EqualTo(FieldTurretRepairResult.Accumulating));
            Assert.That(turret.CurrentHealth, Is.EqualTo(50));
        }

        [Test]
        public void DestroyedTurretRequiresRestoreAndLosesPreviousFractionalProgress()
        {
            var turret = CreateTurret(10);
            turret.Repair(1f, 0.8f);
            turret.ApplyDamage(10);
            Assert.That(turret.CanRepair, Is.False);
            Assert.That(turret.Repair(10f, 1f), Is.EqualTo(FieldTurretRepairResult.Destroyed));
            Assert.That(turret.IsDestroyed, Is.True);
            Assert.That(turret.Restore(), Is.True);
            turret.ApplyDamage(10);
            Assert.That(turret.Repair(1f, 0.3f), Is.EqualTo(FieldTurretRepairResult.Accumulating));
            Assert.That(turret.CurrentHealth, Is.EqualTo(90));
        }

        [Test]
        public void LethalDamageEventCannotRepairBeforeDestructionFinishes()
        {
            var turret = CreateTurret(10);
            FieldTurretRepairResult? result = null;
            FieldTurretInstanceRegistry.HealthChanged += (_, health, _) =>
            {
                if (health != 0) return;
                Assert.That(turret.CanRepair, Is.False);
                result = turret.Repair(100f, 1f);
            };

            turret.ApplyDamage(10);

            Assert.That(result, Is.EqualTo(FieldTurretRepairResult.Destroyed));
            Assert.That(turret.CurrentHealth, Is.Zero);
            Assert.That(turret.IsDestroyed, Is.True);
        }

        [Test]
        public void MissingInitializationOrInactiveComponentCannotBeRepaired()
        {
            var turret = MakeInactiveTurret();
            Assert.That(turret.Repair(10f, 1f), Is.EqualTo(FieldTurretRepairResult.NotInitialized));
            turret.PrepareInitialization(new FieldTurretInitializationData(_definition, 50), new FakePower());
            turret.ConfigureForTest();
            Assert.That(turret.Repair(10f, 1f), Is.EqualTo(FieldTurretRepairResult.Inactive));
            turret.gameObject.SetActive(true);
            turret.enabled = false;
            Assert.That(turret.Repair(10f, 1f), Is.EqualTo(FieldTurretRepairResult.Inactive));
            Assert.That(turret.CurrentHealth, Is.EqualTo(50));
        }

        [Test]
        public void AutoActivationRejectsRepairWithoutAdvancingItsFraction()
        {
            var controller = Track(new GameObject("Repair Auto Controller")).AddComponent<FieldTurretController>();
            controller.TrySetMaximumPower(20);
            var turret = CreateTurret(50, controller);
            turret.Repair(1f, 0.4f);
            turret.RequestActivation(true);
            Assert.That(controller.TryStartAutoActivation(), Is.True);
            Assert.That(controller.IsAutoActivationRunning, Is.True);
            Assert.That(turret.CanRepair, Is.False);
            Assert.That(turret.Repair(100f, 1f), Is.EqualTo(FieldTurretRepairResult.AutoActivationInProgress));
            controller.Budget.Tick(2.1f);
            typeof(FieldTurretController).GetMethod("AdvanceAutoActivation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(controller.IsAutoActivationRunning, Is.False);
            Assert.That(turret.Repair(1f, 0.3f), Is.EqualTo(FieldTurretRepairResult.Accumulating));
            Assert.That(turret.CurrentHealth, Is.EqualTo(50));
            Assert.That(turret.Repair(1f, 0.3f), Is.EqualTo(FieldTurretRepairResult.Repaired));
        }

        [Test]
        public void InitializationAndLevelTransferDoNotCarryFractionalRepairCredit()
        {
            var state = new FieldTurretRuntimeState();
            state.Initialize(new FieldTurretInitializationData(_definition, 50));
            state.Repair(0.8d, 100);
            state.Initialize(new FieldTurretInitializationData(_definition, 30));
            Assert.That(state.Repair(0.3d, 100), Is.Zero);
            var replacement = new FieldTurretRuntimeState();
            state.CopyForLevelChange(replacement, 100, 200);
            Assert.That(replacement.Repair(0.8d, 200), Is.Zero);
            Assert.That(replacement.CurrentHealth, Is.EqualTo(60));
        }

        private InitializationProbeFieldTurret CreateTurret(int health, IFieldTurretPowerSource power = null, bool locked = false)
        {
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new FieldTurretInitializationData(_definition, health, isLocked: locked), power ?? new FakePower());
            turret.gameObject.SetActive(true);
            return turret;
        }

        private InitializationProbeFieldTurret MakeInactiveTurret()
        {
            var gameObject = Track(new GameObject("Repair Test Turret"));
            gameObject.SetActive(false);
            var turret = gameObject.AddComponent<InitializationProbeFieldTurret>();
            SetField(turret, "_definition", _definition, typeof(FieldTurretBase));
            return turret;
        }

        private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void SetField(object target, string field, object value, Type owner = null) =>
            (owner ?? target.GetType()).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void ResetRegistry() => typeof(FieldTurretInstanceRegistry)
            .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);

        private sealed class FakePower : IFieldTurretPowerSource
        {
            public int ConsumeCalls, ReleaseCalls;
            public int CurrentPower => 100;
            public int MaximumPower => 100;
            public event Action<int, int> PowerChanged { add { } remove { } }
            public bool TryConsumePower(int power) { ConsumeCalls++; return true; }
            public bool TryChangeReservation(int previousPower, int newPower) => true;
            public void ReleasePower(int power) => ReleaseCalls++;
        }
    }
}
