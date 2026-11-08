/// <summary>선택한 타깃 주변의 현재 상황과 출현 셀의 현재·예정 병력을 나타낸다.</summary>
public readonly struct SpawnAreaContext
{
    public int CellEnemyCount { get; }
    public int NearbyEnemyCount { get; }
    public int TotalEnemyCount { get; }
    /// <summary>현재 전체 등록 몬스터 중 주변 몬스터의 비율(%).</summary>
    public double NearbyEnemyPercentage =>
        TotalEnemyCount <= 0 ? 0d : (double)NearbyEnemyCount / TotalEnemyCount * 100d;
    public bool HasNearbyPlayer { get; }
    public TargetAttackType AttackType { get; }

    public SpawnAreaContext(int cellEnemyCount, int nearbyEnemyCount, int totalEnemyCount, bool hasNearbyPlayer,
        TargetAttackType attackType = TargetAttackType.Unknown)
    {
        CellEnemyCount = cellEnemyCount;
        NearbyEnemyCount = nearbyEnemyCount;
        TotalEnemyCount = totalEnemyCount;
        HasNearbyPlayer = hasNearbyPlayer;
        AttackType = attackType;
    }
}
