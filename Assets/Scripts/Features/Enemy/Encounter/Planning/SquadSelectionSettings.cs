using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using UnityEngine;

/// <summary>기준 타깃 주변 조사와 상황별 편성 선택·지역 예산 배분에 사용하는 조정값.</summary>
[Serializable]
public sealed class SquadSelectionSettings
{
    [Serializable]
    public sealed class RoleWeightEntry
    {
        public EnemySquadRole role;
        [Min(0)] public int weight = 1;
    }

    [Header("역할 기본 가중치")]
    [Tooltip("Role 추첨 비중입니다. 항목이 없으면 역할별 기본값을 사용하고, 0이면 해당 역할을 자동 편성에서 제외합니다.")]
    public RoleWeightEntry[] roleWeights =
    {
        new RoleWeightEntry { role = EnemySquadRole.Patrol, weight = 4 },
        new RoleWeightEntry { role = EnemySquadRole.Assault, weight = 2 },
        new RoleWeightEntry { role = EnemySquadRole.Backline, weight = 2 },
        new RoleWeightEntry { role = EnemySquadRole.Summon, weight = 2 },
        new RoleWeightEntry { role = EnemySquadRole.Sustain, weight = 2 },
        new RoleWeightEntry { role = EnemySquadRole.Ambush, weight = 1 }
    };

    [Header("주변 조사")]
    [Min(0)] public int nearbyCellRadius = 1;

    [Header("상황 판단")]
    [InspectorName("병력 적음 기준 (%)")]
    [Tooltip("전체 등록 몬스터 중 주변 몬스터의 비율이 이 값 이하면 적음으로 판단합니다.")]
    [Range(0f, 100f)] public float sparseEnemyPercentage = 20f;

    [Header("선택과 배치")]
    [Min(1)] public int preferredMultiplier = 3;
    [Min(1)] public int maxEnemiesPerCell = 20;

    [Header("예산 배분")]
    [Min(1)] public int baseBudgetWeight = 1;
    [Min(0)] public int sparseBudgetBonus = 1;
    [Min(0)] public int playerBudgetBonus = 2;

    [Header("진단")]
    public bool logSelectionDecisions;

    public bool IsValid => nearbyCellRadius >= 0 && sparseEnemyPercentage >= 0f &&
        sparseEnemyPercentage <= 100f && preferredMultiplier > 0 && maxEnemiesPerCell > 0 &&
        baseBudgetWeight >= 1 && sparseBudgetBonus >= 0 && playerBudgetBonus >= 0 && AreRoleWeightsValid();

    public int GetRoleWeight(EnemySquadRole role)
    {
        if (roleWeights != null)
        {
            foreach (RoleWeightEntry entry in roleWeights)
            {
                if (entry != null && entry.role == role)
                    return entry.weight;
            }
        }

        return role switch
        {
            EnemySquadRole.Patrol => 4,
            EnemySquadRole.Assault => 2,
            EnemySquadRole.Backline => 2,
            EnemySquadRole.Summon => 2,
            EnemySquadRole.Sustain => 2,
            EnemySquadRole.Ambush => 1,
            _ => 0
        };
    }

    private bool AreRoleWeightsValid()
    {
        // 기존 Scene에 설정이 없으면 GetRoleWeight의 역할별 기본값을 사용한다.
        if (roleWeights == null) return true;
        for (int i = 0; i < roleWeights.Length; i++)
        {
            RoleWeightEntry entry = roleWeights[i];
            if (entry == null || entry.weight < 0 || !Enum.IsDefined(typeof(EnemySquadRole), entry.role))
                return false;
            for (int j = i + 1; j < roleWeights.Length; j++)
                if (roleWeights[j] != null && roleWeights[j].role == entry.role)
                    return false;
        }
        return true;
    }
}

/// <summary>상황에 맞는 편성의 비중을 높이고 Core 난수로 하나를 선택한다.</summary>
public static class SquadSelectionPolicy
{
    private static readonly EnemySquadRole[] SelectableRoles =
    {
        EnemySquadRole.Patrol,
        EnemySquadRole.Assault,
        EnemySquadRole.Backline,
        EnemySquadRole.Summon,
        EnemySquadRole.Sustain,
        EnemySquadRole.Ambush
    };

