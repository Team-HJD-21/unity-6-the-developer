using System.Text;
using TeamHJD.Game.Domain;
using UnityEngine;

/// <summary>Host에서 Region별 스폰 허용 상태를 조작하고 확인하는 임시 테스터다.</summary>
[DisallowMultipleComponent]
[AddComponentMenu("The Developer/Enemy/Testing/Enemy Spawn Region Tester")]
public sealed class EnemySpawnRegionTester : MonoBehaviour
{
    [Header("임시 Region 스폰 테스트 — 검증 후 제거 가능")]
    [SerializeField] private EnemySpawnExecutor spawnExecutor;
    [SerializeField, Min(1)] private int regionId = 1;

    private void Reset()
    {
        spawnExecutor = GetComponent<EnemySpawnExecutor>();
    }

    private void Awake()
    {
        if (spawnExecutor == null)
            spawnExecutor = GetComponent<EnemySpawnExecutor>();
    }

    [ContextMenu("Region 테스트/전체 그룹 상태 출력")]
    public void PrintGroups()
    {
        if (!TryGetRegistry(out SpawnPointRegistry registry))
            return;

        var groups = registry.GetRegionGroups();
        if (groups.Count == 0)
        {
            Debug.LogWarning("Region 그룹이 없습니다. Static 모드의 Match에서 첫 스폰 요청 후 확인하세요. " +
                "스폰 지점이 Region Grid 안에 있고 Region ID가 0이 아닌지도 확인하세요.", this);
            return;
        }

        foreach (SpawnPointRegistry.RegionGroup group in groups)
        {
            int available = 0;
            int count = 0;
            var points = new StringBuilder();
            foreach (SpawnPoint point in group.SpawnPoints)
            {
                if (point == null)
                    continue;

                count++;
                if (point.CanSpawnEnemies)
                    available++;

                if (points.Length > 0)
                    points.Append(" | ");
                points.Append(point.SpawnPointId).Append(": ")
                    .Append(point.CanSpawnEnemies ? "가능" : "불가");
            }

            Debug.Log($"[EnemySpawnRegionTester] Region {group.RegionId}: " +
                $"지점 {count}, 스폰 가능 {available}. {points}", this);
        }
    }

    [ContextMenu("Region 테스트/선택 Region 스폰 끄기")]
    public void DisableRegion() => SetRegionEnabled(false);

    [ContextMenu("Region 테스트/선택 Region 스폰 켜기")]
    public void EnableRegion() => SetRegionEnabled(true);

    private void SetRegionEnabled(bool enabled)
    {
        if (!TryGetRegistry(out SpawnPointRegistry registry))
            return;

        if (!registry.SetRegionSpawnEnabled(regionId, enabled))
        {
            Debug.LogWarning($"Region {regionId} 그룹을 찾지 못했습니다. 전체 그룹 상태를 먼저 출력하세요.", this);
            return;
        }

        Debug.Log($"[EnemySpawnRegionTester] Region {regionId} 스폰 " +
            (enabled ? "허용" : "차단") + ". 기존 몬스터는 유지됩니다.", this);
        PrintGroups();
    }

    private bool TryGetRegistry(out SpawnPointRegistry registry)
    {
        registry = null;
        if (!Application.isPlaying || spawnExecutor == null || !spawnExecutor.CanSpawn)
        {
            Debug.LogWarning("Spawn Executor를 연결하고 Host/Server를 시작한 뒤 실행하세요.", this);
            return false;
        }

        registry = spawnExecutor.SpawnPointList;
        return true;
    }
}
