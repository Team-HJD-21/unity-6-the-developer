using UnityEngine;

/// <summary>무기고 시제품 수치입니다. 플레이어 공통 스탯과 별도로 적용합니다.</summary>
[CreateAssetMenu(menuName = "Player/Armory Settings")]
public sealed class PlayerArmorySettings : ScriptableObject
{
    [SerializeField, Range(1, 100)] private int _tokensPerLevel = 1;
    [SerializeField, Min(0)] private float _damagePerLevel = .15f;
    [SerializeField] private int[] _defaultGunPellets = { 1, 3, 5 };
    [SerializeField] private int[] _sniperTargets = { 2, 3, 4 };
    [SerializeField] private float[] _cryoSlowMultipliers = { .8f, .65f, .5f };
    public const int MaximumLevel = 3;
    public int TokensPerLevel => Mathf.Clamp(_tokensPerLevel, 1, 100);
    public float DamageMultiplier(int level) => 1 + (Mathf.Clamp(level, 1, MaximumLevel) - 1) * Mathf.Max(0, _damagePerLevel);
    public int PelletCount(int level) => _defaultGunPellets[Mathf.Clamp(level, 1, MaximumLevel) - 1];
    public int SniperTargets(int level) => _sniperTargets[Mathf.Clamp(level, 1, MaximumLevel) - 1];
    public float SlowMultiplier(int level) => _cryoSlowMultipliers[Mathf.Clamp(level, 1, MaximumLevel) - 1];
    public bool IsValid => float.IsFinite(_damagePerLevel) && _damagePerLevel >= 0 &&
        _defaultGunPellets != null && _defaultGunPellets.Length == MaximumLevel &&
        System.Array.TrueForAll(_defaultGunPellets, n => n >= 1 && n <= 32) &&
        _sniperTargets != null && _sniperTargets.Length == MaximumLevel && System.Array.TrueForAll(_sniperTargets, n => n >= 1 && n <= 4096) &&
        _cryoSlowMultipliers != null && _cryoSlowMultipliers.Length == MaximumLevel &&
        System.Array.TrueForAll(_cryoSlowMultipliers, n => float.IsFinite(n) && n > 0 && n <= 1);
}
