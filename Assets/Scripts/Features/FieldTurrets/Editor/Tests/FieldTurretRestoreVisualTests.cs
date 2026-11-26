// 실제 터렛 생성·월드/성장 복원 후 OFF 표시와 과열 표현의 일치를 검사합니다.
// 저장소 파일은 쓰지 않고 실제 제작 프리팹과 Feature writer를 메모리에서 왕복합니다.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FieldTurretRestoreVisualTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [TestCase(100)]
    [TestCase(1000)]
    public void FixedCapacityOverridesSerializedDefaultAndRejectsChanges(int serializedCapacity)
    {
        var owner = new GameObject("Fixed power test");
        owner.SetActive(false);
        try
        {
            var controller = owner.AddComponent<FieldTurretController>();
            typeof(FieldTurretController).GetField("maximumPower", Fields).SetValue(controller, serializedCapacity);
            typeof(FieldTurretController).GetField("fixedMaximumPower", Fields).SetValue(controller, 500);
            Assert.That(controller.MaximumPower, Is.EqualTo(500));
            Assert.That(controller.CurrentPower, Is.EqualTo(500));
            Assert.That(controller.TrySetMaximumPower(serializedCapacity), Is.False);
            Assert.That(controller.TrySetMaximumPower(500), Is.True);
            Assert.That(controller.MaximumPower, Is.EqualTo(500));
        }
        finally { Object.DestroyImmediate(owner); }
    }

    [UnityTest]
    public IEnumerator FixedCapacitySurvivesLegacyGrowthAndWorldRestore()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using var fixture = new Fixture(fixedCapacity: 500);
        var actors = fixture.CreateActors(false);
        actors[0].SetDamageBonus(17);
        foreach (var actor in actors) Assert.That(actor.RequestActivation(true), Is.EqualTo(
            TeamHJD.Game.FieldTurrets.Contracts.FieldTurretActivationResult.Activated));
        yield return null;
        var codec = new BinaryCheckpointPayloadCodec();
        var growth = fixture.Controller.CreateGrowthParticipant();
        ICheckpointParticipant world = fixture.Controller;
        var growthSection = growth.Capture(fixture.Context, codec);
        var worldSection = world.Capture(fixture.Context, codec);
        int reserved = fixture.Controller.Budget.ReservedPower;
        foreach (int oldCapacity in new[] { 100, 1000 })
        {
            // 실제 writer의 나머지 내용은 유지하고, 과거 저장의 용량/잔여 전력만 재현합니다.
            var oldGrowth = WithLegacyCapacity(growthSection, oldCapacity);
            using (var step = growth.PrepareRestore(fixture.Restore(oldGrowth), oldGrowth, codec)) step.Apply();
            Assert.That(fixture.Controller.MaximumPower, Is.EqualTo(500));
            Assert.That(fixture.Controller.CurrentPower, Is.EqualTo(500 - reserved));
            Assert.That(fixture.Controller.GrowthRestoreWarnings.Any(w => w.Contains("고정 전력 500")), Is.True);
            var oldWorld = WithLegacyCapacity(worldSection, oldCapacity, reserved);
            using (var step = world.PrepareRestore(fixture.Restore(oldWorld), oldWorld, codec))
            {
                step.Apply();
                ((ICheckpointReferenceRestoreStep)step).ResolveReferences();
            }
            Assert.That(fixture.Controller.MaximumPower, Is.EqualTo(500));
            Assert.That(fixture.Controller.Budget.ReservedPower, Is.EqualTo(reserved));
            var cannon = fixture.Actors.Single(a => a.Definition.Id.StartsWith("canon_"));
            Assert.That(cannon.EffectiveDamage, Is.EqualTo(cannon.Definition.Damage + 17));
            yield return null;
        }
    }

    [UnityTest]
    public IEnumerator UnfixedCapacityStillRestoresSavedGrowthCapacity()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using var fixture = new Fixture();
        fixture.CreateActors(false);
        yield return null;
        var codec = new BinaryCheckpointPayloadCodec();
        var growth = fixture.Controller.CreateGrowthParticipant();
        var section = growth.Capture(fixture.Context, codec);
        foreach (int capacity in new[] { 100, 1000 })
        {
            var old = WithLegacyCapacity(section, capacity);
            using var step = growth.PrepareRestore(fixture.Restore(old), old, codec);
            step.Apply();
            Assert.That(fixture.Controller.MaximumPower, Is.EqualTo(capacity));
        }
    }

    private static CheckpointSection WithLegacyCapacity(CheckpointSection section, int capacity, int? reserved = null)
    {
        byte[] payload = section.CopyPayload();
        using var stream = new System.IO.MemoryStream(payload, writable: true);
        using var writer = new System.IO.BinaryWriter(stream);
        writer.Write(capacity);
        if (reserved.HasValue) writer.Write(capacity - reserved.Value);
        writer.Flush();
        return new CheckpointSection(section.Key, section.Version, section.Kind, payload);
    }

    [UnityTest]
    public IEnumerator ChangedAuthoring_PreservesKnownGrowthAndRetainsRemovedIds()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using var fixture = new Fixture();
        var original = fixture.CreateActors(false);
        original[0].SetDamageBonus(17);
        original[1].SetDamageBonus(23);
        yield return null;
        var codec = new BinaryCheckpointPayloadCodec();
        var growth = fixture.Controller.CreateGrowthParticipant();
        var saved = growth.Capture(fixture.Context, codec);
        string removedId = original[1].CaptureActorState().EntityId.Value;
        string keptId = original[0].CaptureActorState().EntityId.Value;
        Object.DestroyImmediate(original[1].gameObject);
        original[0].SetDamageBonus(0);
        fixture.CreateActors(false);
        using (var step = growth.PrepareRestore(fixture.Restore(saved), saved, codec)) step.Apply();
        var restored = fixture.Actors.Single(actor => actor.CaptureActorState().EntityId.Value == keptId);
        Assert.That(restored.EffectiveDamage, Is.EqualTo(restored.Definition.Damage + 17));
        Assert.That(fixture.Controller.GrowthRestoreWarnings.Count, Is.GreaterThan(0));
        var roundTrip = growth.Capture(fixture.Context, codec);
        var records = codec.Read(roundTrip, 1, reader =>
        {
            _ = reader.ReadInt32(); int count = reader.ReadInt32();
            var result = new System.Collections.Generic.Dictionary<string, int>();
            for (int i = 0; i < count; i++)
            {
                string id = codec.ReadText(reader); _ = codec.ReadText(reader); _ = codec.ReadText(reader);
                _ = reader.ReadInt32(); result.Add(id, reader.ReadInt32());
            }
            return result;
        });
        Assert.That(records[removedId], Is.EqualTo(23), "사라진 제작 ID도 저장에서 유실되면 안 됩니다.");
        Assert.That(records.Count, Is.EqualTo(fixture.Actors.Length + 1));
    }

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator WorldAndGrowthRestoreKeepColdOffGunsUntintedAndLocksUnchanged()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using var fixture = new Fixture();
        fixture.CreateActors(locked: true);
        fixture.CreateActors(locked: false);
        yield return null; // 실제 Start의 기존 OFF 회색부터 확인한다.
        AssertColdOff(fixture);

        var codec = new BinaryCheckpointPayloadCodec();
        ICheckpointParticipant world = fixture.Controller;
        var worldSection = world.Capture(fixture.Context, codec);
        var growth = fixture.Controller.CreateGrowthParticipant();
        var growthSection = growth.Capture(fixture.Context, codec);
        using (var step = growth.PrepareRestore(fixture.Restore(growthSection), growthSection, codec))
        {
            step.Apply();
            AssertColdOff(fixture); // Start 전에도 실제 생성 경로가 올바른 표시를 복원해야 한다.
            yield return null;
            AssertColdOff(fixture);
        }
        using (var step = world.PrepareRestore(fixture.Restore(worldSection), worldSection, codec))
        {
            step.Apply();
            ((ICheckpointReferenceRestoreStep)step).ResolveReferences();
            AssertColdOff(fixture);
            yield return null;
            AssertColdOff(fixture);
        }
    }

    [UnityTest]
    public IEnumerator CombatRestoreKeepsActiveAndCoolingOffHeatInTheSharedVisualPath()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using var fixture = new Fixture();
        var active = fixture.CreateActors(locked: false);
        var inactive = fixture.CreateActors(locked: false);
        yield return null;
        foreach (var actor in active)
        {
            actor.RequestActivation(true);
            Assert.That(actor.IsActivated, Is.True);
        }
        foreach (var actor in active.Concat(inactive))
            typeof(FieldTurretBase).GetMethod("BeginCooling", Fields).Invoke(actor, new object[] { 10f, .5f });
        // Field 시계는 고정한다. 냉각 완료를 기다리지 않고 실제 공통/무기 상태를 왕복한다.
        var codec = new BinaryCheckpointPayloadCodec();
        ICheckpointParticipant world = fixture.Controller;
        var section = world.Capture(fixture.Context, codec);
        using var step = world.PrepareRestore(fixture.Restore(section), section, codec);
        step.Apply();
        ((ICheckpointReferenceRestoreStep)step).ResolveReferences();
        var actors = fixture.Actors;
        Assert.That(actors.Count(a => a.IsActivated), Is.EqualTo(2));
        foreach (var actor in actors)
        {
            Assert.That(actor.IsCoolingDown, Is.True);
            Assert.That(Gun(actor).color, Is.EqualTo(Color.white));
            Assert.That((float)typeof(FieldTurretBase).GetField("_gunHeatStrength", Fields).GetValue(actor), Is.EqualTo(.5f));
        }
        yield return null; // 기존 LateUpdate가 복원된 열 비율로 실제 Heat Wave를 그리는지 확인한다.
        foreach (var actor in fixture.Actors)
        {
            var wave = (SpriteRenderer)typeof(FieldTurretBase).GetField("_gunHeatWave", Fields).GetValue(actor);
            Assert.That(wave, Is.Not.Null);
            Assert.That(wave.enabled, Is.True);
            Assert.That(wave.color.a, Is.EqualTo(.5f));
            Assert.That(wave.sortingLayerID, Is.EqualTo(Gun(actor).sortingLayerID));
            Assert.That(wave.sortingOrder, Is.GreaterThan(Gun(actor).sortingOrder));
            var led = Gun(actor).GetComponentInChildren<FieldTurretLedIndicator>().GetComponent<SpriteRenderer>();
            Assert.That(wave.sortingOrder, Is.GreaterThan(led.sortingOrder));
            var glow = Gun(actor).transform.Find("Turret Overheat").GetComponent<SpriteRenderer>();
            Assert.That(wave.sprite, Is.SameAs(glow.sprite));
            Assert.That(wave.sharedMaterial.GetTexture("_HeatWaveTex"), Is.SameAs(glow.sprite.texture));
            Assert.That(wave.sharedMaterial.GetFloat("_HeatWaveLocalized"), Is.EqualTo(1f));
            Assert.That(Gun(actor).color.a, Is.EqualTo(1f));
        }
    }


    [UnityTest]
    public IEnumerator TurretStatusEffectsFollowHeatDamageDestructionAndRestore()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using var fixture = new Fixture();
        var actors = fixture.CreateActors(locked: false);
        yield return null;
        foreach (var actor in actors)
        {
            actor.RequestActivation(true);
            Assert.That(actor.IsActivated, Is.True);
            var setHeat = typeof(FieldTurretBase).GetMethod("SetGunHeatVisual", Fields);
            setHeat.Invoke(actor, new object[] { .25f });
            actor.SendMessage("LateUpdate");
            var gun = Gun(actor);
            var wave = gun.transform.Find("Heat Wave").GetComponent<SpriteRenderer>();
            Assert.That(actor.IsCoolingDown, Is.False);
            Assert.That(wave.enabled, Is.True);
            Assert.That(wave.color.a, Is.EqualTo(.25f).Within(.001f));

            fixture.Clock.ElapsedSeconds += .25d;
            setHeat.Invoke(actor, new object[] { .6f });
            actor.SendMessage("LateUpdate");
            Assert.That(wave.color.a, Is.EqualTo(.6f).Within(.001f));

            typeof(FieldTurretBase).GetMethod("BeginCooling", Fields).Invoke(actor, new object[] { 10f, 1f });
            actor.SendMessage("LateUpdate");
            var glow = gun.transform.Find("Turret Overheat").GetComponent<SpriteRenderer>();
            Assert.That(gun.transform.Find("Turret Status Smoke"), Is.Null);
            Assert.That(glow.sprite.name, Is.EqualTo(actor is FieldCanonTurretLv1 ? "canonHeat_Lv1" : $"missileHeat_Lv1_{(int)(gun.sprite.rect.x / 128f)}"));
            Assert.That(glow.sharedMaterial.shader.name, Is.EqualTo("FieldTurrets/HeatEmission"));
            Assert.That(glow.sharedMaterial.GetTexture("_EmissionTex"), Is.SameAs(glow.sprite.texture));
            Assert.That(gun.color, Is.EqualTo(Color.white));
            Assert.That(wave.sortingOrder, Is.GreaterThan(glow.sortingOrder));

            for (int step = 1; step <= 3; step++)
            {
                int threshold = actor.MaxHealth * (4 - step) / 4;
                Assert.That(actor.ApplyDamage(actor.CurrentHealth - threshold), Is.True);
                actor.SendMessage("LateUpdate");
                Assert.That(gun.GetComponentsInChildren<SpriteRenderer>().Count(renderer =>
                    renderer.enabled && renderer.name.StartsWith("Turret Status Smoke")), Is.EqualTo(step));
            }
            var smoke = gun.transform.Find("Turret Status Smoke").GetComponent<SpriteRenderer>();
            Assert.That(smoke.sprite.name, Is.EqualTo("damaged_0"));
            Assert.That(smoke.sharedMaterial.GetTexture("_LedTexture"), Is.SameAs(smoke.sprite.texture));

            Assert.That(actor.ApplyDamage(actor.CurrentHealth), Is.True);
            actor.SendMessage("LateUpdate");
            var flame = gun.transform.Find("Turret Status Flame").GetComponent<SpriteRenderer>();
            Assert.That(smoke.sprite.name, Is.EqualTo("wreckSmoke_0"));
            Assert.That(flame.sprite.name, Is.EqualTo("wreckFlame_0"));
            Assert.That(glow.enabled, Is.False);
            Assert.That(wave.enabled, Is.False);
            var wreck = gun.GetComponentsInChildren<SpriteRenderer>().Where(renderer => renderer.enabled &&
                (renderer.name.StartsWith("Turret Status Smoke") || renderer.name.StartsWith("Turret Status Flame"))).ToArray();
            Assert.That(wreck.Count(renderer => renderer.sprite.name.StartsWith("wreckSmoke_")), Is.EqualTo(4));
            Assert.That(wreck.Count(renderer => renderer.sprite.name.StartsWith("wreckFlame_")), Is.EqualTo(2));
            Assert.That(wreck.Select(renderer => renderer.transform.localPosition).Distinct().Count(), Is.EqualTo(6));

            Vector3 smokeOrigin = smoke.transform.localPosition;
            Vector3 flameOrigin = flame.transform.localPosition;
            Assert.That(smoke.transform.parent, Is.SameAs(gun.transform));
            Assert.That(flame.transform.parent, Is.SameAs(gun.transform));
            Assert.That(Vector3.Distance(smokeOrigin, flameOrigin), Is.GreaterThanOrEqualTo(.4f));
            Vector3 smokePosition = smoke.transform.position;
            gun.transform.Rotate(0f, 0f, 90f, Space.Self);
            actor.SendMessage("LateUpdate");
            Assert.That(smoke.transform.localPosition, Is.EqualTo(smokeOrigin));
            Assert.That(flame.transform.localPosition, Is.EqualTo(flameOrigin));
            Assert.That(Vector3.Distance(smoke.transform.position, smokePosition),
                Is.EqualTo(Mathf.Sqrt(2f) * Vector3.Distance(smokePosition, gun.transform.position)).Within(.001f));

            Assert.That(actor.Restore(), Is.True);
            Assert.That(flame.enabled, Is.False);
            actor.SendMessage("LateUpdate");
            Assert.That(smoke.enabled, Is.True);
            Assert.That(smoke.sprite.name, Is.EqualTo("recovery0_0"));
            Assert.That(smoke.transform.localPosition, Is.EqualTo(smokeOrigin));
            Assert.That(wreck.Count(renderer => renderer.enabled && renderer.sprite.name.StartsWith("recovery")), Is.EqualTo(4));
            Assert.That(wreck.Count(renderer => renderer.enabled && renderer.sprite.name.StartsWith("wreckFlame_")), Is.Zero);
        }

        fixture.Clock.ElapsedSeconds += 1.2d;
        foreach (var actor in actors)
        {
            actor.SendMessage("LateUpdate");
            var smoke = Gun(actor).transform.Find("Turret Status Smoke").GetComponent<SpriteRenderer>();
            Assert.That(smoke.enabled, Is.False);
        }
    }

    [UnityTest]
    public IEnumerator LaserAnimatorReturnsToItsOffPoseAfterFiring()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        using var fixture = new Fixture();
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/FieldTurrets/FieldTurrets/FieldLaserTurret/Stage1/FieldLaserTurret(Green).prefab")
            .GetComponent<FieldLaserTurret>();
        var laser = (FieldLaserTurret)FieldTurretFactory.Create(prefab,
            FieldTurretInitializationData.CreateDefault(prefab.Definition, false),
            fixture.Controller, Vector3.zero, Quaternion.identity);
        yield return null;

        var gun = Gun(laser);
        var animator = gun.GetComponent<Animator>();
        var offSprite = Gun(prefab).sprite;
        animator.Update(.5f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash("Off")));
        Assert.That(gun.sprite, Is.EqualTo(offSprite));

        laser.ActivateTurret();
        Assert.That(laser.IsActivated, Is.True);
        animator.Update(.1f);
        animator.Update(.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash("Active")));

        typeof(FieldLaserTurret).GetMethod("Shoot", Fields).Invoke(laser, null);
        animator.Update(.1f);
        animator.Update(.1f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash("Firing")));
        Assert.That(gun.sprite, Is.Not.EqualTo(offSprite));

        laser.DeactivateTurret();
        Assert.That(laser.IsActivated, Is.False);
        animator.Update(.1f);
        animator.Update(.5f);
        Assert.That(animator.GetCurrentAnimatorStateInfo(0).shortNameHash, Is.EqualTo(Animator.StringToHash("Off")));
        Assert.That(gun.sprite, Is.EqualTo(offSprite));
    }

    private static SpriteRenderer Gun(FieldTurretBase actor)
        => (SpriteRenderer)typeof(FieldTurretBase).GetField("gunRenderer", Fields).GetValue(actor);

    private static void AssertColdOff(Fixture fixture)
    {
        var actors = fixture.Actors;
        Assert.That(actors.Length, Is.EqualTo(4));
        Assert.That(actors.Count(a => a.IsLocked), Is.EqualTo(2));
        foreach (var actor in actors)
        {
            Assert.That(actor.IsActivated, Is.False);
            Assert.That(Gun(actor).color, Is.EqualTo(Color.white), actor.GetType().Name);
            Assert.That((float)typeof(FieldTurretBase).GetField("_gunHeatStrength", Fields).GetValue(actor), Is.Zero);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly FieldTurretController Controller;
        public readonly CheckpointCaptureContext Context;
        public readonly FieldClock Clock;
        private readonly MatchId _match = new MatchId("restore-visual-" + Guid.NewGuid().ToString("N"));
        private readonly IDisposable _scope, _simulation;
        public FieldTurretBase[] Actors => FieldTurretInstanceRegistry.GetAll().Where(Controller.OwnsTurret).ToArray();

        public Fixture(int fixedCapacity = 0)
        {
            var owner = new GameObject("Turret restore visual owner");
            owner.SetActive(false);
            Controller = owner.AddComponent<FieldTurretController>();
            typeof(FieldTurretController).GetField("fixedMaximumPower", Fields).SetValue(Controller, fixedCapacity);
            owner.SetActive(true);
            _scope = Controller.BindMatchScope(_match, () => true);
            Controller.ConfigureCheckpointContent(AssetDatabase.LoadAssetAtPath<FieldTurretLevelUpgradeCatalog>(
                "Assets/Data/FieldTurrets/FieldTurretLevelUpgrade_Stage1.asset"), null);
            Clock = new FieldClock(_match);
            _simulation = Controller.BindFieldSimulation(Clock, default);
            var config = new MatchConfig(_match, MatchMode.Story, new StageId("visual-test"), new DefinitionId("test"),
                "test", 1, Array.Empty<PlayerLoadoutSnapshot>());
            Context = new CheckpointCaptureContext(config, 0, Guid.NewGuid());
        }

        public FieldTurretBase[] CreateActors(bool locked)
        {
            string[] paths = {
                "Assets/Prefabs/FieldTurrets/FieldTurrets/FieldCanonTurret/stage1/FieldCanonTurret LV1.prefab",
                "Assets/Prefabs/FieldTurrets/FieldTurrets/FieldMissileTurret/Stage1/FieldMissileTurret LV1.prefab"
            };
            return paths.Select(path =>
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<FieldTurretBase>();
                var actor = FieldTurretFactory.Create(prefab, FieldTurretInitializationData.CreateDefault(prefab.Definition, locked),
                    Controller, new Vector3(Actors.Length * 10, 0, 0), Quaternion.identity);
                CheckpointActorFactory.EnsureIdentity(actor.gameObject);
                return actor;
            }).ToArray();
        }

        public CheckpointRestoreContext Restore(CheckpointSection section)
        {
            var config = Context.Config;
            var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, _match, config.StageId,
                config.DifficultyProfileId, config.Mode, config.RandomSeed, config.ContentVersion, 0, 1, new[] { section });
            return new CheckpointRestoreContext(Context, data, CheckpointRestorePolicy.Default);
        }

        public void Dispose() { _simulation.Dispose(); _scope.Dispose(); }
    }

    private sealed class FieldClock : IMatchTime
    {
        private readonly MatchId _match;
        private readonly Guid _epoch = Guid.NewGuid();
        public FieldClock(MatchId match) => _match = match;
        public bool IsAvailable => true;
        public double ElapsedSeconds { get; set; } = 1000d;
        public MatchTimeSnapshot Snapshot => new(_match, _epoch, 0, ElapsedSeconds, 0, 1, false, true);
    }
}
