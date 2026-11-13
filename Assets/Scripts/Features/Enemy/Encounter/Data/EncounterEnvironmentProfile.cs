// 환경별 Encounter 입력 조정 데이터를 Enemy Feature에 둡니다.
// 기본값은 중립이며 Core가 날씨 전투 효과나 최종 밸런스를 하드코딩하지 않습니다.
using System;
using TeamHJD.Game.Domain;
using UnityEngine;

/// <summary>
/// 날씨와 시간 조건에 따라 고정 편성과 요청 마릿수를 조정한다.
/// </summary>
[CreateAssetMenu(fileName = "EncounterEnvironmentProfile", menuName = "The Developer/Enemy/Encounter Environment Profile")]
public sealed class EncounterEnvironmentProfile : ScriptableObject
{
    [Serializable]
    private sealed class Rule
    {
        public string ruleId = "environment-rule";
        public string weatherId = "";
        public string timeOfDayId = "";
        public string presetOverride = "";
        [Range(0, 1)] public float budgetScale = 1;
    }
    /// <summary>
    /// 가장 구체적으로 일치하는 환경 규칙으로 편성과 마릿수를 조정하고 비용 예산은 유지한다.
    /// </summary>
    [SerializeField] private Rule[] rules = Array.Empty<Rule>();
    public FixedPresetSpawnRequest Select(FixedPresetSpawnRequest original, MatchEnvironmentSnapshot environment, out string ruleId)
    {
        Rule selected = SelectRule(environment, out ruleId);
        if (selected == null) return original;
        string preset = string.IsNullOrWhiteSpace(selected.presetOverride) ? original.SquadOrder : selected.presetOverride;
        return new FixedPresetSpawnRequest(preset, ScaleCount(original.MaxCount, selected.budgetScale), original.SpawnBudget);
    }

    /// <summary>자동 편성에도 동일 환경 규칙을 적용합니다. 마릿수 상한만 조절하고 비용 예산은 중복 증액하지 않습니다.</summary>
    public AreaSpawnPlanRequest Select(AreaSpawnPlanRequest original, MatchEnvironmentSnapshot environment,
        out string presetOverride, out string ruleId)
    {
        Rule selected = SelectRule(environment, out ruleId);
        presetOverride = selected?.presetOverride?.Trim();
        return selected == null ? original : new AreaSpawnPlanRequest(original.SpawnBudget,
            ScaleCount(original.MaxCount, selected.budgetScale), original.BudgetAllocations);
    }

    private static int ScaleCount(int maximum, float scale) => Math.Min(maximum, (int)Math.Floor(maximum * (double)scale));

    private Rule SelectRule(MatchEnvironmentSnapshot environment, out string ruleId)
    {
        ruleId = "neutral";
        if (environment == null) return null;
        Rule selected = null; int specificity = -1;
        if (rules.Length > 64) throw new InvalidOperationException("Too many Encounter environment rules.");
        foreach (Rule rule in rules)
        {
            if (rule == null || string.IsNullOrWhiteSpace(rule.ruleId) || rule.ruleId.Length > 128 ||
                float.IsNaN(rule.budgetScale) || float.IsInfinity(rule.budgetScale) || rule.budgetScale < 0 || rule.budgetScale > 1 ||
                (rule.presetOverride ?? "").Length > 128)
                throw new InvalidOperationException("Invalid Encounter environment rule.");
            bool weather = !string.IsNullOrWhiteSpace(rule.weatherId), time = !string.IsNullOrWhiteSpace(rule.timeOfDayId);
            if (weather && (!environment.HasWeather || rule.weatherId != environment.WeatherId.Value)) continue;
            if (time && (!environment.HasTimeOfDay || rule.timeOfDayId != environment.TimeOfDayId.Value)) continue;
            int score = (weather ? 1 : 0) + (time ? 1 : 0);
            if (score > specificity) { selected = rule; specificity = score; }
        }
        if (selected != null) ruleId = selected.ruleId;
        return selected;
    }
}
