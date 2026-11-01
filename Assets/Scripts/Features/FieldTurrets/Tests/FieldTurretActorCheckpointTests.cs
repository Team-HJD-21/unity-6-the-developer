// 실제 생성·등록 경로에서 냉각/수리/시작 의도와 공용 전력·자동 큐의 복원을 확인합니다.
// 종류별 무기/Enemy 참조를 포함하는 전체 Scene 저장 인수 검사와 구분합니다.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Object = UnityEngine.Object;

namespace TeamHJD.Game.FieldTurrets.Tests
{
    public sealed class FieldTurretActorCheckpointTests
    {
        private sealed class Source : ITimestampSource
        { public double Now; public bool TryReadSeconds(out double seconds) { seconds = Now; return true; } }
        private readonly List<Object> _objects = new();
        private readonly List<Scene> _scenes = new();
        private readonly List<IDisposable> _leases = new();
        private FieldTurretDefinition _definition;
        private FieldTurretController _owner;
        private Source _source;
        private MatchClock _clock;
        private Scene _originalScene;

        [SetUp]
        public void SetUp()
        {
            ResetRegistry(); _originalScene = SceneManager.GetActiveScene();
            _definition = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
            Set(_definition, "_id", "checkpoint-actor"); Set(_definition, "_maxHealth", 100); Set(_definition, "_power", 20);
            _owner = Track(new GameObject("Checkpoint Owner")).AddComponent<FieldTurretController>();
            _owner.TrySetMaximumPower(100);
            _source = new Source(); _clock = new MatchClock(new MatchId("checkpoint-turrets"), _source);
            _leases.Add(_clock); _leases.Add(_owner.BindFieldSimulation(_clock, default));
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            for (int i = _leases.Count - 1; i >= 0; i--) _leases[i].Dispose();
            _leases.Clear();
            for (int i = _objects.Count - 1; i >= 0; i--) if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
            _objects.Clear();
            if (_originalScene.IsValid() && _originalScene.isLoaded) SceneManager.SetActiveScene(_originalScene);
            foreach (var scene in _scenes) if (scene.IsValid() && scene.isLoaded) yield return SceneManager.UnloadSceneAsync(scene);
            _scenes.Clear(); ResetRegistry();
        }

        [UnityTest]
        public IEnumerator RecreatedActorPreservesCoolingRepairHeadAndPowerWithoutReplayingDamage()
        {
            var parent = Track(new GameObject("Turret Group")).transform; parent.localScale = new Vector3(2, 3, 1);
            var prefab = Prefab(); var actor = Spawn(prefab, "turret/one", parent);
            actor.transform.localScale = new Vector3(.75f, .5f, 1);
            var head = actor.transform.Find("Head"); head.localRotation = Quaternion.Euler(0, 0, 31);
            actor.RequestActivation(true); actor.Repair(1f, .625f); actor.BeginCoolingForTest(8);
            _source.Now = 2; _clock.Pump(); actor.SetFiringProgressForTest(.25f); _clock.SetPaused(true);
            var savedActor = actor.CaptureActorState(); var savedOwner = _owner.CaptureControllerState();
            int previousId = actor.InstanceId; actor.DetachCheckpointReservation(); CheckpointActorFactory.Retire(actor.gameObject);
            _clock.Restore(100); bool sawIdentityBeforeRegistration = false; int damage = 0, revived = 0;
            Action<FieldTurretBase> observe = turret => sawIdentityBeforeRegistration = turret.TryGetEntityId(out var id) && id == savedActor.EntityId;
            FieldTurretInstanceRegistry.Registered += observe;
            FieldTurretInstanceRegistry.HealthChanged += (_, _, _) => damage++;
            FieldTurretInstanceRegistry.Restored += _ => revived++;
            var restored = (InitializationProbeFieldTurret)Track(FieldTurretFactory.CreateForCheckpoint(prefab, savedActor, _owner, null, parent).gameObject)
                .GetComponent<FieldTurretBase>();
            FieldTurretInstanceRegistry.Registered -= observe;
            _owner.RestoreControllerState(savedOwner);
            Assert.That(sawIdentityBeforeRegistration, Is.True);
            Assert.That(restored.InstanceId, Is.Not.EqualTo(previousId)); Assert.That(restored.CurrentHealth, Is.EqualTo(40));
            Assert.That(restored.RuntimeState.IsTemporarilySuspended, Is.True); Assert.That(restored.CoolingDownRemaining, Is.EqualTo(6).Within(.001));
            Assert.That(restored.FiringProgressForTest, Is.EqualTo(.25f));
            Assert.That(restored.transform.localScale, Is.EqualTo(savedActor.Pose.LocalScale));
            Assert.That(Quaternion.Angle(restored.transform.Find("Head").localRotation, savedActor.HeadLocalRotation), Is.LessThan(.001f));
            Assert.That(_owner.CurrentPower, Is.EqualTo(80)); Assert.That(_owner.Budget.ReservedPower, Is.EqualTo(20));
            Assert.That(_owner.Budget.PendingRecovery, Is.Zero); Assert.That(damage, Is.Zero); Assert.That(revived, Is.Zero);
            restored.Repair(1f, .375f); Assert.That(restored.CurrentHealth, Is.EqualTo(41));
            _clock.SetPaused(false); _source.Now += 7; _clock.Pump(); yield return null;
            Assert.That(restored.IsCoolingDown, Is.False); Assert.That(restored.IsOperational, Is.True);
            restored.RequestActivation(false); Assert.That(_owner.Budget.PendingRecovery, Is.EqualTo(20));
        }

