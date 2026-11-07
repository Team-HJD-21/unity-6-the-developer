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
    [Min(0)] public int sparseEnemyCount = 5;
    [Min(1)] public int supportEnemyCount = 6;
    [Min(1)] public int denseTurretCount = 3;
    [Min(0f)] public float nearFrontlineDistance = 10f;
    [Min(0f)] public float approachFrontlineDistance = 25f;

    [Header("선택과 배치")]
    [Min(1)] public int preferredMultiplier = 3;
    [Min(1)] public int maxEnemiesPerCell = 20;

    public bool IsValid => nearbyCellRadius >= 0 && sparseEnemyCount >= 0 &&
        supportEnemyCount > 0 && denseTurretCount > 0 && preferredMultiplier > 0 &&
        maxEnemiesPerCell > 0 && !float.IsNaN(nearFrontlineDistance) &&
        !float.IsInfinity(nearFrontlineDistance) && nearFrontlineDistance >= 0f &&
        !float.IsNaN(approachFrontlineDistance) && !float.IsInfinity(approachFrontlineDistance) &&
        approachFrontlineDistance >= nearFrontlineDistance;
}

/// <summary>상황에 맞는 편성의 비중을 높이고 Core 난수로 하나를 선택한다.</summary>
public static class SquadSelectionPolicy
{
    public static double CalculateWeight(EnemySquadPreset preset, SpawnAreaContext area,
        SquadSelectionSettings settings)
    {
        if (preset == null || preset.SelectionWeight <= 0 || settings == null || !settings.IsValid)
            return 0d;

        bool near = area.HasFrontline && area.FrontlineDistance <= settings.nearFrontlineDistance;
        bool approaching = area.HasFrontline &&
            area.FrontlineDistance > settings.nearFrontlineDistance &&
            area.FrontlineDistance <= settings.approachFrontlineDistance;
        bool denseTurrets = area.NearbyTurretCount >= settings.denseTurretCount;
        bool preferred;
        switch (preset.Role)
        {
            case EnemySquadRole.Patrol:
                preferred = area.NearbyEnemyCount <= settings.sparseEnemyCount && area.NearbyTurretCount == 0;
                break;
            case EnemySquadRole.Assault:
                preferred = near && !denseTurrets;
                break;
            case EnemySquadRole.Backline:
                preferred = near && area.NearbyTurretCount > 0 && !denseTurrets;
                break;
            case EnemySquadRole.Summon:
                preferred = approaching;
                break;
            case EnemySquadRole.Sustain:
                preferred = area.NearbyEnemyCount >= settings.supportEnemyCount;
                break;
            case EnemySquadRole.Elite:
                preferred = near && denseTurrets;
                break;
            case EnemySquadRole.Balanced:
                preferred = near;
                break;
            case EnemySquadRole.Ambush:
                preferred = false;
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
