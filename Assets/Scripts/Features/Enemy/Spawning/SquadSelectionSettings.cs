using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using UnityEngine;

/// <summary>지점 주변 조사 범위와 자동 편성 선택에 사용하는 조정값.</summary>
[Serializable]
public sealed class SquadSelectionSettings
{
    [Header("주변 조사")]
    [Min(0)] public int nearbyCellRadius = 1;

    [Header("상황 판단")]
    [InspectorName("병력 적음 기준 (%)")]
    [Tooltip("전체 등록 몬스터 중 주변 몬스터의 비율이 이 값 이하면 적음으로 판단합니다.")]
    [Range(0f, 100f)] public float sparseEnemyPercentage = 20f;

    [Header("선택과 배치")]
    [Min(1)] public int preferredMultiplier = 3;
    [Min(1)] public int maxEnemiesPerCell = 20;

    [Header("진단")]
    public bool logSelectionDecisions;

    public bool IsValid => nearbyCellRadius >= 0 && sparseEnemyPercentage >= 0f &&
        sparseEnemyPercentage <= 100f && preferredMultiplier > 0 && maxEnemiesPerCell > 0;
}

/// <summary>상황에 맞는 편성의 비중을 높이고 Core 난수로 하나를 선택한다.</summary>
public static class SquadSelectionPolicy
{
    public static double CalculateWeight(EnemySquadPreset preset, SpawnAreaContext area,
        SquadSelectionSettings settings)
    {
        if (preset == null || preset.SelectionWeight <= 0 || settings == null || !settings.IsValid)
            return 0d;

        bool sparse = area.NearbyEnemyPercentage <= settings.sparseEnemyPercentage;
        bool crowded = !sparse;
        bool preferred;
        switch (preset.Role)
        {
            case EnemySquadRole.Patrol:
                preferred = sparse && !area.HasNearbyPlayer && area.AttackType == TargetAttackType.None;
                break;
            case EnemySquadRole.Assault:
                preferred = sparse && area.AttackType == TargetAttackType.SingleTarget;
                break;
            case EnemySquadRole.Backline:
                preferred = sparse && area.AttackType == TargetAttackType.Area;
                break;
            case EnemySquadRole.Summon:
                preferred = sparse && area.AttackType == TargetAttackType.SingleTarget;
                break;
            case EnemySquadRole.Sustain:
                preferred = crowded;
                break;
            case EnemySquadRole.Elite:
                preferred = sparse && area.AttackType == TargetAttackType.Area;
                break;
            case EnemySquadRole.Balanced:
                preferred = false;
                break;
            case EnemySquadRole.Ambush:
                preferred = area.HasNearbyPlayer;
                break;
            default:
                return 0d;
        }

        double weight = preset.SelectionWeight;
        return preferred ? weight * settings.preferredMultiplier : weight;
    }

    public static bool TrySelect(IReadOnlyList<EnemySquadPreset> candidates, SpawnAreaContext area,
        SquadSelectionSettings settings, IRandomSource random, out EnemySquadPreset selected)
    {
        selected = null;
        if (candidates == null || settings == null || !settings.IsValid || random == null || !random.IsAvailable)
            return false;

        int capacity = settings.maxEnemiesPerCell - area.CellEnemyCount;
        if (capacity <= 0) return false;
        double total = 0d;
        foreach (EnemySquadPreset preset in candidates)
            if (preset != null && preset.TotalWeight > 0 && preset.TotalWeight <= capacity)
                total += CalculateWeight(preset, area, settings);
        if (total <= 0d) return false;

        double remaining = random.NextDouble(0d, total);
        foreach (EnemySquadPreset preset in candidates)
        {
            if (preset == null || preset.TotalWeight <= 0 || preset.TotalWeight > capacity) continue;
            double weight = CalculateWeight(preset, area, settings);
            if (weight <= 0d) continue;
            selected = preset;
            if (remaining < weight) return true;
            remaining -= weight;
        }
        return selected != null;
    }
}
