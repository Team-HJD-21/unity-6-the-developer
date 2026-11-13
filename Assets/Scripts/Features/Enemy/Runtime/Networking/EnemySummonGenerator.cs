// Enemy 스킬이 정한 소환 위치·수량·타깃을 유지하며 Match 공통 생성기에 요청합니다.
// 직접 Instantiate하지 않으므로 소환 적도 위치·사망·통계·저장·종료 정리에 포함됩니다.
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 소환형 몬스터가 요청한 적을 생성하고 타깃을 전달한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class EnemySummonGenerator : NetworkBehaviour
{
    [Header("소환 위치")]
    [FormerlySerializedAs("spawnRadius")]
    [SerializeField, Min(0f)] private float spawnDistance = 1f;
    [SerializeField, Min(0f)] private float spawnSpacing = 0.5f;

    /// <summary>
    /// 서버에서 소환 정의의 몬스터를 생성하고 분대 규칙에 맞게 등록한 실제 수를 반환한다.
    /// </summary>
    /// <param name="definition">생성 프리팹과 분대·집계 설정을 제공하는 정의.</param>
    /// <param name="count">요청한 소환 수.</param>
    /// <param name="target">생성 직후 분대가 공유할 초기 타깃.</param>
    /// <returns>네트워크 생성과 분대 등록에 성공한 수.</returns>
    public int Spawn(
        EnemyDefinition definition,
        int count,
        Transform target)
    {
        if (!IsSpawned || !IsServer ||
            definition == null ||
            definition.Prefab == null ||
            count <= 0)
        {
            return 0;
        }

        // 부모를 생성한 실행기가 소환도 등록합니다. 종류·수량·배치 의도는 이 스킬이 그대로 정합니다.
        var parent = GetComponent<EnemyNetworkController>();
        if (parent == null || NetworkManager?.SpawnManager == null ||
            !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(parent.SimulationOwnerId, out var owner) ||
            !owner.TryGetComponent<EnemySpawnExecutor>(out var executor))
        {
            Debug.LogWarning("소환 부모의 Match 생성기를 찾지 못했습니다. 소환은 실행하지 않습니다.", this);
            return 0;
        }
        var positions = new Vector3[count];
        for (int i = 0; i < count; i++) positions[i] = GetSpawnPosition(target, i, count);
        return executor.SpawnSummoned(parent, definition, positions, target);
    }

    /// <summary>타깃 방향의 정면에 생성하고 여러 마리는 좌우로 간격을 둔다.</summary>
    private Vector3 GetSpawnPosition(Transform target, int index, int count)
    {
        Vector2 direction = target != null
            ? (Vector2)target.position - (Vector2)transform.position
            : Vector2.down;

        if (direction.sqrMagnitude < 0.0001f)
            direction = Vector2.down;

        direction.Normalize();
        Vector2 side = new Vector2(-direction.y, direction.x);
        float sideOffset = (index - (count - 1) * 0.5f) * spawnSpacing;
        Vector2 offset = direction * spawnDistance + side * sideOffset;

        return transform.position + new Vector3(offset.x, offset.y, 0f);
    }
}
