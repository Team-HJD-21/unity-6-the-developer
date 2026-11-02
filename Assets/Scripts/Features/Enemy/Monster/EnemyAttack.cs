using UnityEngine;

/// <summary>
/// 적이 공격 범위에 도달했을 때 실행할 공격 방식을 정의한다.
/// </summary>
public abstract class EnemyAttack : MonoBehaviour
{
    /// <summary>
    /// 지정한 타깃을 공격하고 공격이 실행되었는지 반환한다.
    /// </summary>
    public abstract bool Execute(TargetableComponent target, int damage);
}
