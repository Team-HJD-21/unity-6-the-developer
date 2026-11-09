using System;

/// <summary>누적 경험치에서 레벨과 현재 레벨의 진행도를 계산합니다. 성장 효과는 적용하지 않습니다.</summary>
public sealed class PlayerExperienceState
{
    public int FirstLevelExperience { get; }
    public int ExperienceIncreasePerLevel { get; }
    public int MaximumLevel { get; }
    public long TotalExperience { get; private set; }
    public int Level { get; private set; } = 1;
    public bool IsMaximumLevel => Level == MaximumLevel;
    public long ExperienceInLevel => IsMaximumLevel ? 0 : TotalExperience - TotalRequiredForLevel(Level);
    public long RequiredExperience => IsMaximumLevel ? 0 : FirstLevelExperience + (long)(Level - 1) * ExperienceIncreasePerLevel;
    public float Progress => IsMaximumLevel ? 1f : (float)((double)ExperienceInLevel / RequiredExperience);

    public PlayerExperienceState(int firstLevelExperience = 100, int experienceIncreasePerLevel = 0, int maximumLevel = 100)
    {
        if (firstLevelExperience <= 0 || experienceIncreasePerLevel < 0 || maximumLevel < 2 || maximumLevel > 1000)
            throw new ArgumentOutOfRangeException(nameof(firstLevelExperience));
        FirstLevelExperience = firstLevelExperience;
        ExperienceIncreasePerLevel = experienceIncreasePerLevel;
        MaximumLevel = maximumLevel;
    }

    public long TotalRequiredForLevel(int level)
    {
        if (level < 1 || level > MaximumLevel) throw new ArgumentOutOfRangeException(nameof(level));
        long completed = level - 1;
        return completed * FirstLevelExperience + completed * (completed - 1) / 2 * ExperienceIncreasePerLevel;
    }

    public bool AddExperience(long amount)
    {
        if (amount <= 0 || IsMaximumLevel) return false;
        long remaining = TotalRequiredForLevel(MaximumLevel) - TotalExperience;
        RestoreTotalExperience(TotalExperience + Math.Min(remaining, amount));
        return true;
    }

    public void RestoreTotalExperience(long total)
    {
        if (total < 0 || total > TotalRequiredForLevel(MaximumLevel)) throw new ArgumentOutOfRangeException(nameof(total));
        TotalExperience = total;
        // 큰 보상도 필요한 누적 경험치로 탐색해 한 번에 여러 레벨을 올립니다.
        int low = 1, high = MaximumLevel;
        while (low < high)
        {
            int middle = low + (high - low + 1) / 2;
            if (TotalRequiredForLevel(middle) <= total) low = middle;
            else high = middle - 1;
        }
        Level = low;
    }
}