    /// <summary>
    /// 역할이 현재 상황의 우대 조건을 만족하면 역할 기본 가중치에 배율을 적용한다.
    /// </summary>
    public static double CalculateRoleWeight(EnemySquadRole role, SpawnAreaContext area,
        SquadSelectionSettings settings)
    {
        if (settings == null || !settings.IsValid)
            return 0d;

        return CalculateRoleWeightUnchecked(role, area, settings);
    }

    private static double CalculateRoleWeightUnchecked(EnemySquadRole role, SpawnAreaContext area,
        SquadSelectionSettings settings)
    {

        bool sparse = area.NearbyEnemyPercentage <= settings.sparseEnemyPercentage;
        bool crowded = !sparse;
        bool preferred;
        switch (role)
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
            case EnemySquadRole.Ambush:
                preferred = area.HasNearbyPlayer;
                break;
            default:
                return 0d;
        }

        double weight = settings.GetRoleWeight(role);
        // 우대 조건이 맞지 않아도 기본 비중을 유지해 다른 역할이 선택될 수 있게 한다.
        return preferred ? weight * settings.preferredMultiplier : weight;
    }

    /// <summary>
    /// 먼저 역할을 상황별 가중치로 고른 뒤, 해당 역할 안에서 프리셋 가중치로 하나를 선택한다.
    /// </summary>
    public static bool TrySelect(IReadOnlyList<EnemySquadPreset> candidates, SpawnAreaContext area,
        SquadSelectionSettings settings, IRandomSource random, out EnemySquadPreset selected)
    {
        selected = null;
        if (candidates == null || settings == null || !settings.IsValid || random == null || !random.IsAvailable)
            return false;

        int capacity = settings.maxEnemiesPerCell - area.CellEnemyCount;
        if (capacity <= 0) return false;
        double totalRoleWeight = 0d;
        foreach (EnemySquadRole role in SelectableRoles)
        {
            if (!HasCandidate(candidates, role, capacity)) continue;
            totalRoleWeight += CalculateRoleWeightUnchecked(role, area, settings);
        }
        if (totalRoleWeight <= 0d) return false;

        double roleRoll = random.NextDouble(0d, totalRoleWeight);
        EnemySquadRole selectedRole = default;
        bool hasSelectedRole = false;
        foreach (EnemySquadRole role in SelectableRoles)
        {
            if (!HasCandidate(candidates, role, capacity)) continue;
            double roleWeight = CalculateRoleWeightUnchecked(role, area, settings);
            if (roleWeight <= 0d) continue;
            selectedRole = role;
            hasSelectedRole = true;
            if (roleRoll < roleWeight) break;
            roleRoll -= roleWeight;
        }
        if (!hasSelectedRole) return false;

        double totalPresetWeight = 0d;
        foreach (EnemySquadPreset preset in candidates)
            if (IsCandidateForRole(preset, selectedRole, capacity))
                totalPresetWeight += preset.SelectionWeight;
        if (totalPresetWeight <= 0) return false;

        double presetRoll = random.NextDouble(0d, totalPresetWeight);
        foreach (EnemySquadPreset preset in candidates)
        {
            if (!IsCandidateForRole(preset, selectedRole, capacity)) continue;
            if (presetRoll < preset.SelectionWeight)
            {
                selected = preset;
                return true;
            }
            presetRoll -= preset.SelectionWeight;
        }
        return false;
    }

    private static bool HasCandidate(IReadOnlyList<EnemySquadPreset> candidates,
        EnemySquadRole role, int capacity)
    {
        foreach (EnemySquadPreset preset in candidates)
            if (IsCandidateForRole(preset, role, capacity)) return true;
        return false;
    }

    private static bool IsCandidateForRole(EnemySquadPreset preset, EnemySquadRole role, int capacity)
        => preset != null && preset.Role == role && preset.SelectionWeight > 0 &&
           preset.TotalWeight > 0 && preset.TotalWeight <= capacity;
}
