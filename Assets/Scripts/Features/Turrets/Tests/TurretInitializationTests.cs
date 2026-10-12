using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Content;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.Turrets.Tests
{
    public sealed class TurretInitializationTests
    {
        private readonly List<Object> _objects = new();
        private TurretDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            ResetRegistry();
            _definition = Track(ScriptableObject.CreateInstance<TurretDefinition>());
            SetField(_definition, "_id", "test-canon");
            SetField(_definition, "_maxHealth", 100);
            SetField(_definition, "_power", 20);
            SetField(_definition, "_damage", 40);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            ResetRegistry();
        }

        [Test]
        public void DefaultInputUsesDefinitionWithoutMutatingIt()
        {
            var data = TurretInitializationData.CreateDefault(_definition);
            Assert.That(data.CurrentHealth, Is.EqualTo(100));
            Assert.That(data.ShouldActivate, Is.False);
            Assert.That(data.IsDestroyed, Is.False);
            Assert.That(_definition.MaxHealth, Is.EqualTo(100));
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void InvalidHealthIsRejected(int health) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new TurretInitializationData(_definition, health));

        [Test]
        public void MissingAndInvalidUpgradeAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new TurretInitializationData(_definition, 60, upgradeLevel: 1));
            var upgrade = MakeUpgrade();
            SetField(upgrade, "_compatibleDefinitionIds", new List<string> { "another-definition" });
            Assert.Throws<ArgumentException>(() => new TurretInitializationData(_definition, 60, upgrade: upgrade, upgradeLevel: 1));
        }

        [Test]
        public void PartialStateRebuildsModifiersAndPreservesHealthThroughConfigure()
        {
            var state = new TurretRuntimeState();
            state.Initialize(new TurretInitializationData(_definition, 37, true, true, 3, MakeUpgrade(), 2));
            state.InitializeHealth(100);
            Assert.That(state.CurrentHealth, Is.EqualTo(37));
            Assert.That(state.IsLocked, Is.True);
            Assert.That(state.IsActivated, Is.False);
            Assert.That(state.InstanceId, Is.Zero);
            Assert.That(state.UpgradeLevel, Is.EqualTo(2));
            Assert.That(state.GetEffectiveDamage(40), Is.EqualTo(51));
            Assert.That(state.GetEffectivePower(20), Is.EqualTo(16));
        }

        [Test]
        public void DestroyedInitialStateIsNotRepairedOrActivated()
        {
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new TurretInitializationData(_definition, 0, shouldActivate: true), new FakePower());
            Assert.That(turret.ConfigureForTest(), Is.True);
            Assert.That(turret.CurrentHealth, Is.Zero);
            Assert.That(turret.IsDestroyed, Is.True);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(TurretActivationResult.Destroyed));
            Assert.That(turret.Restore(), Is.True); // Repair remains a separate explicit action.
            Assert.That(turret.CurrentHealth, Is.EqualTo(100));
            Assert.That(turret.IsActivated, Is.False);
        }

        [Test]
        public void LockedInitialStateRejectsPowerReservation()
        {
            var power = new FakePower();
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new TurretInitializationData(_definition, 60, true, true), power);
            turret.ConfigureForTest();
            Assert.That(turret.RequestActivation(true), Is.EqualTo(TurretActivationResult.Locked));
            Assert.That(power.ConsumeCalls, Is.Zero);
            turret.SetLocked(false);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(TurretActivationResult.Activated));
            Assert.That(power.ConsumeCalls, Is.EqualTo(1));
        }

        [Test]
        public void LockedTurretRejectsBothUpgradePathsWithoutChangingState()
        {
            var power = new FakePower();
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new TurretInitializationData(_definition, 100, isLocked: true), power);
            Assert.That(turret.ConfigureForTest(), Is.True);
            int originalPower = turret.EffectivePower;
            Assert.That(turret.ApplyUpgrade(MakeUpgrade()), Is.EqualTo(TurretUpgradeResult.Locked));
            Assert.That(turret.RequestLevelUpgrade(null, out TurretBase replacement),
                Is.EqualTo(TurretLevelUpgradeResult.Locked));
            Assert.That(replacement, Is.Null);
            Assert.That(turret.RuntimeState.UpgradeLevel, Is.Zero);
            Assert.That(turret.EffectivePower, Is.EqualTo(originalPower));
            Assert.That(power.ConsumeCalls, Is.Zero);
        }

        [Test]
        public void LockedDestroyedTurretCannotBeRestoredUntilUnlocked()
        {
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new TurretInitializationData(_definition, 0, isLocked: true), new FakePower());
            Assert.That(turret.ConfigureForTest(), Is.True);
            Assert.That(turret.Restore(), Is.False);
            Assert.That(turret.IsDestroyed, Is.True);
            Assert.That(turret.SetLocked(false), Is.True);
            Assert.That(turret.Restore(), Is.True);
        }

        [Test]
        public void CellDistanceZonesCoverTheGridWithoutStoredCellIds()
        {
            var grid = new TurretGridConfiguration(Vector2.zero, 8, 8, 8, 8);
            var zones = new TurretUnlockZones(grid, new Vector2(-3f, -3f), 4);
            Assert.That(zones.MaximumCellDistance, Is.EqualTo(12));
            Assert.That(zones.TryGetZone(new Vector2(-3f, -3f), out int nearest), Is.True);
            Assert.That(nearest, Is.EqualTo(1));
            Assert.That(zones.TryGetZone(new Vector2(3f, 3f), out int farthest), Is.True);
            Assert.That(farthest, Is.EqualTo(4));
            Assert.That(zones.TryGetZone(new Vector2(5f, 5f), out _), Is.False);
        }

        [Test]
        public void TurretGridDefaultsMatchTheConfiguredFullMapAndExposeCellDivisions()
        {
            var grid = new TurretGridConfiguration(
                Vector2.zero,
                TurretGridConfiguration.DefaultMapWidth,
                TurretGridConfiguration.DefaultMapHeight,
                TurretGridConfiguration.DefaultCellsX,
                TurretGridConfiguration.DefaultCellsY);

            Assert.That(grid.MapWidth, Is.EqualTo(275f));
            Assert.That(grid.MapHeight, Is.EqualTo(275f));
            Assert.That(grid.CellsX, Is.EqualTo(16));
            Assert.That(grid.CellsY, Is.EqualTo(16));
            Assert.That(grid.TryMapPoint(new Vector2(-137.5f, -137.5f), out int firstX, out int firstY), Is.True);
            Assert.That(firstX, Is.Zero);
            Assert.That(firstY, Is.Zero);
            Assert.That(grid.TryMapPoint(new Vector2(137.5f, 137.5f), out int lastX, out int lastY), Is.True);
            Assert.That(lastX, Is.EqualTo(15));
            Assert.That(lastY, Is.EqualTo(15));
            Assert.That(grid.TryMapPoint(new Vector2(137.51f, 0f), out _, out _), Is.False);
        }

        [Test]
        public void ControllerStartsWithOnlyFirstZoneUnlockedAndProgressesExplicitly()
        {
            var controller = Track(new GameObject("Unlock Controller")).AddComponent<TurretController>();
            var near = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            var far = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition), controller,
                new Vector3(6f, 6f), Quaternion.identity).gameObject).GetComponent<TurretBase>();

            Assert.That(controller.ConfigureUnlockZones(
                new TurretGridConfiguration(Vector2.zero, 16, 16, 16, 16), Vector2.zero, 4), Is.True);
            Assert.That(near.IsLocked, Is.False);
            Assert.That(far.IsLocked, Is.True);
            Assert.That(far.SetLocked(false), Is.False);
            Assert.That(far.ApplyDamage(25), Is.False);
            Assert.That(far.CurrentHealth, Is.EqualTo(100));
            var middle = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition), controller,
                new Vector3(6f, 0f), Quaternion.identity).gameObject).GetComponent<TurretBase>();
            Assert.That(middle.IsLocked, Is.True, "New turrets inherit the current zone policy.");
            Assert.That(controller.UnlockThroughZone(2), Is.True);
            Assert.That(middle.IsLocked, Is.False);
            Assert.That(far.IsLocked, Is.True);
            Assert.That(controller.UnlockThroughZone(4), Is.True);
            Assert.That(far.IsLocked, Is.False);
            Assert.That(far.IsActivated, Is.False);
            Assert.That(controller.GetSnapshot().ReservedPower, Is.Zero);
        }

        [UnityTest]
        public IEnumerator ZoneLockBlocksQueuedInitialActivation()
        {
            var controller = Track(new GameObject("Unlock Controller")).AddComponent<TurretController>();
            Assert.That(controller.ConfigureUnlockZones(BattlefieldGridConfiguration.Default, Vector2.zero, 4), Is.True);
            var far = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition, shouldActivate: true), controller,
                new Vector3(6f, 6f), Quaternion.identity).gameObject).GetComponent<TurretBase>();
            yield return null;
            yield return null;
            Assert.That(far.IsLocked, Is.True);
            Assert.That(far.IsActivated, Is.False);
            Assert.That(controller.GetSnapshot().ReservedPower, Is.Zero);
        }

        [Test]
        public void InitializationCannotOverwritePreparedOrLiveState()
        {
            var turret = MakeInactiveTurret();
            var data = new TurretInitializationData(_definition, 60);
            var power = new FakePower();
            turret.PrepareInitialization(data, power);
            Assert.Throws<InvalidOperationException>(() => turret.PrepareInitialization(data, power));
            turret.ConfigureForTest();
            turret.ApplyDamage(10);
            Assert.That(turret.ConfigureForTest(), Is.True);
            Assert.That(turret.CurrentHealth, Is.EqualTo(50));
            Assert.Throws<InvalidOperationException>(() => turret.PrepareInitialization(data, power));
        }

        [Test]
        public void FactoryRejectsMismatchedDefinitionBeforeCreatingAnything()
        {
            var other = Track(ScriptableObject.CreateInstance<TurretDefinition>());
            SetField(other, "_id", "other-definition");
            var prefab = MakeInactiveTurret();
            Assert.Throws<ArgumentException>(() => TurretFactory.Create(prefab,
                TurretInitializationData.CreateDefault(other), new FakePower(), Vector3.zero, Quaternion.identity));
            Assert.That(TurretInstanceRegistry.Count, Is.Zero);
        }

        [Test]
        public void ActivationAndShutdownDoNotDoubleReserveOrRelease()
        {
            var power = new FakePower();
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(TurretInitializationData.CreateDefault(_definition), power);
            turret.ConfigureForTest();
            turret.RequestActivation(true);
            turret.RequestActivation(true);
            Assert.That(power.ConsumeCalls, Is.EqualTo(1));
            turret.RequestActivation(false);
            turret.RequestActivation(false);
            Assert.That(power.ReleaseCalls, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator DefaultScenePlacementStillUsesSerializedActivationIntent()
        {
            var controller = Track(new GameObject("Test Controller")).AddComponent<TurretController>();
            var turret = MakeInactiveTurret();
            SetField(turret, "_turretController", controller, typeof(TurretBase));
            turret.RuntimeState.SetActivated(true);
            turret.gameObject.SetActive(true);
            Assert.That(turret.IsInitialized, Is.True);
            Assert.That(turret.CurrentHealth, Is.EqualTo(100));
            yield return null;
            yield return null;
            Assert.That(turret.IsActivated, Is.True);
            Assert.That(controller.GetSnapshot().ReservedPower, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator RealBudgetRejectsSecondTurretWhenPowerIsInsufficient()
        {
            var controller = Track(new GameObject("Test Controller")).AddComponent<TurretController>();
            controller.TrySetMaximumPower(30);
            var first = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition, shouldActivate: true), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            var second = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition, shouldActivate: true), controller,
                Vector3.one, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            yield return null;
            yield return null;
            Assert.That(first.IsActivated, Is.True);
            Assert.That(second.IsActivated, Is.False);
            Assert.That(controller.GetSnapshot().ReservedPower, Is.EqualTo(20));
            Assert.That(second.LastInitialActivationResult, Is.EqualTo(TurretActivationResult.InsufficientPower));
        }

        [UnityTest]
        public IEnumerator FactoryPublishesInitializedStateBeforeInitialActivation()
        {
            var power = new FakePower();
            var prefab = MakeInactiveTurret();
            TurretSnapshot registered = default;
            bool didRegister = false;
            TurretInstanceRegistry.Registered += turret =>
            {
                didRegister = TurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out registered);
            };
            var created = Track(TurretFactory.Create(prefab,
                new TurretInitializationData(_definition, 37, shouldActivate: true, upgrade: MakeUpgrade(), upgradeLevel: 2),
                power, Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            Assert.That(didRegister, Is.True);
            Assert.That(registered.CurrentHealth, Is.EqualTo(37));
            Assert.That(registered.IsActivated, Is.False);
            Assert.That(registered.EffectivePower, Is.EqualTo(16));
            yield return null;
            yield return null;
            Assert.That(created.IsActivated, Is.True);
            Assert.That(power.ConsumeCalls, Is.EqualTo(1));
            Assert.That(created.LastInitialActivationResult, Is.EqualTo(TurretActivationResult.Activated));
        }

        [UnityTest]
        public IEnumerator ManualOffCancelsPendingStartupActivation()
        {
            var power = new FakePower();
            var created = CreateWithActivationIntent(power);
            created.RequestActivation(false);
            yield return null;
            yield return null;
            Assert.That(created.IsActivated, Is.False);
            Assert.That(power.ConsumeCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator DisableCancelsPendingStartupActivation()
        {
            var power = new FakePower();
            var created = CreateWithActivationIntent(power);
            created.gameObject.SetActive(false);
            created.gameObject.SetActive(true);
            yield return null;
            yield return null;
            Assert.That(created.IsActivated, Is.False);
            Assert.That(power.ConsumeCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator InsufficientPowerKeepsInitialTurretOffAndExposesFailure()
        {
            var power = new FakePower { AllowConsume = false };
            var created = CreateWithActivationIntent(power);
            yield return null;
            yield return null;
            Assert.That(created.IsActivated, Is.False);
            Assert.That(created.LastInitialActivationResult, Is.EqualTo(TurretActivationResult.InsufficientPower));
        }

        [Test]
        public void LevelChangeTransferStillPreservesUpgradeAndHealthRatio()
        {
            var source = new TurretRuntimeState();
            source.Initialize(new TurretInitializationData(_definition, 37, true, false, 3, MakeUpgrade(), 2));
            source.AssignInstanceId(42);
            var target = new TurretRuntimeState();
            source.CopyForLevelChange(target, 100, 200);
            Assert.That(target.InstanceId, Is.EqualTo(42));
            Assert.That(target.CurrentHealth, Is.EqualTo(74));
            Assert.That(target.IsLocked, Is.True);
            Assert.That(target.UpgradeLevel, Is.EqualTo(2));
            Assert.That(target.IsActivated, Is.False);
        }

        [Test]
        public void AutoActivationWaitsForRecoveryAndRejectsRepeatedButton()
        {
            var controller = Track(new GameObject("Auto Controller")).AddComponent<TurretController>();
            controller.TrySetMaximumPower(20);
            var turret = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            Assert.That(turret.RequestActivation(true), Is.EqualTo(TurretActivationResult.Activated));
            Assert.That(controller.TryStartAutoActivation(), Is.True);
            Assert.That(turret.IsActivated, Is.False);
            Assert.That(controller.IsAutoActivationRunning, Is.True);
            Assert.That(controller.Budget.PendingRecovery, Is.EqualTo(20));
            Assert.That(controller.TryStartAutoActivation(), Is.False);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(TurretActivationResult.AutoActivationInProgress));
            Assert.That(controller.RequestActivation(turret.InstanceId, false), Is.EqualTo(TurretActivationResult.AutoActivationInProgress));
            Assert.That(turret.IsActivated, Is.False);
            controller.Budget.Tick(2.1f);
            typeof(TurretController).GetMethod("AdvanceAutoActivation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(turret.IsActivated, Is.True);
            Assert.That(controller.IsAutoActivationRunning, Is.False);
        }

        [Test]
        public void AutoActivationSkipsUnaffordableCandidateAndExcludesLockedDestroyed()
        {
            var controller = Track(new GameObject("Auto Controller")).AddComponent<TurretController>();
            controller.TrySetMaximumPower(10);
            var costly = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            var cheapDefinition = Track(ScriptableObject.CreateInstance<TurretDefinition>());
            SetField(cheapDefinition, "_id", "cheap");
            SetField(cheapDefinition, "_power", 5);
            var cheapPrefab = MakeInactiveTurret();
            SetField(cheapPrefab, "_definition", cheapDefinition, typeof(TurretBase));
            var cheap = Track(TurretFactory.Create(cheapPrefab,
                TurretInitializationData.CreateDefault(cheapDefinition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            var locked = Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition, isLocked: true), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            var destroyed = Track(TurretFactory.Create(MakeInactiveTurret(),
                new TurretInitializationData(_definition, 0), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
            controller.AutoActivationScoreProvider = (candidate, count) => candidate == costly ? 100f : 0f;
            Assert.That(controller.TryStartAutoActivation(), Is.True);
            Assert.That(controller.AutoActivationCandidates.Count, Is.EqualTo(2));
            Assert.That(controller.AutoActivationCandidates[0].InstanceId, Is.EqualTo(costly.InstanceId));
            Assert.That(costly.IsActivated, Is.False);
            Assert.That(cheap.IsActivated, Is.True);
            Assert.That(locked.IsActivated, Is.False);
            Assert.That(destroyed.IsActivated, Is.False);
            Assert.That(controller.IsAutoActivationRunning, Is.False);
        }

        private TurretBase CreateWithActivationIntent(FakePower power)
        {
            return Track(TurretFactory.Create(MakeInactiveTurret(),
                TurretInitializationData.CreateDefault(_definition, shouldActivate: true), power,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<TurretBase>();
        }

        private InitializationProbeTurret MakeInactiveTurret()
        {
            var go = Track(new GameObject("Initialization Test Turret"));
            go.SetActive(false);
            var turret = go.AddComponent<InitializationProbeTurret>();
            SetField(turret, "_definition", _definition, typeof(TurretBase));
            return turret;
        }

        private TurretUpgradeDefinition MakeUpgrade()
        {
            var upgrade = Track(ScriptableObject.CreateInstance<TurretUpgradeDefinition>());
            SetField(upgrade, "_id", "test-upgrade");
            SetField(upgrade, "_damageModifierRatio", 0.1f);
            SetField(upgrade, "_powerModifierRatio", -0.1f);
            return upgrade;
        }

        private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void ResetRegistry() => typeof(TurretInstanceRegistry)
            .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private static void SetField(object target, string name, object value, Type owner = null) =>
            (owner ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class FakePower : ITurretPowerSource
        {
            public bool AllowConsume = true;
            public int ConsumeCalls;
            public int ReleaseCalls;
            public int CurrentPower => 100;
            public int MaximumPower => 100;
            public event Action<int, int> PowerChanged { add { } remove { } }
            public bool TryConsumePower(int power) { ConsumeCalls++; return AllowConsume; }
            public bool TryChangeReservation(int previousPower, int newPower) => true;
            public void ReleasePower(int power) { ReleaseCalls++; }
        }
    }
}
