using System.Collections;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

/// <summary>실제 Host 스폰 경로에서 신규 몬스터의 중재와 복구를 확인하는 명시적 테스트입니다.</summary>
public sealed class PlayerFieldNetworkIntegrationTests
{
    private SceneSetup[] _sceneSetup;
    private GameObject _beacon;
    [SetUp]
    public void RememberScenes() => _sceneSetup = EditorSceneManager.GetSceneManagerSetup();
    [UnityTearDown]
    public IEnumerator RestoreScenes()
    {
        if (_beacon != null) Object.DestroyImmediate(_beacon);
        if (Application.isPlaying) yield return new ExitPlayMode();
        EditorSceneManager.RestoreSceneManagerSetup(_sceneSetup);
    }
    [UnityTest]
    [Explicit("Initial_Stage의 NGO Host를 실제로 실행하여 서버 적 연동을 확인할 때 실행")]
    public IEnumerator ServerEnemy_AttacksBeaconAndResumesOriginalAI()
    {
        EditorSceneManager.OpenScene("Assets/PoC/Spaceship/Scenes/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();
        EnemyNetworkController driver = null;
        float deadline = Time.realtimeSinceStartup + 20;
        while (driver == null && Time.realtimeSinceStartup < deadline)
        {
            foreach (var candidate in Object.FindObjectsByType<EnemyNetworkController>(FindObjectsSortMode.None))
            {
                var health = candidate.GetComponent<EnemyHealth>();
                if (candidate.IsSpawned && candidate.IsServer && candidate.enabled && health != null && health.CurrentHealth > 0)
                { driver = candidate; break; }
            }
            yield return null;
        }
        Assert.That(driver, Is.Not.Null, "실제 서버 적의 스폰이 필요합니다.");
        driver.transform.position = new Vector3(14000, 14000, 0);
        _beacon = new GameObject("Server beacon test", typeof(PlayerFieldDevice));
        var device = _beacon.GetComponent<PlayerFieldDevice>();
        Vector3 point = driver.transform.position + Vector3.right * .1f;
        device.Initialize(PlayerFieldDeviceKind.Beacon, point, point, .3f, 80, 8, 12);
        driver.gameObject.AddComponent<PlayerFieldEnemyAdapter>();
        yield return null;
        yield return null;
        Assert.That(driver.enabled, Is.False, "장비 중재 중에만 기존 AI 실행을 멈춥니다.");
        Assert.That(device.Health, Is.LessThan(80), "서버 적이 비콘을 공격해야 합니다.");
        _beacon.SetActive(false);
        yield return null;
        Assert.That(driver.enabled, Is.True, "장비가 사라지면 기존 AI를 복구합니다.");
    }
}
