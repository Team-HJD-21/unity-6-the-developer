// SpawnPoint 복제/추가/비활성/제거와 Scene 경계를 검증합니다.
// 실제 적 생성 없이 등록과 ID 정책만 좁게 확인합니다.
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class SpawnPointRegistrationTests
{
    private Scene _scene;

    [SetUp]
    public void SetUp()
    {
        // Headless runner의 저장되지 않은 Untitled Scene에서는 Additive 생성이 거부됩니다.
        // Batch에만 Single을 사용하고 열린 사용자 Editor에서는 기존 Scene을 유지합니다.
        _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,
            Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
    }

    [TearDown]
    public void TearDown()
    {
        if (_scene.IsValid() && _scene.isLoaded) EditorSceneManager.CloseScene(_scene, true);
    }

    [Test]
    public void Registry_RepairsDuplicateAndEmptyIds_WithoutChangingValidOriginal()
    {
        SpawnPoint original = Point("Original", "EncounterLeft");
        SpawnPoint duplicate = Object.Instantiate(original);
        SpawnPoint empty = Point("Empty", "");
        var registry = new SpawnPointRegistry();
        registry.Initialize(new[] { original, duplicate, empty, original, null });
        Assert.That(registry.GetRegisteredSpawnPoints().Count, Is.EqualTo(3));
        Assert.That(original.SpawnPointId, Is.EqualTo("EncounterLeft"));
        string duplicateId = duplicate.SpawnPointId;
        string emptyId = empty.SpawnPointId;
        Assert.That(new[] { original.SpawnPointId, duplicateId, emptyId }.Distinct().Count(), Is.EqualTo(3));
        registry.Initialize(new[] { empty, duplicate, original });
        Assert.That(duplicate.SpawnPointId, Is.EqualTo(duplicateId));
        Assert.That(empty.SpawnPointId, Is.EqualTo(emptyId));
        Assert.That(registry.TryGetSpawnPoint(duplicateId, out SpawnPoint resolved), Is.True);
        Assert.That(resolved, Is.SameAs(duplicate));
    }

    [Test]
    public void Executor_DiscoversAllSceneRootsAndLateCopies_WithoutForeignScene()
    {
        var ownerObject = new GameObject("Executor");
        SceneManager.MoveGameObjectToScene(ownerObject, _scene);
        EnemySpawnExecutor executor = ownerObject.AddComponent<EnemySpawnExecutor>();
        SpawnPoint first = Point("First", "First");
        first.transform.SetParent(ownerObject.transform);
        SpawnPoint standalone = Point("Standalone", "Standalone");
        SpawnPoint inactive = Point("Inactive", "Inactive");
        inactive.gameObject.SetActive(false);
        Assert.That(executor.SpawnPointList.GetRegisteredSpawnPoints().Count, Is.EqualTo(3));
        Assert.That(executor.SpawnPointList.GetAvailableSpawnPoints().Count, Is.EqualTo(2));

        SpawnPoint clone = Object.Instantiate(first);
        clone.transform.SetParent(null);
        SceneManager.MoveGameObjectToScene(clone.gameObject, _scene);
        Assert.That(executor.SpawnPointList.GetRegisteredSpawnPoints().Count, Is.EqualTo(4));
        Assert.That(clone.SpawnPointId, Is.Not.EqualTo(first.SpawnPointId));
        inactive.gameObject.SetActive(true);
        Assert.That(executor.SpawnPointList.GetAvailableSpawnPoints().Count, Is.EqualTo(4));
        Object.DestroyImmediate(standalone.gameObject);
        Assert.That(executor.SpawnPointList.GetRegisteredSpawnPoints().Count, Is.EqualTo(3));

        var otherOwnerObject = new GameObject("Other Executor");
        SceneManager.MoveGameObjectToScene(otherOwnerObject, _scene);
        otherOwnerObject.AddComponent<EnemySpawnExecutor>();
        SpawnPoint owned = Point("Other owned", "OtherOwned");
        owned.transform.SetParent(otherOwnerObject.transform);
        Assert.That(executor.SpawnPointList.GetRegisteredSpawnPoints(), Has.Member(owned));

        Scene otherScene = EditorSceneManager.NewPreviewScene();
        try
        {
            SceneManager.MoveGameObjectToScene(clone.gameObject, otherScene);
            Assert.That(executor.SpawnPointList.GetRegisteredSpawnPoints(), Has.No.Member(clone));
        }
        finally { EditorSceneManager.ClosePreviewScene(otherScene); }
    }

    [Test]
    public void Authoring_NormalizesCopiedIds_PreservesOriginalAndSerializesNewId()
    {
        SpawnPoint original = Point("Original", "EncounterRight");
        SpawnPointAuthoringIds.NormalizeScene(_scene);
        SpawnPoint clone = Object.Instantiate(original);
        Assert.That(SpawnPointAuthoringIds.NormalizeScene(_scene), Is.EqualTo(1));
        Assert.That(original.SpawnPointId, Is.EqualTo("EncounterRight"));
        Assert.That(clone.SpawnPointId, Is.Not.EqualTo(original.SpawnPointId));
        Assert.That(new SerializedObject(clone).FindProperty("spawnPointId").stringValue,
            Is.EqualTo(clone.SpawnPointId));
        Assert.That(SpawnPointAuthoringIds.NormalizeScene(_scene), Is.Zero);
    }

    [Test]
    public void AuthoringEdit_OverridesTemporaryRegistrationId()
    {
        SpawnPoint original = Point("Original", "Repeated");
        SpawnPoint clone = Object.Instantiate(original);
        var registry = new SpawnPointRegistry();
        registry.Initialize(new[] { original, clone });
        SetId(clone, "AuthoredClone");
        Assert.That(clone.SpawnPointId, Is.EqualTo("AuthoredClone"));
        registry.Initialize(new[] { original, clone });
        Assert.That(registry.TryGetSpawnPoint("AuthoredClone", out _), Is.True);
    }

    private SpawnPoint Point(string name, string id)
    {
        var pointObject = new GameObject(name);
        SceneManager.MoveGameObjectToScene(pointObject, _scene);
        SpawnPoint point = pointObject.AddComponent<SpawnPoint>();
        SetId(point, id);
        return point;
    }

    private static void SetId(SpawnPoint point, string id)
    {
        var serialized = new SerializedObject(point);
        serialized.FindProperty("spawnPointId").stringValue = id;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
