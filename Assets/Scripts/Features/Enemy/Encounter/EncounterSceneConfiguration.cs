// 제작자가 지정한 실제 생성기와 환경 정책을 현재 Scene의 실행 구성으로 전달합니다.
// App의 consumer가 유지돼도 Scene 재로드 뒤에는 새 제작 참조를 사용합니다.
using System;
using TeamHJD.Game.Contracts;
using UnityEngine;
/// <summary>
/// 현재 씬의 생성 실행기와 환경 설정을 Encounter 연결부에 전달한다.
/// </summary>
public sealed class EncounterSceneConfiguration : MonoBehaviour, IEncounterSceneConfiguration
{
    [SerializeField] private EnemySpawnExecutor spawnExecutor;
    [SerializeField] private EncounterEnvironmentProfile environmentProfile;
    public EnemySpawnExecutor SpawnExecutor => spawnExecutor;
    /// <summary>
    /// 현재 씬의 제작 참조를 이를 소비할 수 있는 Encounter 연결부에 전달한다.
    /// </summary>
    public void ConfigureEncounter(IEncounterMatchConsumer consumer)
    {
        if (spawnExecutor == null || !spawnExecutor.isActiveAndEnabled || spawnExecutor.gameObject.scene != gameObject.scene)
            throw new InvalidOperationException("Assign one active Enemy executor owned by this Scene.");
        if (!(consumer is EnemyNetworkTestLauncher launcher))
            throw new ArgumentException("The current Enemy consumer does not support this configuration.", nameof(consumer));
        launcher.ConfigureScene(spawnExecutor, environmentProfile);
    }
}
