// 프로젝트 이관 후 실제 Scene·Player·터렛·Resources 연결을 검증합니다.
// Play Mode에서는 자동/디버그 스폰, 공간 정보, Scene 재진입과 정리를 확인합니다.
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class ProjectAssetMigrationTests
{
    private const string InitialStage = "Assets/Scenes/Gameplay/Initial_Stage.unity";

    [Test]
    public void CurrentPlayer_UsesAuthoredHealthWithoutLegacyManager()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(prefab, Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerInfo>().maxHp, Is.GreaterThan(0));
        var player = new GameObject("Migration health probe");
        try
        {
            PlayerInfo health = player.AddComponent<PlayerInfo>();
            // Edit Mode에서 SendMessage는 Unity의 실행 상태 assertion을 발생시킵니다.
            typeof(PlayerInfo).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(health, null);
            Assert.That(health.maxHp, Is.EqualTo(400));
            Assert.That(health.curHp, Is.EqualTo(400));
        }
        finally { Object.DestroyImmediate(player); }
    }

    [Test]
    public void CatalogTurrets_UseCurrentPrefabGuids()
    {
        string[] current = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs/Turrets" });
        var guids = new System.Collections.Generic.HashSet<string>(current);
        var upgrades = AssetDatabase.LoadAssetAtPath<TurretLevelUpgradeCatalog>("Assets/Data/Turrets/TurretLevelUpgrade_Stage1.asset");
        Assert.That(upgrades, Is.Not.Null);
        string[] dependencies = AssetDatabase.GetDependencies(AssetDatabase.GetAssetPath(upgrades), true);
        Assert.That(dependencies.Where(path => path.EndsWith(".prefab")).All(path => guids.Contains(AssetDatabase.AssetPathToGUID(path))), Is.True);
        Assert.That(AssetDatabase.IsValidFolder("Assets/Prefabs/Tower"), Is.False);
        Assert.That(Resources.Load<EnemySquadPresetCatalog>("Spawning/EnemySquadPresetCatalog"), Is.Not.Null);
    }

    [Test]
    public void Build_UsesCurrentGameplayAndSpaceshipOnly()
    {
        string[] enabled = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
        Assert.That(enabled, Is.EqualTo(new[] { InitialStage, "Assets/Scenes/Spaceship/SpaceShipGrayBox.unity" }));
    }

    [TestCase("Assets/Scenes/Gameplay/Initial_Stage.unity")]
    [TestCase("Assets/Scenes/Spaceship/SpaceShipGrayBox.unity")]
    [TestCase("Assets/Scenes/Tests/EnemySandbox.unity")]
    public void CurrentScenes_DoNotContainMissingScripts(string path)
    {
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        try
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            foreach (Transform child in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)))
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject), Is.Zero, path + " / " + child.name);
        }
        finally
        {
            if (setup.Any(scene => scene.isLoaded) && setup.Count(scene => scene.isActive) == 1)
                EditorSceneManager.RestoreSceneManagerSetup(setup);
            else
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }

    [UnityTest]
    public IEnumerator InitialStage_RequestsSpawnsAndRestartsWithoutLegacyRuntime()
    {
        EditorSceneManager.OpenScene(InitialStage, OpenSceneMode.Single);
        yield return new EnterPlayMode();
        AppRoot root = Object.FindAnyObjectByType<AppRoot>();
        Assert.That(root, Is.Not.Null);
        Assert.That(Object.FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None).Any(component =>
            new[] { "Global", "GeneralManager", "GameManager", "AudioManager", "DataManager" }.Contains(component.GetType().Name)), Is.False);
        Assert.That(Object.FindAnyObjectByType<PlayerInfo>().curHp, Is.GreaterThan(0));
        Assert.That(Object.FindObjectsByType<TurretBase>(FindObjectsSortMode.None), Is.Not.Empty);
        var firstMatch = root.CurrentMatchId;
        var snapshot = root.CurrentBattlefieldSnapshot;
        Assert.That(snapshot.Triangles, Is.Not.Empty);
        Assert.That(snapshot.FrontlineEdges, Is.Not.Empty);
        Assert.That(snapshot.Grid.OccupiedCells, Is.Not.Empty);
        var launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        Assert.That(launcher, Is.Not.Null);
        float deadline = Time.realtimeSinceStartup + 20;
        while (launcher.ActiveEnemyCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(launcher.ActiveEnemyCount, Is.GreaterThan(0), "Match 시작의 자동 Encounter가 스폰해야 합니다.");
        Assert.That(launcher.TryRequestCurrentEncounter("Normal", 10, EncounterRequestSource.DebugWindow, out string message), Is.True, message);
        Assert.That(launcher.RecentDiagnostics.Any(item => item.SpawnDecision != null && item.SpawnDecision.Actuals.Count > 0), Is.True);

        yield return SceneManager.LoadSceneAsync("SpaceShipGrayBox", LoadSceneMode.Additive);
        Assert.That(root.CurrentMatchId, Is.EqualTo(firstMatch), "우주선 Additive 로드가 기존 Match를 교체하면 안 됩니다.");
        yield return SceneManager.UnloadSceneAsync("SpaceShipGrayBox");
        Assert.That(root.CurrentMatchId, Is.EqualTo(firstMatch));

        yield return SceneManager.LoadSceneAsync("Initial_Stage", LoadSceneMode.Single);
        yield return null;
        Assert.That(Object.FindObjectsByType<AppRoot>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(root.CurrentMatchId, Is.Not.EqualTo(firstMatch));
        Assert.That(Object.FindObjectsByType<PlayerInfo>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Assert.That(Object.FindObjectsByType<Unity.Netcode.NetworkManager>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        deadline = Time.realtimeSinceStartup + 20;
        while (launcher.ActiveEnemyCount == 0 && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(launcher.ActiveEnemyCount, Is.GreaterThan(0), "Scene 재진입 후에도 첫 자동 스폰이 동작해야 합니다.");
        // Scene 소유 actor/Encounter를 먼저 해제한 뒤 App을 파괴하는 실제 종료 순서를 확인합니다.
        Scene teardown = SceneManager.CreateScene("MigrationTeardown");
        SceneManager.SetActiveScene(teardown);
        yield return SceneManager.UnloadSceneAsync("Initial_Stage");
        Assert.That(root.CurrentMatchId.IsEmpty, Is.True);
        Object.Destroy(root.gameObject);
        // Destroy는 프레임 끝에 처리되므로 Editor 테스트 Update 한 번만 기다리지 않습니다.
        deadline = Time.realtimeSinceStartup + 5;
        while (root != null && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(root == null, Is.True, "AppRoot의 실제 Unity 오브젝트 파괴를 확인해야 합니다.");
        Assert.That((bool)typeof(AppRoot).GetField("_isDisposed", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(root), Is.True);
        yield return new ExitPlayMode();
    }
}
