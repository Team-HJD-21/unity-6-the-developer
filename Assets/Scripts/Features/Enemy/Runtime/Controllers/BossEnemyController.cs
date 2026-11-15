using UnityEngine;

/// <summary>
/// 이동 기반을 재사용하고 보스가 선택한 능력의 시작과 실행 잠금을 관리한다.
/// 구체적인 능력 선택은 각 보스 컨트롤러가 담당한다.
/// </summary>
public class BossEnemyController : MovingEnemyController
{
    private EnemyAbility _executingAbility;

    /// <summary>능력이 끝나기를 기다리며 이동과 다음 능력을 잠그고 있는지 반환한다.</summary>
    public bool IsExecutingAbility =>
        _executingAbility != null && _executingAbility.IsExecuting;

    /// <summary>분대 대표 갱신과 함께 비활성화 전 능력 실행 참조를 정리한다.</summary>
    protected override void OnDisable()
    {
        _executingAbility = null;
        base.OnDisable();
    }

    /// <summary>진행 중인 능력을 기다리거나 다음 능력을 선택해 추적 또는 실행한다.</summary>
    public override void ExecuteBehavior()
    {
        if (_executingAbility != null)
        {
            if (_executingAbility.isActiveAndEnabled && _executingAbility.IsExecuting)
            {
                if (!_executingAbility.ControlsMovement) StopMoving();
                return;
            }

            EnemyAbility completedAbility = _executingAbility;
            _executingAbility = null;
            OnAbilityCompleted(completedAbility);
        }

        EnemyAbility ability = SelectAbility();
        if (ability == null || !ability.isActiveAndEnabled)
        {
            StopMoving();
            return;
        }

        if (ability.IsExecuting)
        {
            _executingAbility = ability;
            if (!ability.ControlsMovement) StopMoving();
            return;
        }

        bool isInTargetRange =
            enemyTargeting.Target != null &&
            ability.IsInRange(enemyTargeting.TargetColliders);

        if (!ability.CheckCondition(isInTargetRange))
        {
            MoveToTarget(isInTargetRange);
            return;
        }

        if (!ability.ControlsMovement) StopMoving();
        else enemyAnimation.StopMovementAnimation();
        FaceAbilityTarget(ability);

        // 조건 검사는 위의 결과를 재사용하고 권한·쿨타임은 능력에서 다시 확인한다.
        if (!ability.TryUse(conditionAlreadyChecked: true))
            return;

        _executingAbility = ability;
        PlaySelectedAbilityAnimation(ability);
    }

    /// <summary>기본 보스는 연결된 능력을 사용하고 개별 보스는 선택 규칙을 재정의한다.</summary>
    protected virtual EnemyAbility SelectAbility() => enemyAbility;

    /// <summary>실행 중이던 능력이 끝나면 개별 보스가 다음 패턴으로 진행한다.</summary>
    protected virtual void OnAbilityCompleted(EnemyAbility ability)
    {
    }

    /// <summary>현재는 기존 Attack 전달 경로를 사용한다. 전용 스킬은 이후 구분해 전달한다.</summary>
    protected virtual void PlaySelectedAbilityAnimation(EnemyAbility ability)
    {
        enemyAnimation.PlayAbilityAnimation();
    }

    /// <summary>능력이 바라볼 대상을 우선 사용하고 없으면 현재 전투 타깃을 사용한다.</summary>
    private void FaceAbilityTarget(EnemyAbility ability)
    {
        Transform target = ability.FacingTarget != null
            ? ability.FacingTarget
            : enemyTargeting.Target;

        if (target == null)
            return;

        Vector2 direction = (Vector2)target.position - (Vector2)transform.position;
        if (direction.sqrMagnitude > 0.0001f)
            enemyAnimation.SetFacingDirection(direction);
    }
}
