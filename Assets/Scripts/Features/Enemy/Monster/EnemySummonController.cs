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

    protected override void Awake()
    {
        base.Awake();

        if (rigidbody2D == null)
            rigidbody2D = GetComponent<Rigidbody2D>();
    }

    public override void ExecuteBehavior()
    {
        if (enemyAbility == null || !enemyAbility.isActiveAndEnabled)
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
        if (_state != SummonState.Transforming)
            return;

        rigidbody2D.linearVelocity = Vector2.zero;
        rigidbody2D.constraints |= RigidbodyConstraints2D.FreezePosition;
        _state = SummonState.Transformed;
    }
}
