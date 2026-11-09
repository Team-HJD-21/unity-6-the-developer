using UnityEngine;

/// <summary>플레이어 공통 성장 수치입니다. 무기 자체 피해량/강화 데이터와 구분합니다.</summary>
[CreateAssetMenu(menuName = "Player/Base Stats Settings")]
public sealed class PlayerBaseStatsSettings : ScriptableObject
{
    [SerializeField, Min(0)] private float _attackPerPoint = .05f;
    [SerializeField, Min(0)] private float _movementPerPoint = .02f;
    [SerializeField, Range(.001f, 1)] private float _criticalChancePerPoint = .01f;
    [SerializeField, Range(0, 1)] private float _criticalChanceCap = .5f;
    [SerializeField, Min(1)] private float _criticalDamageMultiplier = 1.5f;
    [SerializeField, Range(1, 100)] private int _pointsPerLevelReward = 1;
    public float AttackPerPoint => Mathf.Max(0, _attackPerPoint);
    public float MovementPerPoint => Mathf.Max(0, _movementPerPoint);
    public float CriticalChancePerPoint => Mathf.Clamp(_criticalChancePerPoint, .001f, 1);
    public float CriticalChanceCap => Mathf.Clamp01(_criticalChanceCap);
    public float CriticalDamageMultiplier => Mathf.Max(1, _criticalDamageMultiplier);
    public int PointsPerLevelReward => Mathf.Clamp(_pointsPerLevelReward, 1, 100);
    public int CriticalPointLimit => Mathf.CeilToInt(CriticalChanceCap / CriticalChancePerPoint - .0001f);
    public bool IsValid => float.IsFinite(_attackPerPoint) && _attackPerPoint >= 0 &&
        float.IsFinite(_movementPerPoint) && _movementPerPoint >= 0 &&
        float.IsFinite(_criticalChancePerPoint) && _criticalChancePerPoint >= .001f && _criticalChancePerPoint <= 1 &&
        float.IsFinite(_criticalChanceCap) && _criticalChanceCap >= 0 && _criticalChanceCap <= 1 &&
        float.IsFinite(_criticalDamageMultiplier) && _criticalDamageMultiplier >= 1 &&
        _pointsPerLevelReward >= 1 && _pointsPerLevelReward <= 100;
}
