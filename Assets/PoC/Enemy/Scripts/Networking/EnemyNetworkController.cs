using System;
using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NGO에서 몬스터 AI 실행 권한을 서버로 제한하는 진입점이다.
/// 네트워크에 스폰된 서버 인스턴스만 <see cref="EnemyAIBrain"/>을 갱신한다.
/// </summary>
[RequireComponent(typeof(EnemyAIBrain))]
public class EnemyNetworkController : NetworkBehaviour
{
    private EnemyAIBrain _monsterAiBrain;

    /// <summary>
    /// 이 적이 네트워크에서 제거될 때 생성 주체에게 알린다.
    /// 서버의 Executor가 구독하며, Enemy는 Executor를 직접 참조하지 않는다.
    /// </summary>
    public event Action<ulong> Despawned;

    /// <summary>
    /// 서버가 실행할 AI Brain 컴포넌트를 초기화한다.
    /// </summary>
    private void Awake()
    {
        _monsterAiBrain = GetComponent<EnemyAIBrain>();
    }

    /// <summary>
    /// 서버에서만 몬스터 AI를 갱신한다.
    /// </summary>
    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        _monsterAiBrain.UpdateAI();
    }

    /// <summary>
    /// 사망뿐 아니라 다른 원인으로 Despawn되어도 분대에서 해제할 수 있도록 통지한다.
    /// </summary>
    public override void OnNetworkDespawn()
    {
        Despawned?.Invoke(NetworkObjectId);
        base.OnNetworkDespawn();
    }
}
