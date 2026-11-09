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

        GameObject prefab = definition.Prefab;

        if (!prefab.TryGetComponent<NetworkObject>(out _) ||
            !prefab.TryGetComponent<EnemyController>(out _) ||
            !prefab.TryGetComponent<EnemyNetworkController>(out _))
        {
            Debug.LogError(
                $"소환 프리팹의 필수 컴포넌트가 없습니다: {definition.EnemyId}",
                this);
            return 0;
        }

        EnemySquad sharedSquad = definition.SquadMode == EnemySquadMode.Shared
            ? new EnemySquad(definition.SquadMode, definition.TargetRule, target)
            : null;
        int spawnedCount = 0;

        for (int i = 0; i < count; i++)
        {
            Vector3 position = GetSpawnPosition(target, i, count);

            GameObject enemy = Instantiate(
                prefab,
                position,
                Quaternion.identity);

            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            EnemyController controller = enemy.GetComponent<EnemyController>();

            networkObject.Spawn();
            EnemySquad squad = sharedSquad ??
                new EnemySquad(definition.SquadMode, definition.TargetRule, target);

            if (!controller.JoinSquad(squad))
            {
                networkObject.Despawn();
                continue;
            }

            spawnedCount++;
        }

        return spawnedCount;
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