        [UnityTest]
        public IEnumerator AutoActivationQueueResolvesNewLocalIdsAndKeepsItsDeadline()
        {
            var prefab = Prefab(); var actor = Spawn(prefab, "turret/queued"); actor.BeginCoolingForTest(4);
            Assert.That(_owner.TryStartAutoActivation(), Is.True); Assert.That(_owner.IsAutoActivationRunning, Is.True);
            _clock.SetPaused(true); var savedActor = actor.CaptureActorState(); var savedOwner = _owner.CaptureControllerState();
            int previous = actor.InstanceId; actor.DetachCheckpointReservation(); CheckpointActorFactory.Retire(actor.gameObject);
            _clock.Restore(100);
            var restored = Track(FieldTurretFactory.CreateForCheckpoint(prefab, savedActor, _owner, null).gameObject).GetComponent<FieldTurretBase>();
            _owner.RestoreControllerState(savedOwner);
            Assert.That(restored.InstanceId, Is.Not.EqualTo(previous));
            Assert.That(_owner.AutoActivationCandidates[0].InstanceId, Is.EqualTo(restored.InstanceId));
            Assert.That(_owner.AutoActivationCandidates[0].EntityId, Is.EqualTo(savedActor.EntityId));
            Assert.That(_owner.AutoActivationCooldownRemaining, Is.EqualTo(60).Within(.001));
            _clock.SetPaused(false); _source.Now += 5; _clock.Pump(); yield return null;
            Assert.That(restored.IsActivated, Is.True); Assert.That(_owner.IsAutoActivationRunning, Is.False);
            Assert.That(_owner.AutoActivationCooldownRemaining, Is.EqualTo(55).Within(.001)); Assert.That(_owner.CurrentPower, Is.EqualTo(80));
        }

        [UnityTest]
        public IEnumerator PendingStartupIntentSurvivesRestoreAndWaitsForFieldResume()
        {
            _clock.SetPaused(true); var prefab = Prefab(); var actor = Spawn(prefab, "turret/pending", activate: true);
            yield return null; yield return null;
            Assert.That(actor.IsActivated, Is.False); var savedActor = actor.CaptureActorState();
            Assert.That(savedActor.PendingInitialActivation, Is.True); var savedOwner = _owner.CaptureControllerState();
            actor.DetachCheckpointReservation(); CheckpointActorFactory.Retire(actor.gameObject);
            var restored = Track(FieldTurretFactory.CreateForCheckpoint(prefab, savedActor, _owner, null).gameObject).GetComponent<FieldTurretBase>();
            _owner.RestoreControllerState(savedOwner); yield return null; yield return null;
            Assert.That(restored.IsActivated, Is.False); Assert.That(_owner.CurrentPower, Is.EqualTo(100));
            _clock.SetPaused(false); yield return null; yield return null;
            Assert.That(restored.IsActivated, Is.True); Assert.That(_owner.CurrentPower, Is.EqualTo(80));
            Assert.That(restored.CaptureActorState().PendingInitialActivation, Is.False);
        }

