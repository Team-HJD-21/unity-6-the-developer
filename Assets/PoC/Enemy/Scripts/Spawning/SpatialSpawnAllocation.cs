// 공간 정책이 선택한 지점과 그 지점에 배정할 분대 세트 수입니다.
public readonly struct SpatialSpawnAllocation
{
    public string SpawnPointId { get; }
    public int Sets { get; }

    public SpatialSpawnAllocation(string spawnPointId, int sets)
    {
        SpawnPointId = spawnPointId;
        Sets = sets;
    }
}
