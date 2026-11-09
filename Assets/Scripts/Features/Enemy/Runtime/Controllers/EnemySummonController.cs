using UnityEngine;

/// <summary>소환형의 이동과 변태를 제어하고, 변태 후 소환 능력을 실행한다.</summary>
[RequireComponent(typeof(EnemySummonAbility))]
public sealed class EnemySummonController : MovingEnemyController
{
    private enum SummonState
    {
        Mobile,
        Transforming,
        Transformed
    }

    [SerializeField] private Rigidbody2D rigidbody2D;

    private SummonState _state = SummonState.Mobile;
    private bool _deathHandled;

    protected override void Awake()
    {
        base.Awake();

        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
    }

    /// <summary>
    /// 이동·변태 대기·위치 고정 상태에 따라 추적 또는 소환 능력을 실행한다.
    /// </summary>
    public override void ExecuteBehavior()
    {
        if (_deathHandled || enemyAbility == null || !enemyAbility.isActiveAndEnabled)
        {
            StopMoving();
            return;
        }

        switch (_state)
        {
            case SummonState.Mobile:
                if (TryTransform())
                {
                    StopMoving();
                    enemyAnimation.PlayAbilityAnimation();
                }
                else
                {
                    MoveToTarget();
                }
                break;

            case SummonState.Transforming:
                StopMoving();
                break;

            case SummonState.Transformed:
                StopMoving();
                TryUseAbility();
                break;
        }
    }

    /// <summary>
    /// 유효한 타깃이 범위에 들어오면 소환 쿨타임을 소비하지 않고 변태를 시작한다.
    /// </summary>
    private bool TryTransform()
    {
        if (enemyTargeting == null || enemyTargeting.Target == null)
            return false;

        ITargetable target =
            enemyTargeting.Target.GetComponent<ITargetable>();

        if (target == null ||
            !target.CanBeTargeted ||
            !enemyAbility.IsInRange(enemyTargeting.TargetColliders))
        {
            return false;
        }

        _state = SummonState.Transforming;
        return true;
    }

    /// <summary>변태가 끝나면 위치를 고정하고 소환 상태로 전환한다.</summary>
    public void OnTransformationAnimationFinished()
    {
        if (_deathHandled || _state != SummonState.Transforming)
            return;

        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.constraints |= RigidbodyConstraints2D.FreezePosition;
        _state = SummonState.Transformed;
    }

    /// <summary>
    /// 중복 사망 처리를 막고 변태를 완료한 경우에만 사망 소환을 실행한다.
    /// </summary>
    public override void OnDeath()
    {
        if (_deathHandled)
            return;

        _deathHandled = true;
        StopMoving();

        if (_state != SummonState.Transformed)
            return;

        EnemySummonAbility summonAbility = enemyAbility as EnemySummonAbility;
        if (summonAbility != null)
            summonAbility.SummonOnDeath();
    }
}
