using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 능력의 공통 쿨타임과 실행 조건, 거리 판정을 제공한다.
/// </summary>
public abstract class EnemyAbility : MonoBehaviour
{
    [Header("능력 설정")]
    [FormerlySerializedAs("attackCooldown")]
    [SerializeField, Min(0f)] private float cooldown = 1f;

    [Header("사거리 판정")]
    [SerializeField] protected Collider2D _bodyCollider;
    [FormerlySerializedAs("attackRange")]
    [SerializeField] protected float activationRange = 0.4f;

    private float _nextUseTime;

    public virtual bool IsExecuting => false;

    /// <summary>
    /// 능력 사용 시 바라볼 대상. 없으면 컨트롤러에 배정된 타깃을 사용한다.
    /// </summary>
    public virtual Transform FacingTarget => null;

    protected virtual void Awake()
    {
        // 명시적으로 연결한 콜라이더는 Trigger 여부와 관계없이 사용한다.
        if (_bodyCollider != null)
            return;

        // 기존 근접 프리팹은 참조가 없으면 일반 몸통 콜라이더를 사용한다.
        foreach (Collider2D collider in GetComponents<Collider2D>())
        {
            if (collider.isTrigger)
                continue;

            _bodyCollider = collider;
            break;
        }
    }

    /// <summary>
    /// 능력별 발동 조건을 확인한다. 쿨타임은 포함하지 않는다.
    /// </summary>
    public abstract bool CheckCondition();

    /// <summary>
    /// 발동에 성공한 경우에만 쿨타임을 시작한다.
    /// </summary>
    public bool TryUse()
    {
        if (!isActiveAndEnabled ||
            IsExecuting ||
            Time.time < _nextUseTime ||
            !CheckCondition() ||
            !Activate())
        {
            return false;
        }

        _nextUseTime = Time.time + cooldown;
        return true;
    }

    protected abstract bool Activate();

    /// <summary>
    /// 현재 능력의 범위 안에 대상 콜라이더가 있는지 확인한다.
    /// </summary>
    public bool IsInRange(Collider2D[] targetColliders)
    {
        return IsInRange(targetColliders, activationRange);
    }

    /// <summary>
    /// 자신의 몸통과 대상 콜라이더 사이의 간격이 지정 거리 이내인지 확인한다.
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

        foreach (Collider2D targetCollider in targetColliders)
        {
            if (targetCollider == null ||
                !targetCollider.enabled ||
                !targetCollider.gameObject.activeInHierarchy ||
                targetCollider.isTrigger)
            {
                continue;
            }

            ColliderDistance2D distance =
                _bodyCollider.Distance(targetCollider);

            if (distance.isValid && distance.distance <= range)
                return true;
        }

        return false;
    }
    
    private void OnDrawGizmosSelected()
    {
        Collider2D bodyCollider = _bodyCollider;

        if (bodyCollider == null)
        {
            foreach (Collider2D collider in GetComponents<Collider2D>())
            {
                if (!collider.isTrigger)
                {
                    bodyCollider = collider;
                    break;
                }
            }
        }

        if (bodyCollider == null)
            return;

        Bounds bounds = bodyCollider.bounds;
        float bodyRadius = Mathf.Max(bounds.extents.x, bounds.extents.y);

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(bounds.center, bodyRadius + activationRange);
    }
}
