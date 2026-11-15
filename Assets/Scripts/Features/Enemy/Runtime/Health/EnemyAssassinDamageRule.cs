using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>탐지된 암살자에게 플레이어 공격만 허용한다.</summary>
[RequireComponent(typeof(EnemyDetection))]
public sealed class EnemyAssassinDamageRule : EnemyDamageRule
{
    private EnemyDetection _detection;

    // Unity 생명주기

    /// <summary>플레이어에게 탐지되었는지 확인할 상태 컴포넌트를 연결한다.</summary>
    private void Awake()
    {
        _detection = GetComponent<EnemyDetection>();
    }

    // 외부 호출과 상태 변경

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
