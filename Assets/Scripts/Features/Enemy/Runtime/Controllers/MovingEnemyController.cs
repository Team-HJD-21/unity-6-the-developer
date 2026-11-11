using TeamHJD.Game.Presentation.Scene;
using UnityEngine;

/// <summary>
/// 능력의 발동 조건에 따라 타깃으로 이동하거나 정지하고 능력을 사용한다.
/// 실제 이동과 능력 효과는 각 컴포넌트에 맡긴다.
/// </summary>
public class MovingEnemyController : EnemyController
{
    [Header("이동")]
    [SerializeField] protected EnemyMovement enemyMovement;

    [Header("플레이어 추적")]
    [SerializeField, Min(0.02f)] private float nearPathRefreshInterval = 0.25f;
    [SerializeField, Min(0.02f)] private float farPathRefreshInterval = 1f;
    [SerializeField, Min(0f)] private float minimumTargetMovement = 0.2f;

    private EnemyPlayerSensor _playerSensor;
    private bool _isPlayerTarget;
    private Vector2 _lastPathTargetPosition;
    private double _nextPathRefreshTime;
    private float _pathRefreshPhase;

    protected override void Awake()
    {
        base.Awake();
        if (enemyMovement == null)
            enemyMovement = GetComponent<EnemyMovement>();

        _playerSensor = GetComponentInChildren<EnemyPlayerSensor>(true);

        // 여러 몬스터가 같은 플레이어를 추적할 때 첫 반복 계산 시점을 분산한다.
        _pathRefreshPhase = Mathf.Abs(GetInstanceID() % 1000) / 1000f;
    }

    /// <summary>
    /// 발동 조건을 충족하면 정지하고 능력을 시도하며, 충족하지 않으면 타깃으로 이동한다.
    /// </summary>
    public override void ExecuteBehavior()
    {
        if (enemyAbility == null || !enemyAbility.isActiveAndEnabled)
        {
            StopMoving();
            return;
        }

        if (enemyAbility.IsExecuting)
        {
            StopMoving();
            return;
        }

        if (enemyAbility.CheckCondition())
        {
            StopMoving();

            Transform facingTarget = enemyAbility.FacingTarget;
            if (facingTarget == null)
                facingTarget = enemyTargeting.Target;

            if (facingTarget != null)
            {
                Vector2 abilityDirection =
                    (Vector2)facingTarget.position - (Vector2)transform.position;

                if (abilityDirection.sqrMagnitude > 0.0001f)
                    enemyAnimation.SetFacingDirection(abilityDirection);
            }

            TryUseAbility();
            return;
        }

        MoveToTarget();
    }

    /// <summary>배정된 타깃으로 이동하고 범위에 도착하면 멈춘다.</summary>
    protected void MoveToTarget()
    {
        if (enemyTargeting.Target == null ||
            enemyAbility.IsInRange(enemyTargeting.TargetColliders))
        {
            StopMoving();
            return;
        }

        RefreshPlayerPath();

        if (!enemyMovement.TryGetNextPathDirection(out Vector2 moveDirection))
        {
            StopMoving();
            return;
        }

        enemyAnimation.SetFacingDirection(moveDirection);
        MoveTowardsTarget(moveDirection);
    }

    /// <summary>플레이어의 이동량과 감지 상태에 따라 경로를 갱신한다.</summary>
    private void RefreshPlayerPath()
    {
        if (!_isPlayerTarget)
            return;

        Transform target = enemyTargeting.Target;
        double now = FieldSimulationTime.Seconds(this);

        bool isInside = _playerSensor != null &&
            _playerSensor.IsTargetInside(enemyTargeting.TargetColliders);

        float interval = isInside
            ? nearPathRefreshInterval
            : farPathRefreshInterval;

        // 느린 갱신을 기다리던 중 다시 감지되면 다음 계산 시점을 앞당긴다.
        if (isInside)
        {
            _nextPathRefreshTime = System.Math.Min(
                _nextPathRefreshTime, now + interval);
        }

        if (now < _nextPathRefreshTime)
            return;

        _nextPathRefreshTime = now + interval;

        Vector2 targetPosition = target.position;
        float movedDistanceSquared =
            (targetPosition - _lastPathTargetPosition).sqrMagnitude;

        // 경로가 없으면 플레이어가 멈춰 있어도 다시 시도한다.
        if (enemyMovement.HasPath &&
            movedDistanceSquared < minimumTargetMovement * minimumTargetMovement)
        {
            return;
        }

        if (enemyMovement.TryUpdatePath(target))
            _lastPathTargetPosition = targetPosition;
    }

    /// <summary>
    /// 지정된 방향으로 이동하고 이동 애니메이션 속도를 갱신한다.
    /// </summary>
    /// <param name="moveDirection">정규화된 이동 방향.</param>
    protected void MoveTowardsTarget(Vector2 moveDirection)
    {
        enemyAnimation.PlayMovementAnimation(enemyMovement.MoveSpeed);
        enemyMovement.Move(moveDirection);
    }

    /// <summary>
    /// 이동 힘을 추가하지 않는 분기에서 이동 애니메이션을 중단한다.
    /// </summary>
    protected void StopMoving()
    {
        enemyAnimation.StopMovementAnimation();
    }

    /// <summary>
    /// 배정된 타깃으로 이동 경로를 갱신하고, 타깃이 해제되면 경로를 비운다.
    /// </summary>
    /// <param name="target">이동 목적지로 사용할 타깃. 해제된 경우 null.</param>
    protected override void OnTargetChanged(Transform target)
    {
        // 새 타깃의 첫 경로는 기다리지 않고 계산한다.
        enemyMovement.SetDestination(target);

        ITargetable targetable = target != null
            ? target.GetComponent<ITargetable>()
            : null;

        _isPlayerTarget =
            targetable != null && targetable.TargetType == TargetType.Player;

        _lastPathTargetPosition = target != null
            ? (Vector2)target.position
            : Vector2.zero;

        _nextPathRefreshTime =
            FieldSimulationTime.Seconds(this) +
            nearPathRefreshInterval * (0.5f + _pathRefreshPhase * 0.5f);
    }
}
