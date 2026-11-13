using System;
using UnityEngine;

/// <summary>Match의 전체 스폰 예산과 정적 모드 증가량 설정.</summary>
[Serializable]
public sealed class EncounterBudgetSettings
{
    [Min(1)] public int baseBudget = 20;
    [Min(1)] public int maximumBudget = 100;
    [Min(1)] public int staticIncreasePerRequest = 5;
    public EncounterAdditionalSpawnSettings additionalSpawns = new EncounterAdditionalSpawnSettings();
}
