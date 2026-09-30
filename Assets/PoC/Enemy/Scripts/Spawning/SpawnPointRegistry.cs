using System.Collections.Generic;
using UnityEngine;

public class SpawnPointRegistry : MonoBehaviour
{
    [SerializeField] private List<SpawnPoint> spawnPoints = new();

    public bool TryGetSpawnPoint(string spawnPointId, out SpawnPoint spawnPoint)
    {
        foreach(SpawnPoint sp in spawnPoints)
        {
            if (sp != null && sp.SpawnPointId == spawnPointId)
            {
                spawnPoint = sp;
                return true;
            }
        }

        spawnPoint = null;
        return false;
    }
}
