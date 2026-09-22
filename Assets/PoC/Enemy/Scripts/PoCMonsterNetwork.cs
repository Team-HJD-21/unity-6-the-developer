using Unity.Netcode;
using Unity.Netcode.Components;
using UnityEngine;

public class PoCMonsterNetwork : NetworkBehaviour
{
    [SerializeField] private PoCMonster monster;
    [SerializeField] private NetworkAnimator networkAnimator;

    public override void OnNetworkSpawn()
    {
        if (!IsServer)
            return;

        monster.AttackStarted += HandleAttackStarted;
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
            return;

        monster.AttackStarted -= HandleAttackStarted;
    }
    
    void Update()
    {
        if (!IsSpawned)
            return;

        if (IsServer)
            monster.Tick();
    }

    private void HandleAttackStarted()
    {
        networkAnimator.Animator.SetTrigger("Attack");
    }
}
