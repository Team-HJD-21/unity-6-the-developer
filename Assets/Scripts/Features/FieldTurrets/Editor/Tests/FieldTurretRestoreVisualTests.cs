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

public sealed class FieldTurretRestoreVisualTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;

    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator WorldAndGrowthRestoreKeepColdOffGunsGrayAndLocksUnchanged()
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
            Assert.That(Gun(actor).color, Is.EqualTo(Color.Lerp(Color.white, new Color(1f, .25f, .25f), .5f)));
            Assert.That((float)typeof(FieldTurretBase).GetField("_gunHeatStrength", Fields).GetValue(actor), Is.EqualTo(.5f));
        }
        yield return null; // 기존 LateUpdate가 복원된 열 비율로 실제 Heat Wave를 그리는지 확인한다.
        foreach (var actor in fixture.Actors)
        {
            var wave = (SpriteRenderer)typeof(FieldTurretBase).GetField("_gunHeatWave", Fields).GetValue(actor);
            Assert.That(wave, Is.Not.Null);
            Assert.That(wave.enabled, Is.True);
            Assert.That(wave.color.a, Is.EqualTo(.5f));
            Assert.That(Gun(actor).color.a, Is.EqualTo(1f));
        }
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
            Assert.That(Gun(actor).color, Is.EqualTo(Color.gray), actor.GetType().Name);
            Assert.That((float)typeof(FieldTurretBase).GetField("_gunHeatStrength", Fields).GetValue(actor), Is.Zero);
        }
    }

    private sealed class Fixture : IDisposable
    {
        public readonly FieldTurretController Controller;
        public readonly CheckpointCaptureContext Context;
        private readonly MatchId _match = new MatchId("restore-visual-" + Guid.NewGuid().ToString("N"));
        private readonly IDisposable _scope, _simulation;
        public FieldTurretBase[] Actors => FieldTurretInstanceRegistry.GetAll().Where(Controller.OwnsTurret).ToArray();

        public Fixture()
        {
            Controller = new GameObject("Turret restore visual owner").AddComponent<FieldTurretController>();
            _scope = Controller.BindMatchScope(_match, () => true);
            Controller.ConfigureCheckpointContent(AssetDatabase.LoadAssetAtPath<FieldTurretLevelUpgradeCatalog>(
                "Assets/Data/FieldTurrets/FieldTurretLevelUpgrade_Stage1.asset"), null);
            _simulation = Controller.BindFieldSimulation(new FieldClock(_match), default);
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
        public double ElapsedSeconds => 1000d;
        public MatchTimeSnapshot Snapshot => new(_match, _epoch, 0, ElapsedSeconds, 0, 1, false, true);
    }
}
