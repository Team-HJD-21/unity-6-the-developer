// 공간 정책에 전달하는 SpawnPoint 식별자와 XY 좌표입니다.
using TeamHJD.Game.Domain;

public readonly struct SpatialSpawnCandidate
{
    public string Id { get; }
    public BattlefieldPoint Position { get; }

    public SpatialSpawnCandidate(string id, BattlefieldPoint position)
    {
        Id = id;
        Position = position;
    }
}
