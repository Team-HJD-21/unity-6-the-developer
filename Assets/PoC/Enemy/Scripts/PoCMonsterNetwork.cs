using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 서버에서 몬스터 AI를 실행한다.
/// </summary>
public class PoCMonsterNetwork : NetworkBehaviour
{
    [Header("참조")]
    [SerializeField] private PoCMonster monster;

    /// <summary>
    /// 서버에서만 몬스터 AI를 갱신한다.
    /// </summary>
    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        monster.Tick();
    }
}
