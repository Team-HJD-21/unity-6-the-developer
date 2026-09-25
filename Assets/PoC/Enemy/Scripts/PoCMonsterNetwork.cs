using Unity.Netcode;
using UnityEngine;

/// <summary>
/// NGO에서 몬스터 AI 실행 권한을 서버로 제한하는 진입점이다.
/// 네트워크에 스폰된 서버 인스턴스만 <see cref="PoCAIBrain"/>을 갱신한다.
/// </summary>
[RequireComponent(typeof(PoCAIBrain))]
public class PoCMonsterNetwork : NetworkBehaviour
{
    private PoCAIBrain _monsterAiBrain;

    /// <summary>
    /// 서버가 실행할 AI Brain 컴포넌트를 초기화한다.
    /// </summary>
    private void Awake()
    {
        _monsterAiBrain = GetComponent<PoCAIBrain>();
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
}
