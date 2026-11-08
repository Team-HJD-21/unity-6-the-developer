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

    public override bool CanBeAttackedBy(AttackSourceType source)
    {
        return source == AttackSourceType.Player &&
               _detection != null &&
               _detection.IsDetected;
    }
}
