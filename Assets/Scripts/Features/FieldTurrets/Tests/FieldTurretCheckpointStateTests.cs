// 실제 터렛 모델에서 수리 소수점·강화 정의·전력 반환 기한과 예약 소유권 복원을 확인합니다.
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Content;
using UnityEngine;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.FieldTurrets.Tests
{
    public sealed class FieldTurretCheckpointStateTests
    {
        private readonly List<Object> _objects = new();
        private FieldTurretDefinition _definition;
        [SetUp]
        public void SetUp()
        {
            _definition = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
            Set(_definition, "_id", "checkpoint-canon"); Set(_definition, "_maxHealth", 100);
            Set(_definition, "_power", 20); Set(_definition, "_damage", 40);
        }
        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
        }

        [Test]
        public void RecreatedRuntimeKeepsRepairProgressAndUpgradeWithoutCopyingLocalIdentity()
        {
            var upgrade = Track(ScriptableObject.CreateInstance<FieldTurretUpgradeDefinition>());
            Set(upgrade, "_id", "damage-for-power"); Set(upgrade, "_damageModifierRatio", .1f);
            Set(upgrade, "_powerModifierRatio", -.05f); Set(upgrade, "_rangeModifierRatio", .2f);
            var source = new FieldTurretRuntimeState();
            source.Initialize(new FieldTurretInitializationData(_definition, 40, damageBonus: 3, upgrade: upgrade, upgradeLevel: 3));
            source.AssignInstanceId(71); source.Repair(.625, 100);
            var saved = source.CaptureState(_definition);
            var restored = new FieldTurretRuntimeState(); restored.AssignInstanceId(92);
            restored.RestoreState(saved, _definition, upgrade);
            Assert.That(restored.InstanceId, Is.EqualTo(92), "Local registry IDs are not persistent IDs.");
            Assert.That(restored.CurrentHealth, Is.EqualTo(40));
            Assert.That(restored.GetEffectiveDamage(40), Is.EqualTo(source.GetEffectiveDamage(40)));
            Assert.That(restored.GetEffectivePower(20), Is.EqualTo(source.GetEffectivePower(20)));
            Assert.That(restored.Repair(.375, 100), Is.EqualTo(1));
            Assert.That(restored.CurrentHealth, Is.EqualTo(41));
            Assert.That(source.CurrentHealth, Is.EqualTo(40), "Captured state must not alias its owner.");
        }

        [Test]
        public void ChangedContentIsRejectedBeforeMutatingRuntime()
        {
            var source = new FieldTurretRuntimeState(); source.Initialize(new FieldTurretInitializationData(_definition, 35));
            var saved = source.CaptureState(_definition);
            source.ApplyDamage(10); Set(_definition, "_maxHealth", 101);
            Assert.Throws<ArgumentException>(() => source.RestoreState(saved, _definition, null));
            Assert.That(source.CurrentHealth, Is.EqualTo(25));
            Set(_definition, "_maxHealth", 100); Set(_definition, "_id", "different-definition");
            Assert.Throws<ArgumentException>(() => source.RestoreState(saved, _definition, null));
            Assert.That(source.CurrentHealth, Is.EqualTo(25));
        }

        [Test]
        public void CaptureRejectsTheMiddleOfDamageFinalization()
        {
            var state = new FieldTurretRuntimeState(); state.Initialize(new FieldTurretInitializationData(_definition, 1));
            state.ApplyDamage(1);
            Assert.Throws<ArgumentException>(() => state.CaptureState(_definition));
            state.MarkDestroyed();
            Assert.That(state.CaptureState(_definition).IsDestroyed, Is.True);
        }

        [Test]
        public void PowerRestorePreservesReservedBudgetAndFractionalReturnWithoutPrematureEvents()
        {
            var source = new FieldTurretPowerBudget(100);
            Assert.That(source.TryConsumePower(50), Is.True);
            source.ReleasePower(20); source.Tick(.0625f);
            var saved = source.CaptureState();
            var restored = new FieldTurretPowerBudget(999); int events = 0;
            restored.PowerChanged += (_, _) => events++;
            restored.RestoreState(saved);
            Assert.That(restored.MaximumPower, Is.EqualTo(100));
            Assert.That(restored.CurrentPower, Is.EqualTo(50));
            Assert.That(restored.PendingRecovery, Is.EqualTo(20));
            Assert.That(restored.ReservedPower, Is.EqualTo(30));
            Assert.That(events, Is.Zero, "Publish only after the owner's actor graph is consistent.");
            restored.PublishRestoredState(); Assert.That(events, Is.EqualTo(1));
            restored.Tick(.03125f); Assert.That(restored.CurrentPower, Is.EqualTo(50));
            restored.Tick(.015625f); Assert.That(restored.CurrentPower, Is.EqualTo(51));
            Assert.That(source.CurrentPower, Is.EqualTo(50));
        }

        [Test]
        public void RestoredActivationAdoptsOneExistingReservationAndReturnsItOnce()
        {
            var budget = new FieldTurretPowerBudget(100);
            var state = new FieldTurretRuntimeState(); state.Initialize(new FieldTurretInitializationData(_definition, 60));
            var activation = new FieldTurretActivationController(state, budget, 20);
            activation.RequestActivation(true);
            var savedActor = state.CaptureState(_definition); var savedBudget = budget.CaptureState();
            activation.RequestActivation(false); budget.Tick(1);
            state.RestoreState(savedActor, _definition, null); budget.RestoreState(savedBudget);
            activation.RestoreReservation(20);
            Assert.That(budget.CurrentPower, Is.EqualTo(80), "Restore must not consume a second reservation.");
            Assert.That(activation.ReservedPower, Is.EqualTo(20));
            Assert.Throws<InvalidOperationException>(() => activation.RestoreReservation(20));
            activation.RequestActivation(false); activation.RequestActivation(false);
            Assert.That(budget.PendingRecovery, Is.EqualTo(20));
            Assert.That(budget.ReservedPower, Is.Zero);
            budget.Tick(2); Assert.That(budget.CurrentPower, Is.EqualTo(100));
        }

        [Test]
        public void MalformedBudgetAndUninitializedRuntimeCannotBecomeSavedState()
        {
            Assert.Throws<ArgumentException>(() => new FieldTurretPowerBudget.State(int.MaxValue, int.MaxValue, 1, 0));
            Assert.Throws<ArgumentException>(() => new FieldTurretPowerBudget.State(10, 5, 0, .05));
            Assert.Throws<ArgumentException>(() => new FieldTurretPowerBudget.State(10, 5, 5, double.NaN));
            Assert.Throws<InvalidOperationException>(() => new FieldTurretRuntimeState().CaptureState(_definition));
        }

        private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void Set(object target, string name, object value) =>
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    }
}
