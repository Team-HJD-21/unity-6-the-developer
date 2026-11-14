using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using Unity.Profiling;

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
    private FieldSimulationActor _simulationActor;

    // 프로파일링
    private static readonly ProfilerMarker CheckConditionMarker =
        new("TeamHJD.Enemy.CheckCondition");

    private static readonly ProfilerMarker TargetRangeMarker =
        new("TeamHJD.Enemy.IsInRange");

    private static readonly ProfilerMarker AbilityConditionMarker =
        new("TeamHJD.Enemy.AbilityCondition");

    private static readonly ProfilerMarker AttackMarker =
        new("TeamHJD.Enemy.Attack");

    private static readonly ProfilerMarker PathRefreshMarker =
        new("TeamHJD.Enemy.RefreshPlayerPath");

    private static readonly ProfilerMarker PathDirectionMarker =
        new("TeamHJD.Enemy.PathDirection");

    private static readonly ProfilerMarker MovementMarker =
        new("TeamHJD.Enemy.Movement");

    private static readonly ProfilerMarker StopMovingMarker =
        new("TeamHJD.Enemy.StopMoving");
    
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

        bool isInTargetRange;
        bool conditionMet;

        using (CheckConditionMarker.Auto())
        {
            // 전투 타깃의 사거리 계산.
            using (TargetRangeMarker.Auto())
            {
                isInTargetRange =
                    enemyTargeting.Target != null &&
                    enemyAbility.IsInRange(enemyTargeting.TargetColliders);
            }

            // 사거리 결과를 받은 능력별 조건 검사.
            using (AbilityConditionMarker.Auto())
            {
                conditionMet = enemyAbility.CheckCondition(isInTargetRange);
            }
        }

        if (conditionMet)
        {
            // 정지·공격 방향 설정·능력 사용 시도.
            using (AttackMarker.Auto())
            {
                StopMoving();

                Transform facingTarget = enemyAbility.FacingTarget;
                if (facingTarget == null)
                    facingTarget = enemyTargeting.Target;

                if (facingTarget != null)
                {
                    Vector2 abilityDirection =
                        (Vector2)facingTarget.position -
                        (Vector2)transform.position;

                    if (abilityDirection.sqrMagnitude > 0.0001f)
                        enemyAnimation.SetFacingDirection(abilityDirection);
                }

                TryUseAbility(conditionAlreadyChecked: true);
            }

            return;
        }

        MoveToTarget(isInTargetRange);
    }

    /// <summary>이번 행동에서 계산한 거리 결과로 정지 또는 이동을 결정한다.</summary>
    protected void MoveToTarget(bool isInTargetRange)
    {
        if (enemyTargeting.Target == null || isInTargetRange)
        {
            StopMoving();
            return;
        }

        // 감지 범위 확인·갱신 시점 판단·필요한 경우 경로 계산.
        using (PathRefreshMarker.Auto())
        {
            RefreshPlayerPath();
        }

        bool hasPathDirection;
        Vector2 moveDirection;

        // 경유점 도착 확인·다음 이동 방향 계산.
        using (PathDirectionMarker.Auto())
        {
            hasPathDirection =
                enemyMovement.TryGetNextPathDirection(out moveDirection);
        }

        if (!hasPathDirection)
        {
            StopMoving();
            return;
        }

        // 이동 방향·애니메이션 갱신·Rigidbody2D 이동 힘 적용.
        using (MovementMarker.Auto())
        {
            enemyAnimation.SetFacingDirection(moveDirection);
            MoveTowardsTarget(moveDirection);
        }
    }

    /// <summary>플레이어의 이동량과 감지 상태에 따라 경로를 갱신한다.</summary>
    private void RefreshPlayerPath()
    {
        if (!_isPlayerTarget)
            return;

        Transform target = enemyTargeting.Target;
        double now = GetPathTime();

        // 감지 콜라이더가 꺼져 있어도 거리로 근거리·원거리 갱신을 구분한다.
        bool isInside = _playerSensor != null &&
                        _playerSensor.IsTargetInside(target);

        float interval = isInside
            ? nearPathRefreshInterval
            : farPathRefreshInterval;

        // 느린 갱신을 기다리던 중 다시 감지되면 다음 계산 시점을 앞당긴다.
        if (isInside)
        {
            _nextPathRefreshTime = System.Math.Min(
                _nextPathRefreshTime,
                now + interval);
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
        using (StopMovingMarker.Auto())
        {
            enemyAnimation.StopMovementAnimation();
        }
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
            GetPathTime() +
            nearPathRefreshInterval * (0.5f + _pathRefreshPhase * 0.5f);
    }
    
    /// <summary>연결된 Match 시간 참조를 재사용한다.</summary>
    private double GetPathTime()
    {
        // 스폰 과정에서 나중에 추가되는 컴포넌트도 연결한다.
        if (_simulationActor == null)
            _simulationActor = GetComponentInParent<FieldSimulationActor>();

        if (_simulationActor != null)
            return _simulationActor.Seconds;

        // Match에 연결되지 않은 테스트는 기존 시간 조회를 사용한다.
        return FieldSimulationTime.Seconds(this);
    }
}
