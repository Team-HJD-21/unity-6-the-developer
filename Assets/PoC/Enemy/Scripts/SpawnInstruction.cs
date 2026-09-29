using UnityEngine;

public readonly struct SpawnInstruction
{
    public string EnemyId { get; }
    public int SpawnPointId { get; }
    public int Count { get; }

    public SpawnInstruction(
        string enemyId,
        int spawnPointId,
        int count)
    {
        EnemyId = enemyId;
        SpawnPointId = spawnPointId;
        Count = count;
    }
}
