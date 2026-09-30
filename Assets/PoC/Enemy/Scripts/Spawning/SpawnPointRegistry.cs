using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 씬에 배치된 스폰 지점 목록을 보관하고 고유 식별 값으로 조회할 수 있게 제공한다.
/// </summary>
public class SpawnPointRegistry : MonoBehaviour
{
    [SerializeField] private List<SpawnPoint> spawnPoints = new();

    /// <summary>
    /// 고유 식별 값과 일치하는 스폰 지점을 조회한다.
    /// </summary>
    /// <param name="spawnPointId">조회할 스폰 지점의 고유 식별 값.</param>
    /// <param name="spawnPoint">조회에 성공한 스폰 지점.</param>
    /// <returns>일치하는 스폰 지점이 있으면 <see langword="true"/>, 없으면 <see langword="false"/>.</returns>
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
