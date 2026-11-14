// 정식 Encounter 연결이 네트워크 개설·테스트 UI·자동 생산을 암묵적으로 시작하지 않는지 검사합니다.
// 격리된 Preview Scene을 사용하며 실제 게임 Scene을 수정하거나 저장하지 않습니다.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

public sealed class EncounterRuntimeSeparationTests
{
    private Scene _scene;
    private GameObject _root;
    private EncounterSceneBinding _binding;
    private EnemyCatalog _enemies;
    private EnemySquadPresetCatalog _squads;

    [SetUp]
    public void SetUp()
    {
        _scene = EditorSceneManager.NewPreviewScene();
        _root = new GameObject("Isolated Encounter binding");
        _root.SetActive(false);
        SceneManager.MoveGameObjectToScene(_root, _scene);
        _binding = _root.AddComponent<EncounterSceneBinding>();
        _root.AddComponent<NetworkObject>();
        var executor = _root.AddComponent<EnemySpawnExecutor>();
        _enemies = ScriptableObject.CreateInstance<EnemyCatalog>();
        _squads = ScriptableObject.CreateInstance<EnemySquadPresetCatalog>();
        typeof(EnemySpawnExecutor).GetField("enemyList", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(executor, _enemies);
        Set("_networkManager", _root.GetComponent<NetworkManager>());
        Set("_spawnExecutor", executor);
        Set("_squadCatalog", _squads);
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(_root);
        Object.DestroyImmediate(_enemies);
        Object.DestroyImmediate(_squads);
        EditorSceneManager.ClosePreviewScene(_scene);
    }

    [Test]
    public void BindAndUnbind_DoNotStartConnectionOrReadWorldWithoutARequest()
    {
        int reads = 0;
        var match = new MatchId("passive-runtime-binding");
        _binding.BindMatch(match, () => { reads++; return null; });
        Assert.That(_binding.BoundMatchId, Is.EqualTo(match));
        Assert.That(_binding.IsListening, Is.False);
        Assert.That(reads, Is.Zero);
        Assert.That(_binding.RecentDiagnostics.Select(e => e.Kind),
            Is.EqualTo(new[] { GameDiagnosticKind.MatchStarted }));

        _binding.UnbindMatch(match);
        Assert.That(_binding.BoundMatchId.IsEmpty, Is.True);
        Assert.That(_binding.IsListening, Is.False);
        Assert.That(reads, Is.Zero);
        Assert.That(_binding.RecentDiagnostics.Last().Kind, Is.EqualTo(GameDiagnosticKind.MatchEnded));
    }

    [Test]
    public void AppOwnership_IsExplicitAndCannotBeTransferredAfterStandaloneBinding()
    {
        var match = new MatchId("ownership-boundary");
        _binding.BindMatch(match, () => null);
        Assert.Throws<InvalidOperationException>(() => _binding.UseAppNetworkOwnership());
        _binding.UnbindMatch(match);
        _binding.UseAppNetworkOwnership();
        _binding.BindAuthorityMatch(match, new PlayerId("host"), () => null);
        _binding.UseAppNetworkOwnership();
        _binding.UnbindMatch(match);
        Assert.That(_binding.IsAppNetworkOwned, Is.True);
        Assert.That(_binding.IsListening, Is.False);
    }

    [Test]
    public void RuntimeBinding_HasNoTestLauncherOrAutomaticStartupHooks()
    {
        var flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.DeclaredOnly;
        Assert.That(typeof(EncounterSceneBinding).GetFields(flags)
            .Any(field => field.FieldType.Name == "EnemyNetworkTestLauncher"), Is.False);
        Assert.That(typeof(EncounterSceneBinding).GetMethod("OnGUI", flags), Is.Null);
        Assert.That(typeof(EncounterSceneBinding).GetMethod("Start", flags), Is.Null);
        Assert.That(_root.GetComponent<EnemyNetworkTestControls>(), Is.Null,
            "테스트 조작은 사용자가 명시적으로 추가했을 때만 존재해야 합니다.");
    }

    [Test]
    public void OldCheckpoint_TestStartupSlotsRemainReadableButAreNotSavedAsRuntimeState()
    {
        _binding.BindMatch(new MatchId("legacy-test-slots"), () => null);
        var type = typeof(EncounterSceneBinding);
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        var write = type.GetMethod("WriteEncounter", flags);
        var read = type.GetMethod("ReadEncounter", flags);
        var state = type.GetMethod("CaptureEncounter", BindingFlags.NonPublic | BindingFlags.Instance)
            .Invoke(_binding, null);
        var codec = new BinaryCheckpointPayloadCodec();
        var original = codec.Write("world/encounter", 3, CheckpointSectionKind.World,
            writer => write.Invoke(null, new[] { writer, state, codec }));
        byte[] legacy = original.CopyPayload();
        legacy[0] = 1;
        BitConverter.GetBytes(9d).CopyTo(legacy, 1);
        var section = new CheckpointSection(original.Key, 3, original.Kind, legacy);
        var decoded = codec.Read(section, 3, reader => read.Invoke(null, new object[] { reader, codec, 3 }));
        var rewritten = codec.Write(original.Key, 3, original.Kind,
            writer => write.Invoke(null, new[] { writer, decoded, codec }));

        using var stream = new MemoryStream(rewritten.CopyPayload());
        using var binary = new BinaryReader(stream);
        Assert.That(binary.ReadBoolean(), Is.False);
        Assert.That(binary.ReadDouble(), Is.Zero);
        Assert.That(rewritten.CopyPayload(), Is.EqualTo(original.CopyPayload()),
            "예산·분산 이력·예약 payload는 보존하고 폐기된 테스트 시작 상태만 제거해야 합니다.");
    }

    private void Set(string name, object value) => typeof(EncounterSceneBinding)
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(_binding, value);
}
