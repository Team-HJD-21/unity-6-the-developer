using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>탐지된 암살자에게 플레이어 공격만 허용한다.</summary>
[RequireComponent(typeof(EnemyDetection))]
public sealed class EnemyAssassinDamageRule : EnemyDamageRule
{
    private EnemyDetection _detection;

    private void Awake()
    {
        _detection = GetComponent<EnemyDetection>();
    }

    /// <summary>
    /// 플레이어가 공격한 경우에만 현재 탐지 상태를 기준으로 피해를 허용한다.
    /// </summary>
    public override bool CanBeAttackedBy(AttackSourceType source)
    {
        return source == AttackSourceType.Player &&
               _detection != null &&
               _detection.IsDetected;
    }
}
