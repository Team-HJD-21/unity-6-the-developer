using Unity.Netcode;
using UnityEngine;

/// <summary>
/// 네트워크에서 몬스터 AI가 서버에서만 실행되도록 제어한다.
/// 위치와 애니메이션 결과는 NetworkTransform과 NetworkAnimator가 동기화한다.
/// </summary>
public class PoCMonsterNetwork : NetworkBehaviour
{
    [SerializeField] private PoCMonster monster;

    private void Update()
    {
        if (!IsSpawned || !IsServer)
            return;

        monster.Tick();
    }
}
