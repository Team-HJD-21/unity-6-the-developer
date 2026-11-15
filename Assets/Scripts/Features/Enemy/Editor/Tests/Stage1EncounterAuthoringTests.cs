// 실제 Stage_1의 네트워크·스폰·날씨·전술 지도 제작 참조를 검사합니다.
// 씬을 저장하지 않는 Preview Scene을 사용하며 지형이나 프리팹을 변경하지 않습니다.
using System.Linq;
using Map;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap.SceneComposition;
using Unity.Netcode;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class Stage1EncounterAuthoringTests
{
    private Scene _scene;
    private MonoBehaviour[] _components;

    [OneTimeSetUp]
    public void OpenScene()
    {
        _scene = EditorSceneManager.OpenPreviewScene("Assets/Scenes/Gameplay/Stage_1.unity");
        _components = _scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
            .Where(component => component != null).ToArray();
    }

    [OneTimeTearDown]
    public void CloseScene() => EditorSceneManager.ClosePreviewScene(_scene);

    [Test]
    public void Runtime_HasOneExplicitExecutorAndSeparateNetworkOwner()
    {
        var manager = _components.OfType<NetworkManager>().Single();
        var encounter = _components.OfType<EncounterSceneBinding>().Single();
        var executor = _components.OfType<EnemySpawnExecutor>().Single();
        var network = _components.OfType<InitialStageNetworkBinding>().Single();
        var root = _components.OfType<InitialStageCompositionRoot>().Single();
        var config = _components.OfType<EncounterSceneConfiguration>().Single();
        Assert.That(config.SpawnExecutor, Is.SameAs(executor));
        Assert.That(Reference(network, "_manager"), Is.SameAs(manager));
        Assert.That(Reference(root, "_encounterConsumerBehaviour"), Is.SameAs(encounter));
        Assert.That(encounter.gameObject, Is.Not.SameAs(manager.gameObject));
    }

    [Test]
    public void SpawnPoints_PreserveUniqueIdsAndBelongToTheFormalRoot()
    {
        var executor = _components.OfType<EnemySpawnExecutor>().Single();
        var points = _components.OfType<SpawnPoint>().ToArray();
        Assert.That(points, Is.Not.Empty);
        Assert.That(points.All(point => point.transform.IsChildOf(executor.transform)), Is.True);
        Assert.That(points.All(point => !string.IsNullOrWhiteSpace(point.SpawnPointId)), Is.True);
        Assert.That(points.Select(point => point.SpawnPointId).Distinct().Count(), Is.EqualTo(points.Length));
        Assert.That(points.All(point => point.name == "Spawn_" + point.SpawnPointId), Is.True);
        // 지점을 추가해도 개수 제한 없이 발견되어야 하므로 현재 제작 개수를 고정하지 않습니다.
    }

    [Test]
    public void SpawnPoints_CoverTheLastAuthoredRegionBeforeItIsSecured()
    {
        var binding = _components.OfType<RegionSceneBinding>().Single();
        var definition = binding.CaptureInitialDefinition();
        int last = definition.RegionIds.Max();
        var available = _components.OfType<SpawnPoint>().Where(point => point.CanSpawnEnemies);
        Assert.That(available.Any(point => definition.TryGetCellIndex(
            new TeamHJD.Game.Domain.BattlefieldPoint(point.Position.x, point.Position.y), out int cell) &&
            definition.RegionIds[cell] == last), Is.True,
            "이전 구역을 모두 확보해도 마지막 구역에 정규 스폰 후보가 남아야 합니다.");
    }

    [Test]
    public void CameraEffects_KeepAnIntermediateColorTargetAcrossReentry()
    {
        var feature = ScriptableObject.CreateInstance<StageCameraEffectsFeature>();
        try
        {
            feature.Create();
            var pass = typeof(StageCameraEffectsFeature).GetField("_pass",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(feature);
            Assert.That(pass.GetType().GetProperty("requiresIntermediateTexture").GetValue(pass), Is.True,
                "Backbuffer 직접 렌더링으로 바뀌어도 날씨/낮밤 Blit이 생략되면 안 됩니다.");
        }
        finally { Object.DestroyImmediate(feature); }
    }

    [Test]
    public void Camera_HasTheDayNightConsumerAndItsRealSourceMaterial()
    {
        var dayNight = _components.OfType<DayNightShaderApply>().Single();
        var weather = _components.OfType<WeatherManager>().Single();
        Assert.That(dayNight.GetComponent<Camera>().CompareTag("MainCamera"), Is.True);
        Assert.That(Reference(dayNight, "_weatherManager"), Is.SameAs(weather));
        Assert.That(Reference(dayNight, "_dayNightMaterial"),
            Is.SameAs(AssetDatabase.LoadAssetAtPath<Material>("Assets/Shader/DayNight/DayNight.mat")));
    }

    [Test]
    public void Presentation_HasSpawnWarningAndExplicitMapScenery()
    {
        var config = _components.OfType<EncounterSceneConfiguration>().Single();
        var tactical = _components.OfType<TacticalCommandController>().Single();
        Assert.That(Reference(config, "additionalSpawnWarning"), Is.Not.Null);
        Assert.That(tactical.SceneryRenderers, Is.Not.Empty);
        Assert.That(tactical.SceneryRenderers.All(renderer => renderer != null && renderer.gameObject.scene == _scene), Is.True);
        // 옛 카메라 마커를 재생성하지 않고 기존 Grid bounds fallback을 사용합니다.
    }

    private static Object Reference(Object owner, string field)
        => new SerializedObject(owner).FindProperty(field).objectReferenceValue;
}
