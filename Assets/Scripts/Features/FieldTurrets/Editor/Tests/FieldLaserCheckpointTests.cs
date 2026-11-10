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

public sealed class FieldLaserCheckpointTests
{
    [UnityTearDown]
    public IEnumerator LeavePlayMode()
    {
        if (UnityEngine.Application.isPlaying)
            yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RegisteredLaserRestoresBeamThroughFieldTurretCheckpoint()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();

        var catalog = AssetDatabase.LoadAssetAtPath<FieldTurretLevelUpgradeCatalog>(
            "Assets/Data/FieldTurrets/FieldTurretLevelUpgrade_Stage1.asset");
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/FieldTurrets/FieldTurrets/FieldLaserTurret/Stage1/FieldLaserTurret(Green).prefab")
            .GetComponent<FieldLaserTurret>();
        Assert.That(catalog.TryGetPrefab(prefab.Definition.Id, out var registeredPrefab), Is.True);
        Assert.That(registeredPrefab, Is.SameAs(prefab));

        var controller = new GameObject("Laser checkpoint controller").AddComponent<FieldTurretController>();
        var matchId = new MatchId("laser-checkpoint-test");
        using var match = controller.BindMatchScope(matchId, () => true);
        controller.ConfigureCheckpointContent(catalog, null);
        var original = (FieldLaserTurret)FieldTurretFactory.Create(prefab,
            FieldTurretInitializationData.CreateDefault(prefab.Definition), controller, Vector3.zero, Quaternion.identity);
        var actorId = CheckpointActorFactory.EnsureIdentity(original.gameObject);
        Assert.That(controller.OwnsTurret(original), Is.True);

        var config = new MatchConfig(matchId, MatchMode.Story, new StageId("test"), new DefinitionId("test"),
            "test", 1, Array.Empty<PlayerLoadoutSnapshot>());
        var context = new CheckpointCaptureContext(config, 0, Guid.NewGuid());
        ICheckpointParticipant participant = controller;
        var codec = new BinaryCheckpointPayloadCodec();
        Assert.That(participant.Capture(context, codec).Key, Is.EqualTo("world/field-turrets")); // OFF 상태도 저장 경로를 통과한다.

        original.ActivateTurret();
        Assert.That(original.IsActivated, Is.True);
        BeamField("_isBeamPlaying").SetValue(original, true);
        BeamField("_beamOrigin").SetValue(original, new Vector3(2f, 3f, 0f));
        BeamField("_beamDirection").SetValue(original, Vector2.right);
        BeamField("_beamLength").SetValue(original, 5f);
        BeamField("_visibleLengthRatio").SetValue(original, 1f);
        BeamField("_visibleWidthRatio").SetValue(original, 1f);
        BeamField("_beamEndsAt").SetValue(original, Time.timeAsDouble + 0.2d);
        BeamField("_nextDamageAt").SetValue(original, Time.timeAsDouble + 0.05d);
        var clock = new FieldClock(matchId) { Paused = true };
        using var simulation = controller.BindFieldSimulation(clock, default);
        Assert.That(Deadline(original, "_beamEndsAt") - clock.ElapsedSeconds, Is.EqualTo(0.2d).Within(0.001d));
        var section = participant.Capture(context, codec);
        var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, matchId, config.StageId,
            config.DifficultyProfileId, MatchMode.Story, 1, "test", 0, 1, new[] { section });

        using var step = participant.PrepareRestore(new CheckpointRestoreContext(context, data, new CheckpointRestorePolicy()), section, codec);
        step.Apply();
        ((ICheckpointReferenceRestoreStep)step).ResolveReferences();

        var restored = FieldTurretInstanceRegistry.GetAll().OfType<FieldLaserTurret>().Single(controller.OwnsTurret);
        Assert.That(restored, Is.Not.SameAs(original));
        Assert.That(restored.TryGetEntityId(out var restoredId) && restoredId == actorId, Is.True);
        Assert.That(BeamField("_isBeamPlaying").GetValue(restored), Is.True);
        Assert.That(BeamField("_beamDirection").GetValue(restored), Is.EqualTo(Vector2.right));
        double endsAt = Deadline(restored, "_beamEndsAt");
        double damageAt = Deadline(restored, "_nextDamageAt");
        Assert.That(endsAt - clock.ElapsedSeconds, Is.EqualTo(0.2d).Within(0.001d));
        Assert.That(damageAt - clock.ElapsedSeconds, Is.EqualTo(0.05d).Within(0.001d));

        double unityStart = Time.timeAsDouble;
        while (Time.timeAsDouble - unityStart < 0.3d)
            yield return null;
        restored.SendMessage("Update");
        Assert.That(BeamField("_isBeamPlaying").GetValue(restored), Is.True);
        Assert.That(Deadline(restored, "_nextDamageAt"), Is.EqualTo(damageAt));
        var heat = typeof(DefaultFieldLaserTurret).GetProperty("FireTime", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(heat.GetValue(restored), Is.EqualTo(0f));

        clock.Paused = false;
        clock.ElapsedSeconds = damageAt;
        Assert.That(controller.CanSimulate, Is.True);
        Assert.That(restored.IsOperational && !restored.IsCoolingDown, Is.True);
        restored.SendMessage("Update");
        Assert.That(BeamField("_isBeamPlaying").GetValue(restored), Is.True);
        Assert.That(Deadline(restored, "_nextDamageAt"), Is.GreaterThan(damageAt));

        clock.ElapsedSeconds = endsAt;
        restored.SendMessage("Update");
        Assert.That(BeamField("_isBeamPlaying").GetValue(restored), Is.False);
    }

    private static double Deadline(FieldLaserTurret turret, string name)
        => (double)BeamField(name).GetValue(turret);

    private static FieldInfo BeamField(string name)
        => typeof(FieldLaserTurret).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);

    private sealed class FieldClock : IMatchTime
    {
        private readonly MatchId _matchId;
        private readonly Guid _epoch = Guid.NewGuid();

        public FieldClock(MatchId matchId) => _matchId = matchId;
        public double ElapsedSeconds { get; set; } = 1000d;
        public bool Paused { get; set; }
        public bool IsAvailable => true;
        public MatchTimeSnapshot Snapshot => new(_matchId, _epoch, 0, ElapsedSeconds, 0, 1, Paused, true);
    }
}
