using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>몬스터별 피격 허용 규칙을 제공한다.</summary>
[DisallowMultipleComponent]
public abstract class EnemyDamageRule : MonoBehaviour
{
    /// <summary>
    /// 몬스터별 규칙에 따라 해당 종류의 공격자가 피해를 줄 수 있는지 반환한다.
    /// </summary>
    public abstract bool CanBeAttackedBy(AttackSourceType source);

    /// <summary>피격 허용 이후 실제 적용할 피해량을 계산한다. 기본은 원래 피해를 유지한다.</summary>
    public virtual int ModifyDamage(int damage, AttackSourceType source) => damage;
}
