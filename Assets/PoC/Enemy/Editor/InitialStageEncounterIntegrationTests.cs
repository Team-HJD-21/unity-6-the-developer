// Initial_Stage에서 디버그 버튼 없이 첫 Encounter가 생성되는지 확인합니다.
// NGO Host와 전체 Scene을 사용하는 명시적 실행 전용 통합 테스트입니다.

using System.Collections;
using NUnit.Framework;
using Unity.Netcode;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public sealed class InitialStageEncounterIntegrationTests
{
    [UnityTest]
    [Explicit("Stage 1 통합 Scene과 NGO Host가 필요한 경우에만 실행")]
    public IEnumerator InitialStage_StartsEncounterWithoutDebugRequest()
    {
        EditorSceneManager.OpenScene("Assets/PoC/Spaceship/Scenes/Initial_Stage.unity", OpenSceneMode.Single);
        yield return new EnterPlayMode();

        EnemyNetworkTestLauncher launcher = Object.FindAnyObjectByType<EnemyNetworkTestLauncher>();
        Assert.That(launcher, Is.Not.Null, "Initial_Stage의 EnemyNetworkTestLauncher가 로드되어야 합니다.");

        float deadline = Time.realtimeSinceStartup + 20f;
        int spawnedEnemies = 0;
        bool serverStarted = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            EnemySpawnExecutor executor = Object.FindAnyObjectByType<EnemySpawnExecutor>();
            spawnedEnemies = executor != null ? executor.SpawnedEnemyCount : 0;
            serverStarted = NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;
            if (spawnedEnemies > 0) break;
            yield return null;
        }

        yield return new ExitPlayMode();
        Assert.That(serverStarted, Is.True, "Match 시작 뒤 NGO Host가 자동으로 실행되어야 합니다.");
        Assert.That(spawnedEnemies, Is.GreaterThan(0), "디버그 버튼 없이 첫 분대가 생성되어야 합니다.");
    }
}
