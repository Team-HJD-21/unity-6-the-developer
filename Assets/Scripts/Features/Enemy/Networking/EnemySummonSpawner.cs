using System;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 소환형 몬스터가 요청한 적을 생성하고 타깃을 전달한다.
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public sealed class EnemySummonSpawner : NetworkBehaviour, IRandomConsumer
{
    [Header("소환 위치")]
    [SerializeField, Min(0f)] private float spawnRadius = 1f;

    private IRandomSource _random;

    public IDisposable BindRandom(IRandomStreams random)
    {
        return RandomBinding.Bind(
            random,
            "enemy/summon-spawn",
            ref _random);
    }

    /// <summary>생성 주체로부터 코어가 소유한 난수 참조를 전달받는다.</summary>
    public void InitializeRandom(IRandomSource random)
    {
        if (random == null)
            throw new ArgumentNullException(nameof(random));
        if (!random.IsAvailable)
            throw new InvalidOperationException("사용할 수 없는 코어 난수입니다.");
        if (_random != null && _random.IsAvailable)
            throw new InvalidOperationException("소환 난수가 이미 연결되어 있습니다.");

        // 수명과 해제는 이 참조를 전달한 코어 바인딩이 관리한다.
        _random = random;
    }

    public override void OnNetworkDespawn()
    {
        _random = null;
        base.OnNetworkDespawn();
    }

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

        if (_random == null || !_random.IsAvailable)
        {
            Debug.LogError("소환에 사용할 코어 난수가 연결되지 않았습니다.", this);
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

        for (int i = 0; i < count; i++)
        {
            var point = _random.InsideUnitCircle();

            Vector3 position = transform.position + new Vector3(
                (float)point.X * spawnRadius,
                (float)point.Y * spawnRadius,
                0f);

            GameObject enemy = Instantiate(
                prefab,
                position,
                Quaternion.identity);

            if (enemy.TryGetComponent<EnemySummonSpawner>(out var summonSpawner))
                summonSpawner.InitializeRandom(_random);

            NetworkObject networkObject = enemy.GetComponent<NetworkObject>();
            EnemyController controller = enemy.GetComponent<EnemyController>();

            networkObject.Spawn();
            controller.AssignTarget(target);
        }

        return count;
    }
}
