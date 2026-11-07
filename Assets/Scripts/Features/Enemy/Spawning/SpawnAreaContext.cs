/// <summary>선택한 타깃 주변의 현재 상황과 출현 셀의 현재·예정 병력을 나타낸다.</summary>
public readonly struct SpawnAreaContext
{
    public int CellEnemyCount { get; }
    public int NearbyEnemyCount { get; }
    public int NearbyTurretCount { get; }
    public bool HasFrontline { get; }
    public double FrontlineDistance { get; }

    public SpawnAreaContext(int cellEnemyCount, int nearbyEnemyCount, int nearbyTurretCount,
        bool hasFrontline, double frontlineDistance)
    {
        CellEnemyCount = cellEnemyCount;
        NearbyEnemyCount = nearbyEnemyCount;
        NearbyTurretCount = nearbyTurretCount;
        HasFrontline = hasFrontline;
        FrontlineDistance = frontlineDistance;
    }
}
