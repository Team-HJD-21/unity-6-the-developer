using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Serialization;
using Unity.Profiling;
/// <summary>
/// 능력의 공통 쿨타임과 실행 조건, 거리 판정을 제공한다.
/// </summary>
public abstract partial class EnemyAbility : MonoBehaviour
{
    [Header("능력 설정")]
    [FormerlySerializedAs("attackCooldown")]
    [SerializeField, Min(0f)] private float cooldown = 1f;

    [Header("사거리 판정")]
    [SerializeField] protected Collider2D _bodyCollider;
    [FormerlySerializedAs("attackRange")]
    [SerializeField] protected float activationRange = 0.4f;

    private EnemyNetworkController _networkController;
    
    private double _nextUseTime;

    public virtual bool IsExecuting => false;
    
    /// <summary>
    /// 능력 사용 시 바라볼 대상. 없으면 컨트롤러에 배정된 타깃을 사용한다.
    /// </summary>
    public virtual Transform FacingTarget => null;

    // 프로파일링
    private static readonly ProfilerMarker UseConditionMarker =
        new("TeamHJD.Enemy.TryUse.CheckCondition");
    
    
    /// <summary>
    /// 네트워크 실행 중에는 서버만 능력을 사용하도록 확인한다.
    /// </summary>
    protected bool HasAbilityAuthority
    {
        get
        {
            // 독립 테스트에서 컴포넌트가 나중에 추가되는 경우도 지원한다.
            if (_networkController == null)
                TryGetComponent(out _networkController);

            if (_networkController == null)
                return true;

            var manager = _networkController.NetworkManager;

            return manager == null ||
                   !manager.IsListening ||
                   (_networkController.IsSpawned && _networkController.IsServer);
        }
    }

    protected bool CanAdvanceAbility => isActiveAndEnabled && HasAbilityAuthority && FieldSimulationTime.CanAdvance(this);

    protected virtual void Awake()
    {
        _networkController = GetComponent<EnemyNetworkController>();

        // 명시적으로 연결한 콜라이더는 Trigger 여부와 관계없이 사용한다.
        if (_bodyCollider != null)
            return;

        _bodyCollider = FindBodyCollider();
    }

    /// <summary>
    /// Inspector 참조가 없으면 HitBox 자식의 콜라이더를 찾고,
    /// 구형 프리팹을 위해 루트의 비-Trigger 콜라이더도 확인한다.
    /// </summary>
    protected Collider2D FindBodyCollider()
    {
        Transform hitBox = transform.Find("HitBox");
        if (hitBox != null &&
            hitBox.TryGetComponent(out Collider2D hitBoxCollider) &&
            !hitBoxCollider.isTrigger)
        {
            return hitBoxCollider;
        }

        // 구형 프리팹은 루트에 몸통 콜라이더를 가지고 있다.
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            if (collider.isTrigger)
                continue;

            return collider;
        }

        return null;
    }

    /// <summary>
    /// 능력별 발동 조건을 확인한다. 쿨타임은 포함하지 않는다.
    /// </summary>
    public abstract bool CheckCondition();

    /// <summary>
    /// 발동에 성공한 경우에만 쿨타임을 시작한다.
    /// </summary>
    // public bool TryUse()
    // {
    //     if (!CanAdvanceAbility ||
    //         IsExecuting ||
    //         FieldSimulationTime.Seconds(this) < _nextUseTime ||
    //         !CheckCondition() ||
    //         !Activate())
    //     {
    //         return false;
    //     }
    //
    //     _nextUseTime = FieldSimulationTime.Seconds(this) + cooldown;
    //     return true;
    // }

    public bool TryUse()
    {
        if (!CanAdvanceAbility ||
            IsExecuting ||
            FieldSimulationTime.Seconds(this) < _nextUseTime)
        {
            return false;
        }

        bool conditionMet;

        using (UseConditionMarker.Auto())
        {
            conditionMet = CheckCondition();
        }

        if (!conditionMet || !Activate())
            return false;

        _nextUseTime = FieldSimulationTime.Seconds(this) + cooldown;
        return true;
    }
    
    /// <summary>
    /// 실제 능력을 실행하고 성공 여부를 반환한다. 성공 시에만 공통 쿨타임을 소비한다.
    /// </summary>
    protected abstract bool Activate();

    /// <summary>
    /// 현재 능력의 범위 안에 대상 콜라이더가 있는지 확인한다.
    /// </summary>
    public bool IsInRange(Collider2D[] targetColliders)
    {
        return IsInRange(targetColliders, activationRange);
    }

    /// <summary>
    /// 먼 대상은 Bounds 간격으로 제외하고, 가까운 대상만 실제 콜라이더 간격을 확인한다.
    /// </summary>
    protected bool IsInRange(
        Collider2D[] targetColliders,
        float range)
    {
        if (_bodyCollider == null ||
            !_bodyCollider.enabled ||
            !_bodyCollider.gameObject.activeInHierarchy ||
            targetColliders == null ||
            range < 0f)
        {
            return false;
        }

        Bounds bodyBounds = _bodyCollider.bounds;
        float rangeSquared = range * range;

        foreach (Collider2D targetCollider in targetColliders)
        {
            if (targetCollider == null ||
                !targetCollider.enabled ||
                !targetCollider.gameObject.activeInHierarchy ||
                targetCollider.isTrigger)
            {
                continue;
            }

            Bounds targetBounds = targetCollider.bounds;

            // 두 Bounds 사이의 X·Y 간격을 구한다.
            // 해당 축에서 겹치면 간격은 0이다.
            float gapX = Mathf.Max(
                0f,
                Mathf.Abs(bodyBounds.center.x - targetBounds.center.x) -
                bodyBounds.extents.x -
                targetBounds.extents.x);

            float gapY = Mathf.Max(
                0f,
                Mathf.Abs(bodyBounds.center.y - targetBounds.center.y) -
                bodyBounds.extents.y -
                targetBounds.extents.y);

            // Bounds부터 사거리 밖이면 실제 콜라이더도 사거리 밖이다.
            if (gapX * gapX + gapY * gapY > rangeSquared)
                continue;

            ColliderDistance2D distance =
                _bodyCollider.Distance(targetCollider);

            if (distance.isValid && distance.distance <= range)
                return true;
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D bodyCollider =
            _bodyCollider != null ? _bodyCollider : FindBodyCollider();

        if (bodyCollider == null)
            return;

        Bounds bounds = bodyCollider.bounds;
        float bodyRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bounds.center, bodyRadius + activationRange);
    }
}
