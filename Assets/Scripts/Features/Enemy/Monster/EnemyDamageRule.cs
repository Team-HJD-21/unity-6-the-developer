using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>몬스터별 피격 허용 규칙을 제공한다.</summary>
[DisallowMultipleComponent]
public abstract class EnemyDamageRule : MonoBehaviour
{
    public abstract bool CanBeAttackedBy(AttackSourceType source);
}
