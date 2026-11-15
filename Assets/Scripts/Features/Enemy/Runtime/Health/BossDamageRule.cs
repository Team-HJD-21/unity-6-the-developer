using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>직접 소환한 병력의 생존 여부와 탈진 상태에 따라 보스 피해량을 조절한다.</summary>
public sealed class BossDamageRule : EnemyDamageRule
{
    // Inspector 설정과 제작 참조
    [SerializeField] private EnemySummonGenerator summonGenerator;

    [Header("받는 피해 배율")]
    [SerializeField, Range(0f, 1f)] private float summonDamageReduction = 0.5f;
    [SerializeField, Min(1f)] private float exposedDamageMultiplier = 1.5f;

    public bool IsExposed { get; private set; }
    public bool IsProtectedBySummons =>
        summonGenerator != null && summonGenerator.AliveSummonCount > 0;

    // Unity 생명주기
    private void Awake()
    {
        if (summonGenerator == null)
            summonGenerator = GetComponent<EnemySummonGenerator>();
    }

    private void OnDisable() => IsExposed = false;

    // 외부 호출과 상태 변경
    public override bool CanBeAttackedBy(AttackSourceType source) => true;

    /// <summary>보호와 약점 배율을 곱한다. 완전 무적 대신 양수 공격에 최소 1 피해를 보장한다.</summary>
    public override int ModifyDamage(int damage, AttackSourceType source)
    {
        if (damage <= 0)
            return 0;

        double multiplier = IsProtectedBySummons ? 1d - summonDamageReduction : 1d;
        if (IsExposed)
            multiplier *= exposedDamageMultiplier;

        double scaled = System.Math.Round(damage * multiplier, System.MidpointRounding.AwayFromZero);
        return (int)System.Math.Clamp(scaled, 1d, int.MaxValue);
    }

    /// <summary>패턴 컨트롤러가 탈진 시작과 종료 시 약점 상태를 갱신한다.</summary>
    public void SetExposed(bool exposed) => IsExposed = exposed;
}
