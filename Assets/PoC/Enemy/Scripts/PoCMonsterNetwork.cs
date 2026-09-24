using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버에서 몬스터 AI를 실행한다.
/// </summary>
[RequireComponent(typeof(PoCAIBrain))]
public class PoCMonsterNetwork : NetworkBehaviour
{
    private PoCAIBrain _monsterAiBrain;

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

        _monsterAiBrain.Tick();
    }
}