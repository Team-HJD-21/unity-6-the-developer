using Unity.Netcode;
using UnityEngine;

public class PoCMonsterSpawner : NetworkBehaviour
{
    [SerializeField] private GameObject monsterPrefab;
    [SerializeField] private Transform spawnPoint;
    [SerializeField] private Transform tempTarget;

    public void Spawn()
    {
        if (!IsServer)
            return;

        GameObject monster = Instantiate(
            monsterPrefab,
            spawnPoint.position,
            spawnPoint.rotation
            );
        
        
        monster.GetComponent<NetworkObject>().Spawn();
    }
}
