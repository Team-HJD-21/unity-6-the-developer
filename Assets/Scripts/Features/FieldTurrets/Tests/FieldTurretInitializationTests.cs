using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Content;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.FieldTurrets.Tests
{
    public sealed class FieldTurretInitializationTests
    {
        private readonly List<Object> _objects = new();
        private FieldTurretDefinition _definition;

        [SetUp]
        public void SetUp()
        {
            ResetRegistry();
            _definition = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
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
            var data = FieldTurretInitializationData.CreateDefault(_definition);
            Assert.That(data.CurrentHealth, Is.EqualTo(100));
            Assert.That(data.ShouldActivate, Is.False);
            Assert.That(data.IsDestroyed, Is.False);
            Assert.That(_definition.MaxHealth, Is.EqualTo(100));
        }

        [Test]
        public void PartialRepairCapsHealthAndPreservesActivationAndReservation()
        {
            var power = new FakePower();
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new FieldTurretInitializationData(_definition, 37), power);
            Assert.That(turret.ConfigureForTest(), Is.True);
            turret.gameObject.SetActive(true);
            FieldTurretInstanceRegistry.Register(turret);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
            int healthEvents = 0, snapshotEvents = 0;
            Action<FieldTurretBase, int, int> health = (target, current, max) => { if (target == turret) healthEvents++; };
            Action<int> snapshot = id => { if (id == turret.InstanceId) snapshotEvents++; };
            FieldTurretInstanceRegistry.HealthChanged += health;
            FieldTurretInstanceRegistry.SnapshotChanged += snapshot;
            try
            {
                Assert.That(turret.Repair(10), Is.True);
                Assert.That(turret.CurrentHealth, Is.EqualTo(47));
                Assert.That(turret.Repair(int.MaxValue), Is.True);
                Assert.That(turret.CurrentHealth, Is.EqualTo(100));
                Assert.That(turret.Repair(10), Is.False);
                Assert.That(turret.Repair(0), Is.False);
                Assert.That(turret.Repair(-1), Is.False);
                Assert.That(turret.IsActivated, Is.True);
                Assert.That(power.ConsumeCalls, Is.EqualTo(1));
                Assert.That(power.ReleaseCalls, Is.Zero);
                Assert.That(healthEvents, Is.EqualTo(2));
                Assert.That(snapshotEvents, Is.EqualTo(2));
                Assert.That(FieldTurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out var latest), Is.True);
                Assert.That(latest.CurrentHealth, Is.EqualTo(100));
            }
            finally
            {
                FieldTurretInstanceRegistry.HealthChanged -= health;
                FieldTurretInstanceRegistry.SnapshotChanged -= snapshot;
            }
        }

        [Test]
        public void RepairDoesNotReviveDestroyedOrUnlockLockedTurret()
        {
            var destroyed = new FieldTurretRuntimeState();
            destroyed.Initialize(new FieldTurretInitializationData(_definition, 0));
            Assert.That(destroyed.TryRepair(10, 100), Is.False);
            Assert.That(destroyed.CurrentHealth, Is.Zero);
            Assert.That(destroyed.IsDestroyed, Is.True);
            var dying = new FieldTurretRuntimeState();
            dying.Initialize(new FieldTurretInitializationData(_definition, 30));
            dying.ApplyDamage(30);
            Assert.That(dying.TryRepair(10, 100), Is.False, "Health callbacks must not revive a zero-health target before MarkDestroyed.");
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new FieldTurretInitializationData(_definition, 30, isLocked: true), new FakePower());
            turret.ConfigureForTest();
            turret.gameObject.SetActive(true);
            Assert.That(turret.Repair(10), Is.False);
            Assert.That(turret.CurrentHealth, Is.EqualTo(30));
            Assert.That(turret.IsLocked, Is.True);
        }

        [TestCase(-1)]
        [TestCase(101)]
        public void InvalidHealthIsRejected(int health) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new FieldTurretInitializationData(_definition, health));

        [Test]
        public void MissingAndInvalidUpgradeAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new FieldTurretInitializationData(_definition, 60, upgradeLevel: 1));
            var upgrade = MakeUpgrade();
            SetField(upgrade, "_compatibleDefinitionIds", new List<string> { "another-definition" });
            Assert.Throws<ArgumentException>(() => new FieldTurretInitializationData(_definition, 60, upgrade: upgrade, upgradeLevel: 1));
        }

        [Test]
        public void PartialStateRebuildsModifiersAndPreservesHealthThroughConfigure()
        {
            var state = new FieldTurretRuntimeState();
            state.Initialize(new FieldTurretInitializationData(_definition, 37, true, true, 3, MakeUpgrade(), 2));
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
            turret.PrepareInitialization(new FieldTurretInitializationData(_definition, 0, shouldActivate: true), new FakePower());
            Assert.That(turret.ConfigureForTest(), Is.True);
            Assert.That(turret.CurrentHealth, Is.Zero);
            Assert.That(turret.IsDestroyed, Is.True);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Destroyed));
            Assert.That(turret.Restore(), Is.True); // Repair remains a separate explicit action.
            Assert.That(turret.CurrentHealth, Is.EqualTo(100));
            Assert.That(turret.IsActivated, Is.False);
        }

        [Test]
        public void LockedInitialStateRejectsPowerReservation()
        {
            var power = new FakePower();
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new FieldTurretInitializationData(_definition, 60, true, true), power);
            turret.ConfigureForTest();
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Locked));
            Assert.That(power.ConsumeCalls, Is.Zero);
            turret.SetLocked(false);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
            Assert.That(power.ConsumeCalls, Is.EqualTo(1));
        }

        [Test]
        public void LockedTurretRejectsBothUpgradePathsWithoutChangingState()
        {
            var power = new FakePower();
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new FieldTurretInitializationData(_definition, 100, isLocked: true), power);
            Assert.That(turret.ConfigureForTest(), Is.True);
            int originalPower = turret.EffectivePower;
            Assert.That(turret.ApplyUpgrade(MakeUpgrade()), Is.EqualTo(FieldTurretUpgradeResult.Locked));
            Assert.That(turret.RequestLevelUpgrade(null, out FieldTurretBase replacement),
                Is.EqualTo(FieldTurretLevelUpgradeResult.Locked));
            Assert.That(replacement, Is.Null);
            Assert.That(turret.RuntimeState.UpgradeLevel, Is.Zero);
            Assert.That(turret.EffectivePower, Is.EqualTo(originalPower));
            Assert.That(power.ConsumeCalls, Is.Zero);
        }

        [Test]
        public void LockedDestroyedTurretCannotBeRestoredUntilUnlocked()
        {
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(new FieldTurretInitializationData(_definition, 0, isLocked: true), new FakePower());
            Assert.That(turret.ConfigureForTest(), Is.True);
            Assert.That(turret.Restore(), Is.False);
            Assert.That(turret.IsDestroyed, Is.True);
            Assert.That(turret.SetLocked(false), Is.True);
            Assert.That(turret.Restore(), Is.True);
        }

        [Test]
        public void CellDistanceZonesCoverTheGridWithoutStoredCellIds()
        {
            var grid = new FieldTurretGridConfiguration(Vector2.zero, 8, 8, 8, 8);
            var zones = new FieldTurretUnlockZones(grid, new Vector2(-3f, -3f), 4);
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
            var grid = new FieldTurretGridConfiguration(
                Vector2.zero,
                FieldTurretGridConfiguration.DefaultMapWidth,
                FieldTurretGridConfiguration.DefaultMapHeight,
                FieldTurretGridConfiguration.DefaultCellsX,
                FieldTurretGridConfiguration.DefaultCellsY);

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
            var controller = Track(new GameObject("Unlock Controller")).AddComponent<FieldTurretController>();
            var near = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var far = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), controller,
                new Vector3(6f, 6f), Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();

            Assert.That(controller.ConfigureUnlockZones(
                new FieldTurretGridConfiguration(Vector2.zero, 16, 16, 16, 16), Vector2.zero, 4), Is.True);
            Assert.That(near.IsLocked, Is.False);
            Assert.That(far.IsLocked, Is.True);
            Assert.That(far.SetLocked(false), Is.False);
            Assert.That(far.ApplyDamage(25), Is.False);
            Assert.That(far.CurrentHealth, Is.EqualTo(100));
            var middle = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), controller,
                new Vector3(6f, 0f), Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
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
            var controller = Track(new GameObject("Unlock Controller")).AddComponent<FieldTurretController>();
            Assert.That(controller.ConfigureUnlockZones(
                new FieldTurretGridConfiguration(Vector2.zero, 16, 16, 16, 16), Vector2.zero, 4), Is.True);
            var far = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition, shouldActivate: true), controller,
                new Vector3(6f, 6f), Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
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
            var data = new FieldTurretInitializationData(_definition, 60);
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
            var other = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
            SetField(other, "_id", "other-definition");
            var prefab = MakeInactiveTurret();
            Assert.Throws<ArgumentException>(() => FieldTurretFactory.Create(prefab,
                FieldTurretInitializationData.CreateDefault(other), new FakePower(), Vector3.zero, Quaternion.identity));
            Assert.That(FieldTurretInstanceRegistry.Count, Is.Zero);
        }

        [Test]
        public void ActivationAndShutdownDoNotDoubleReserveOrRelease()
        {
            var power = new FakePower();
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(FieldTurretInitializationData.CreateDefault(_definition), power);
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
            var controller = Track(new GameObject("Test Controller")).AddComponent<FieldTurretController>();
            var turret = MakeInactiveTurret();
            SetField(turret, "_turretController", controller, typeof(FieldTurretBase));
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
            var controller = Track(new GameObject("Test Controller")).AddComponent<FieldTurretController>();
            controller.TrySetMaximumPower(30);
            var first = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition, shouldActivate: true), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var second = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition, shouldActivate: true), controller,
                Vector3.one, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            yield return null;
            yield return null;
            Assert.That(first.IsActivated, Is.True);
            Assert.That(second.IsActivated, Is.False);
            Assert.That(controller.GetSnapshot().ReservedPower, Is.EqualTo(20));
            Assert.That(second.LastInitialActivationResult, Is.EqualTo(FieldTurretActivationResult.InsufficientPower));
        }

        [UnityTest]
        public IEnumerator FactoryPublishesInitializedStateBeforeInitialActivation()
        {
            var power = new FakePower();
            var prefab = MakeInactiveTurret();
            FieldTurretSnapshot registered = default;
            bool didRegister = false;
            FieldTurretInstanceRegistry.Registered += turret =>
            {
                didRegister = FieldTurretInstanceRegistry.TryGetSnapshot(turret.InstanceId, out registered);
            };
            var created = Track(FieldTurretFactory.Create(prefab,
                new FieldTurretInitializationData(_definition, 37, shouldActivate: true, upgrade: MakeUpgrade(), upgradeLevel: 2),
                power, Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            Assert.That(didRegister, Is.True);
            Assert.That(registered.CurrentHealth, Is.EqualTo(37));
            Assert.That(registered.IsActivated, Is.False);
            Assert.That(registered.EffectivePower, Is.EqualTo(16));
            yield return null;
            yield return null;
            Assert.That(created.IsActivated, Is.True);
            Assert.That(power.ConsumeCalls, Is.EqualTo(1));
            Assert.That(created.LastInitialActivationResult, Is.EqualTo(FieldTurretActivationResult.Activated));
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
            Assert.That(created.LastInitialActivationResult, Is.EqualTo(FieldTurretActivationResult.InsufficientPower));
        }

        [Test]
        public void LevelChangeTransferStillPreservesUpgradeAndHealthRatio()
        {
            var source = new FieldTurretRuntimeState();
            source.Initialize(new FieldTurretInitializationData(_definition, 37, true, false, 3, MakeUpgrade(), 2));
            source.AssignInstanceId(42);
            var target = new FieldTurretRuntimeState();
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
            var controller = Track(new GameObject("Auto Controller")).AddComponent<FieldTurretController>();
            controller.TrySetMaximumPower(20);
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
            Assert.That(controller.TryStartAutoActivation(), Is.True);
            Assert.That(turret.IsActivated, Is.False);
            Assert.That(controller.IsAutoActivationRunning, Is.True);
            Assert.That(controller.Budget.PendingRecovery, Is.EqualTo(20));
            Assert.That(controller.TryStartAutoActivation(), Is.False);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.AutoActivationInProgress));
            Assert.That(controller.RequestActivation(turret.InstanceId, false), Is.EqualTo(FieldTurretActivationResult.AutoActivationInProgress));
            Assert.That(turret.IsActivated, Is.False);
            controller.Budget.Tick(2.1f);
            typeof(FieldTurretController).GetMethod("AdvanceAutoActivation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(turret.IsActivated, Is.True);
            Assert.That(controller.IsAutoActivationRunning, Is.False);
        }

        [UnityTest]
        public IEnumerator ActiveOverheatUsesSharedCoolingAndResumesWithoutReservingPowerAgain()
        {
            var power = new FakePower();
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), power,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<InitializationProbeFieldTurret>();
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));

            turret.BeginCoolingForTest(0.2f);

            Assert.That(turret.IsCoolingDown, Is.True);
            Assert.That(turret.CoolingDownRemaining, Is.GreaterThan(0f));
            Assert.That(turret.IsActivated, Is.True);
            Assert.That(turret.IsOperational, Is.False);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Unchanged));
            Assert.That(power.ReleaseCalls, Is.Zero);

            yield return new WaitForSeconds(0.25f);
            yield return null;

            Assert.That(turret.IsCoolingDown, Is.False);
            Assert.That(turret.CoolingDownRemaining, Is.Zero);
            Assert.That(turret.IsActivated, Is.True);
            Assert.That(turret.IsOperational, Is.True);
            Assert.That(power.ConsumeCalls, Is.EqualTo(1));
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ActiveTurretRejectsBothUpgradeDirectionsWithoutChangingState(bool upgrade, bool overheated)
        {
            SetField(_definition, "_level", 2);
            var specification = MakeUpgrade();
            var power = new FakePower();
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                new FieldTurretInitializationData(_definition, 70, upgrade: specification, upgradeLevel: 1),
                power, Vector3.zero, Quaternion.identity).gameObject).GetComponent<InitializationProbeFieldTurret>();
            var targetPrefab = MakeLevelPrefab(upgrade ? 3 : 1);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
            if (overheated) turret.BeginCoolingForTest(1f);
            int originalDamage = turret.EffectiveDamage;
            int originalPower = turret.EffectivePower;
            int originalId = turret.InstanceId;

            Assert.That(turret.CanModifyUpgrades, Is.False);
            Assert.That(upgrade ? turret.ApplyUpgrade(specification) : turret.DowngradeUpgrade(specification),
                Is.EqualTo(FieldTurretUpgradeResult.RequiresDeactivation));
            FieldTurretBase replacement;
            Assert.That(upgrade ? turret.RequestLevelUpgrade(targetPrefab, out replacement)
                : turret.RequestLevelDowngrade(targetPrefab, out replacement),
                Is.EqualTo(FieldTurretLevelUpgradeResult.RequiresDeactivation));

            Assert.That(replacement, Is.Null);
            Assert.That(turret.RuntimeState.UpgradeLevel, Is.EqualTo(1));
            Assert.That(turret.EffectiveDamage, Is.EqualTo(originalDamage));
            Assert.That(turret.EffectivePower, Is.EqualTo(originalPower));
            Assert.That(turret.CurrentHealth, Is.EqualTo(70));
            Assert.That(FieldTurretInstanceRegistry.TryGet(originalId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(turret));
            Assert.That(power.ConsumeCalls, Is.EqualTo(1));
            Assert.That(power.ReleaseCalls, Is.Zero);
        }

        [UnityTest]
        public IEnumerator CoolingBlocksUpgradeChangesUntilInactiveTurretIsFullyCooled()
        {
            SetField(_definition, "_level", 2);
            var specification = MakeUpgrade();
            var power = new FakePower();
            var originalPrefab = MakeInactiveTurret();
            var nextPrefab = MakeLevelPrefab(3);
            var previousPrefab = MakeLevelPrefab(1);
            var turret = Track(FieldTurretFactory.Create(originalPrefab,
                new FieldTurretInitializationData(_definition, 70, upgrade: specification, upgradeLevel: 1),
                power, Vector3.zero, Quaternion.identity).gameObject).GetComponent<InitializationProbeFieldTurret>();
            turret.SetCooldownAfterDeactivation(.2f);
            turret.RequestActivation(true);
            turret.RequestActivation(false);
            int originalId = turret.InstanceId;

            Assert.That(turret.IsActivated, Is.False);
            Assert.That(turret.CanModifyUpgrades, Is.False);
            Assert.That(turret.ApplyUpgrade(specification), Is.EqualTo(FieldTurretUpgradeResult.CoolingDown));
            Assert.That(turret.DowngradeUpgrade(specification), Is.EqualTo(FieldTurretUpgradeResult.CoolingDown));
            Assert.That(turret.RequestLevelUpgrade(nextPrefab, out var rejectedUp),
                Is.EqualTo(FieldTurretLevelUpgradeResult.CoolingDown));
            Assert.That(turret.RequestLevelDowngrade(previousPrefab, out var rejectedDown),
                Is.EqualTo(FieldTurretLevelUpgradeResult.CoolingDown));
            Assert.That(rejectedUp, Is.Null);
            Assert.That(rejectedDown, Is.Null);
            Assert.That(turret.RuntimeState.UpgradeLevel, Is.EqualTo(1));

            yield return new WaitForSeconds(.25f);
            yield return null;

            Assert.That(turret.CanModifyUpgrades, Is.True);
            Assert.That(turret.ApplyUpgrade(specification), Is.EqualTo(FieldTurretUpgradeResult.Applied));
            Assert.That(turret.DowngradeUpgrade(specification), Is.EqualTo(FieldTurretUpgradeResult.Downgraded));
            Assert.That(turret.RequestLevelUpgrade(nextPrefab, out var upgraded),
                Is.EqualTo(FieldTurretLevelUpgradeResult.Upgraded));
            Track(upgraded.gameObject);
            Assert.That(upgraded.RequestLevelDowngrade(originalPrefab, out var downgraded),
                Is.EqualTo(FieldTurretLevelUpgradeResult.Downgraded));
            Track(downgraded.gameObject);
            Assert.That(downgraded.InstanceId, Is.EqualTo(originalId));
            Assert.That(downgraded.CurrentHealth, Is.EqualTo(70));
            Assert.That(downgraded.RuntimeState.UpgradeLevel, Is.EqualTo(1));
            Assert.That(downgraded.IsActivated, Is.False);
            Assert.That(downgraded.IsCoolingDown, Is.False);
            Assert.That(power.ConsumeCalls, Is.EqualTo(1), "Upgrade changes must not reserve power.");
        }

        [Test]
        public void AutoActivationBlocksManualMutationsButStillProcessesDamageAndActivation()
        {
            SetField(_definition, "_level", 2);
            var controller = Track(new GameObject("Auto Controller")).AddComponent<FieldTurretController>();
            controller.TrySetMaximumPower(20);
            var specification = MakeUpgrade();
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                new FieldTurretInitializationData(_definition, 70, upgrade: specification, upgradeLevel: 1),
                controller, Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var locked = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition, isLocked: true), controller,
                Vector3.one, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var destroyed = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                new FieldTurretInitializationData(_definition, 0), controller,
                Vector3.down, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var nextPrefab = MakeLevelPrefab(3);
            var previousPrefab = MakeLevelPrefab(1);
            turret.RequestActivation(true);
            Assert.That(controller.TryStartAutoActivation(), Is.True);
            Assert.That(controller.IsAutoActivationRunning, Is.True);
            int originalDamage = turret.EffectiveDamage;
            int originalPower = turret.EffectivePower;
            int pendingPower = controller.Budget.PendingRecovery;

            Assert.That(turret.IsManualOperationBlocked, Is.True);
            Assert.That(turret.CanModifyUpgrades, Is.False);
            Assert.That(turret.ApplyUpgrade(specification), Is.EqualTo(FieldTurretUpgradeResult.AutoActivationInProgress));
            Assert.That(turret.DowngradeUpgrade(specification), Is.EqualTo(FieldTurretUpgradeResult.AutoActivationInProgress));
            Assert.That(turret.RequestLevelUpgrade(nextPrefab, out var rejectedUp),
                Is.EqualTo(FieldTurretLevelUpgradeResult.AutoActivationInProgress));
            Assert.That(turret.RequestLevelDowngrade(previousPrefab, out var rejectedDown),
                Is.EqualTo(FieldTurretLevelUpgradeResult.AutoActivationInProgress));
            Assert.That(rejectedUp, Is.Null);
            Assert.That(rejectedDown, Is.Null);
            Assert.That(turret.SetLocked(true), Is.False);
            Assert.That(locked.SetLocked(false), Is.False);
            Assert.That(destroyed.Restore(), Is.False);
            turret.SetDamageBonus(100);
            turret.AddDamageBonus(100);
            Assert.That(turret.EffectiveDamage, Is.EqualTo(originalDamage));
            Assert.That(turret.EffectivePower, Is.EqualTo(originalPower));
            Assert.That(turret.RuntimeState.UpgradeLevel, Is.EqualTo(1));
            Assert.That(controller.Budget.PendingRecovery, Is.EqualTo(pendingPower));
            Assert.That(turret.ApplyDamage(10), Is.True, "Enemy damage remains a system operation.");
            Assert.That(turret.CurrentHealth, Is.EqualTo(60));

            controller.Budget.Tick(2.1f);
            typeof(FieldTurretController).GetMethod("AdvanceAutoActivation", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(controller, null);
            Assert.That(turret.IsActivated, Is.True);
            Assert.That(controller.IsAutoActivationRunning, Is.False);
            Assert.That(turret.IsManualOperationBlocked, Is.False);
            Assert.That(destroyed.Restore(), Is.True);
        }

        [UnityTest]
        public IEnumerator SwitchingOffDuringCoolingPreservesDeadlineAndDoesNotReactivate()
        {
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), new FakePower(),
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<InitializationProbeFieldTurret>();
            turret.SetCooldownAfterDeactivation(5f);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
            turret.BeginCoolingForTest(0.3f);
            yield return new WaitForSeconds(0.05f);

            float remainingBeforeOff = turret.CoolingDownRemaining;
            Assert.That(turret.RequestActivation(false), Is.EqualTo(FieldTurretActivationResult.Deactivated));
            Assert.That(turret.CoolingDownRemaining, Is.EqualTo(remainingBeforeOff).Within(0.001f));
            Assert.That(turret.IsCoolingDown, Is.True);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.CoolingDown));

            yield return new WaitForSeconds(0.35f);
            yield return null;

            Assert.That(turret.IsCoolingDown, Is.False);
            Assert.That(turret.IsActivated, Is.False);
            Assert.That(turret.IsOperational, Is.False);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
        }

        [Test]
        public void ZeroDurationCoolingDoesNotSuspendAnActiveTurret()
        {
            var turret = MakeInactiveTurret();
            turret.PrepareInitialization(FieldTurretInitializationData.CreateDefault(_definition), new FakePower());
            turret.ConfigureForTest();
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));

            turret.BeginCoolingForTest(0f);

            Assert.That(turret.IsCoolingDown, Is.False);
            Assert.That(turret.CoolingDownRemaining, Is.Zero);
            Assert.That(turret.IsOperational, Is.True);
        }

        [UnityTest]
        public IEnumerator ManualActivationWaitsUntilHeatCooldownCompletes()
        {
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), new FakePower(),
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<InitializationProbeFieldTurret>();
            turret.SetCooldownAfterDeactivation(0.2f);

            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
            Assert.That(turret.RequestActivation(false), Is.EqualTo(FieldTurretActivationResult.Deactivated));
            Assert.That(turret.IsCoolingDown, Is.True);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.CoolingDown));

            yield return new WaitForSeconds(0.25f);

            Assert.That(turret.IsCoolingDown, Is.False);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
        }

        [UnityTest]
        public IEnumerator AutoActivationWaitsUntilCoolingTurretCanRestart()
        {
            var controller = Track(new GameObject("Cooling Auto Controller")).AddComponent<FieldTurretController>();
            controller.TrySetMaximumPower(20);
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<InitializationProbeFieldTurret>();
            turret.SetCooldownAfterDeactivation(0.2f);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));

            Assert.That(controller.TryStartAutoActivation(), Is.True);
            Assert.That(turret.IsActivated, Is.False);
            Assert.That(turret.IsCoolingDown, Is.True);
            Assert.That(controller.IsAutoActivationRunning, Is.True);
            controller.Budget.Tick(2.1f);

            yield return new WaitForSeconds(0.25f);
            yield return null;

            Assert.That(turret.IsActivated, Is.True);
            Assert.That(controller.IsAutoActivationRunning, Is.False);
        }

        [Test]
        public void AutoActivationSkipsUnaffordableCandidateAndExcludesLockedDestroyed()
        {
            var controller = Track(new GameObject("Auto Controller")).AddComponent<FieldTurretController>();
            controller.TrySetMaximumPower(10);
            var costly = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var cheapDefinition = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
            SetField(cheapDefinition, "_id", "cheap");
            SetField(cheapDefinition, "_power", 5);
            var cheapPrefab = MakeInactiveTurret();
            SetField(cheapPrefab, "_definition", cheapDefinition, typeof(FieldTurretBase));
            var cheap = Track(FieldTurretFactory.Create(cheapPrefab,
                FieldTurretInitializationData.CreateDefault(cheapDefinition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var locked = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition, isLocked: true), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var destroyed = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                new FieldTurretInitializationData(_definition, 0), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
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

        [Test]
        public void CoreRegionDynamic_ReleaseDoesNotLockOrDeactivateTurret()
        {
            var controller = Track(new GameObject("Dynamic Region Controller")).AddComponent<FieldTurretController>();
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            var definition = new RegionGridDefinition(new BattlefieldGridConfiguration(
                new BattlefieldPoint(0, 0), 0, 16, 16, 16, 16));
            using var runtime = new TeamHJD.Game.Application.RegionRuntime(new MatchId("dynamic"), definition, RegionPolicyMode.Dynamic);
            controller.SetRegionAccessPolicy(new TeamHJD.Game.Application.RegionAccessPolicy(runtime));
            try
            {
                Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Activated));
                runtime.TryApply(runtime.Snapshot, new[] { 136 }, RegionCaptureUpdateMode.Replace);
                runtime.TryApply(runtime.Snapshot, Array.Empty<int>(), RegionCaptureUpdateMode.Replace);
                Assert.That(runtime.Snapshot.CapturedCellCount, Is.Zero);
                Assert.That(turret.IsLocked, Is.False);
                Assert.That(turret.IsActivated, Is.True);
                Assert.That(controller.GetSnapshot().ReservedPower, Is.EqualTo(20));
            }
            finally { controller.SetRegionAccessPolicy(null); }
        }

        [Test]
        public void CoreRegionStatic_UsesCapturedGridAndRejectsIndependentLegacyUnlock()
        {
            var controller = Track(new GameObject("Static Region Controller")).AddComponent<FieldTurretController>();
            var turret = Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition), controller,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
            using var runtime = new TeamHJD.Game.Application.RegionRuntime(new MatchId("static"),
                new RegionGridDefinition(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, 16, 16, 16, 16)));
            controller.SetRegionAccessPolicy(new TeamHJD.Game.Application.RegionAccessPolicy(runtime));
            try
            {
                Assert.That(turret.IsLocked, Is.True);
                runtime.TryApply(runtime.Snapshot, new[] { 136 }, RegionCaptureUpdateMode.Replace);
                Assert.That(turret.IsLocked, Is.False);
                Assert.That(turret.IsActivated, Is.False);
                Assert.That(controller.UnlockThroughZone(4), Is.False);
                Assert.Throws<InvalidOperationException>(() => controller.ConfigureUnlockZones(Vector2.zero));
            }
            finally { controller.SetRegionAccessPolicy(null); }
        }

        private FieldTurretBase CreateWithActivationIntent(FakePower power)
        {
            return Track(FieldTurretFactory.Create(MakeInactiveTurret(),
                FieldTurretInitializationData.CreateDefault(_definition, shouldActivate: true), power,
                Vector3.zero, Quaternion.identity).gameObject).GetComponent<FieldTurretBase>();
        }

        private InitializationProbeFieldTurret MakeInactiveTurret()
        {
            var go = Track(new GameObject("Initialization Test Turret"));
            go.SetActive(false);
            var turret = go.AddComponent<InitializationProbeFieldTurret>();
            SetField(turret, "_definition", _definition, typeof(FieldTurretBase));
            return turret;
        }

        private FieldTurretUpgradeDefinition MakeUpgrade()
        {
            var upgrade = Track(ScriptableObject.CreateInstance<FieldTurretUpgradeDefinition>());
            SetField(upgrade, "_id", "test-upgrade");
            SetField(upgrade, "_damageModifierRatio", 0.1f);
            SetField(upgrade, "_powerModifierRatio", -0.1f);
            return upgrade;
        }

        private InitializationProbeFieldTurret MakeLevelPrefab(int level)
        {
            var definition = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
            SetField(definition, "_id", $"test-canon-level-{level}");
            SetField(definition, "_level", level);
            SetField(definition, "_maxHealth", 100);
            SetField(definition, "_power", 20);
            var prefab = MakeInactiveTurret();
            SetField(prefab, "_definition", definition, typeof(FieldTurretBase));
            return prefab;
        }

        private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void ResetRegistry() => typeof(FieldTurretInstanceRegistry)
            .GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        private static void SetField(object target, string name, object value, Type owner = null) =>
            (owner ?? target.GetType()).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class FakePower : IFieldTurretPowerSource
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