        [UnityTest]
        public IEnumerator FactoryUsesPowerOwnerSceneAndClockWhileAnotherHubIsActive()
        {
            var prefab = Prefab(); var hub = SceneManager.CreateScene("Checkpoint Active Hub"); _scenes.Add(hub);
            SceneManager.SetActiveScene(hub);
            var actor = Spawn(prefab, "turret/owner-scene"); actor.BeginCoolingForTest(8);
            Assert.That(actor.gameObject.scene, Is.EqualTo(_owner.gameObject.scene));
            Assert.That(FieldTurretController.FindForScene(hub), Is.Null);
            _source.Now = 3; _clock.Pump(); yield return null;
            Assert.That(actor.CoolingDownRemaining, Is.EqualTo(5).Within(.001), "Cooling must use its explicit power owner's Match clock.");
        }

        [Test]
        public void InvalidBudgetOrMissingQueueTargetIsRejectedBeforeChangingTheOwner()
        {
            var actor = Spawn(Prefab(), "turret/power"); actor.RequestActivation(true);
            var badBudget = new FieldTurretControllerState(new FieldTurretPowerBudget.State(999, 999, 0, 0), 0, false,
                Array.Empty<FieldTurretControllerState.Candidate>());
            Assert.Throws<ArgumentException>(() => _owner.RestoreControllerState(badBudget));
            Assert.That(_owner.MaximumPower, Is.EqualTo(100)); Assert.That(_owner.CurrentPower, Is.EqualTo(80));
            var missing = new FieldTurretControllerState(_owner.Budget.CaptureState(), 60, true,
                new[] { new FieldTurretControllerState.Candidate(new EntityId("turret/missing"), 3, 3, false) });
            Assert.Throws<ArgumentException>(() => _owner.RestoreControllerState(missing));
            Assert.That(_owner.AutoActivationCandidates, Is.Empty); Assert.That(_owner.CurrentPower, Is.EqualTo(80));
        }

        [Test]
        public void PrefabCatalogRejectsAmbiguousAssetsInsteadOfChoosingTheFirstMatch()
        {
            var first = Prefab(); var second = Prefab(); var catalog = Track(ScriptableObject.CreateInstance<FieldTurretLevelUpgradeCatalog>());
            SetTransitions(catalog, first, first);
            Assert.That(catalog.TryGetPrefab(_definition.Id, out var resolved), Is.True); Assert.That(resolved, Is.SameAs(first));
            SetTransitions(catalog, first, second);
            Assert.That(catalog.TryGetDefinition(_definition.Id, out _), Is.True);
            Assert.That(catalog.TryGetPrefab(_definition.Id, out resolved), Is.False); Assert.That(resolved, Is.Null);
        }

        private InitializationProbeFieldTurret Prefab()
        {
            var go = Track(new GameObject("Checkpoint Turret Prefab")); go.SetActive(false);
            var actor = go.AddComponent<InitializationProbeFieldTurret>(); var head = new GameObject("Head").transform; head.SetParent(go.transform, false);
            Set(actor, "_definition", _definition, typeof(FieldTurretBase)); Set(actor, "turretRotationPoint", head, typeof(FieldTurretBase));
            return actor;
        }
        private InitializationProbeFieldTurret Spawn(InitializationProbeFieldTurret prefab, string id, Transform parent = null, bool activate = false)
        {
            var actor = Track(FieldTurretFactory.Create(prefab, new FieldTurretInitializationData(_definition, 40, shouldActivate: activate),
                _owner, new Vector3(3, 4, 0), Quaternion.identity, parent).gameObject).GetComponent<InitializationProbeFieldTurret>();
            actor.gameObject.AddComponent<SceneEntityIdentity>().Initialize(new EntityId(id)); return actor;
        }
        private static void SetTransitions(FieldTurretLevelUpgradeCatalog catalog, params FieldTurretBase[] prefabs)
        {
            var type = typeof(FieldTurretLevelUpgradeCatalog).GetNestedType("Transition", BindingFlags.NonPublic);
            var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type));
            foreach (var prefab in prefabs)
            { object transition = Activator.CreateInstance(type); Set(transition, "_source", prefab.Definition); Set(transition, "_sourceLevelPrefab", prefab); list.Add(transition); }
            Set(catalog, "_transitions", list);
        }
        private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }
        private static void Set(object target, string field, object value, Type owner = null) =>
            (owner ?? target.GetType()).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        private static void ResetRegistry() => typeof(FieldTurretInstanceRegistry).GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
    }
}
