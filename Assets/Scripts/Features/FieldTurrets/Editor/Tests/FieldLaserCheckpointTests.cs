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
        BeamField("_beamEndsAt").SetValue(original, Time.time + 2f);
        BeamField("_nextDamageAt").SetValue(original, Time.time + 0.2f);
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
        Assert.That((float)BeamField("_beamEndsAt").GetValue(restored) - Time.time, Is.EqualTo(2f).Within(0.001f));
        Assert.That((float)BeamField("_nextDamageAt").GetValue(restored) - Time.time, Is.EqualTo(0.2f).Within(0.001f));
    }

    private static FieldInfo BeamField(string name)
        => typeof(FieldLaserTurret).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
}
