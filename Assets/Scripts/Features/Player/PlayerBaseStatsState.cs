using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public enum PlayerBaseStat { Attack, Movement, CriticalChance }

/// <summary>달성 레벨의 중복 수령 방지와 투자/회수만 담당합니다. 경험치·지급 권한·전투 계산은 호출자가 제공합니다.</summary>
public sealed class PlayerBaseStatsState
{
    private readonly HashSet<int> _claimed = new();
    private readonly int[] _invested = new int[3];
    public int MaximumLevel { get; }
    public int CriticalPointLimit { get; }
    public int PointsPerReward { get; }
    public int EarnedPoints => _claimed.Count * PointsPerReward;
    public int AvailablePoints => EarnedPoints - _invested.Sum();

    public PlayerBaseStatsState(int maximumLevel, int criticalPointLimit, int pointsPerReward = 1)
    {
        if (maximumLevel < 2 || maximumLevel > 1000 || criticalPointLimit < 0 || criticalPointLimit > 1000 ||
            pointsPerReward < 1 || pointsPerReward > 100) throw new ArgumentOutOfRangeException(nameof(maximumLevel));
        MaximumLevel = maximumLevel; CriticalPointLimit = criticalPointLimit; PointsPerReward = pointsPerReward;
    }
    public int Invested(PlayerBaseStat stat) => IsStat(stat) ? _invested[(int)stat] : 0;
    public bool IsClaimed(int level) => _claimed.Contains(level);
    public bool CanClaim(int level, int achievedLevel) => level >= 2 && level <= MaximumLevel &&
        level <= achievedLevel && !_claimed.Contains(level);
    public bool TryClaim(int level, int achievedLevel)
    {
        if (!CanClaim(level, achievedLevel)) return false;
        return _claimed.Add(level);
    }
    public bool CanInvest(PlayerBaseStat stat) => IsStat(stat) && AvailablePoints > 0 &&
        (stat != PlayerBaseStat.CriticalChance || Invested(stat) < CriticalPointLimit);
    public bool TryInvest(PlayerBaseStat stat)
    {
        if (!CanInvest(stat)) return false;
        _invested[(int)stat]++; return true;
    }
    public bool TryRefund(PlayerBaseStat stat)
    {
        if (!IsStat(stat) || Invested(stat) <= 0) return false;
        _invested[(int)stat]--; return true;
    }
    public bool ResetInvestments()
    {
        if (_invested.All(value => value == 0)) return false;
        Array.Clear(_invested, 0, _invested.Length); return true;
    }
    public Snapshot Capture() => new(_invested[0], _invested[1], _invested[2], _claimed.OrderBy(value => value).ToArray());
    public void Restore(Snapshot snapshot)
    {
        if (snapshot == null || snapshot.ClaimedLevels == null || snapshot.Attack < 0 || snapshot.Movement < 0 ||
            snapshot.Critical < 0 || snapshot.Critical > CriticalPointLimit || snapshot.ClaimedLevels.Length >= MaximumLevel ||
            snapshot.ClaimedLevels.Any(level => level < 2 || level > MaximumLevel) ||
            snapshot.ClaimedLevels.Distinct().Count() != snapshot.ClaimedLevels.Length ||
            (long)snapshot.Attack + snapshot.Movement + snapshot.Critical > (long)snapshot.ClaimedLevels.Length * PointsPerReward)
            throw new InvalidDataException("Invalid Player base stats allocation or level rewards.");
        _claimed.Clear();
        foreach (int level in snapshot.ClaimedLevels) _claimed.Add(level);
        _invested[0] = snapshot.Attack; _invested[1] = snapshot.Movement; _invested[2] = snapshot.Critical;
    }
    private static bool IsStat(PlayerBaseStat stat) => stat >= PlayerBaseStat.Attack && stat <= PlayerBaseStat.CriticalChance;
    public sealed class Snapshot
    {
        public readonly int Attack, Movement, Critical;
        public readonly int[] ClaimedLevels;
        public Snapshot(int attack, int movement, int critical, int[] claimedLevels)
        { Attack = attack; Movement = movement; Critical = critical; ClaimedLevels = claimedLevels; }
    }
}
