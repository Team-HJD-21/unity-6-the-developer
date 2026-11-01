// 실제 CU 컴포넌트의 체력·위치 복원과 파괴 사건 억제를 검증합니다.
// 제작 정의 불일치는 적용 전에 거부하고 현재 Scene의 값을 보존해야 합니다.
using System;
using System.IO;
using NUnit.Framework;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Infrastructure;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class ControlUnitCheckpointTests
{
    private GameObject _object;
    private ControlUnitHealth _health;
    private FieldTurretDefinition _definition;
    private readonly BinaryCheckpointPayloadCodec _codec = new();
    private CheckpointCaptureContext _context;
    private ICheckpointParticipant Writer => _health;

    [SetUp]
    public void SetUp()
    {
        _object = new GameObject("CU checkpoint test");
        _object.SetActive(false);
        _health = _object.AddComponent<ControlUnitHealth>();
        _definition = ScriptableObject.CreateInstance<FieldTurretDefinition>();
        var definition = new SerializedObject(_definition);
        definition.FindProperty("_id").stringValue = "test-cu";
        definition.FindProperty("_role").enumValueIndex = (int)FieldTurretRole.ControlUnit;
        definition.FindProperty("_maxHealth").intValue = 1500;
        definition.ApplyModifiedPropertiesWithoutUndo();
        var component = new SerializedObject(_health);
        component.FindProperty("_definition").objectReferenceValue = _definition;
        component.ApplyModifiedPropertiesWithoutUndo();
        _object.SetActive(true);
        // EditMode는 MonoBehaviour 메시지를 실행하지 않으므로 실제 초기화 본문만 명시 호출합니다.
        typeof(ControlUnitHealth).GetMethod("Awake", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Invoke(_health, null);
        var config = new MatchConfig(new MatchId("current"), MatchMode.Story, new StageId("stage"),
            new DefinitionId("normal"), "content", 3, new[] { new PlayerLoadoutSnapshot(new PlayerId("host"), null) });
        _context = new CheckpointCaptureContext(config, 5, Guid.NewGuid());
    }

    [TearDown]
    public void TearDown() { Object.DestroyImmediate(_object); Object.DestroyImmediate(_definition); }

    private CheckpointRestoreContext Context(CheckpointSection section)
    {
        var c = _context.Config;
        var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, c.MatchId, c.StageId, c.DifficultyProfileId,
            c.Mode, c.RandomSeed, c.ContentVersion, 5, 1, new[] { section });
        return new CheckpointRestoreContext(_context, data, CheckpointRestorePolicy.Default);
    }

    [Test]
    public void ActualCuHealthAndPoseApplyAndRollbackWithoutDestructionEvent()
    {
        _health.ApplyDamage(200); _object.transform.position = new Vector3(1, 2, 3);
        var saved = Writer.Capture(_context, _codec);
        _health.ApplyDamage(300); _object.transform.position = new Vector3(4, 5, 6);
        int destroyed = 0, healthChanges = 0;
        _health.Destroyed += () => destroyed++;
        _health.HealthChanged += (_, _) => healthChanges++;
        using var step = Writer.PrepareRestore(Context(saved), saved, _codec);
        Assert.That(_health.CurrentHealth, Is.EqualTo(1000));
        step.Apply();
        Assert.That(_health.CurrentHealth, Is.EqualTo(1300));
        Assert.That(_object.transform.position, Is.EqualTo(new Vector3(1, 2, 3)));
        step.Rollback();
        Assert.That(_health.CurrentHealth, Is.EqualTo(1000));
        Assert.That(_object.transform.position, Is.EqualTo(new Vector3(4, 5, 6)));
        Assert.That(destroyed, Is.Zero); Assert.That(healthChanges, Is.EqualTo(2));
    }

    [Test]
    public void ChangedDefinitionRejectsBeforeChangingHealth()
    {
        var saved = Writer.Capture(_context, _codec);
        var definition = new SerializedObject(_definition);
        definition.FindProperty("_id").stringValue = "different-cu"; definition.ApplyModifiedPropertiesWithoutUndo();
        _health.ApplyDamage(300);
        Assert.Throws<InvalidDataException>(() => Writer.PrepareRestore(Context(saved), saved, _codec));
        Assert.That(_health.CurrentHealth, Is.EqualTo(1200));
    }

    [Test]
    public void CompletedOrUnconfiguredCuCannotBeCapturedAsLiveWorld()
    {
        _health.ApplyDamage(1500);
        Assert.Throws<InvalidOperationException>(() => Writer.Capture(_context, _codec));
    }
}
