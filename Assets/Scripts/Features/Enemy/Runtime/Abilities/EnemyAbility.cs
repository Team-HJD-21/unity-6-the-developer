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
    /// 컨트롤러가 계산한 전투 타깃의 거리 결과를 받아 조건을 검사한다.
    /// 별도 대상을 사용하는 능력은 기존 검사를 수행한다.
    /// </summary>
    public virtual bool CheckCondition(bool isInTargetRange)
    {
        return CheckCondition();
    }

    public bool TryUse()
    {
        return TryUse(false);
    }

    /// <summary>
    /// 같은 행동 실행에서 조건을 통과했다면 중복 검사를 생략한다.
    /// 권한·실행 상태·쿨타임은 계속 확인한다.
    /// </summary>
    internal bool TryUse(bool conditionAlreadyChecked)
    {
        if (!CanAdvanceAbility ||
            IsExecuting ||
            FieldSimulationTime.Seconds(this) < _nextUseTime)
        {
            return false;
        }

        if (!conditionAlreadyChecked)
        {
            using (UseConditionMarker.Auto())
            {
                if (!CheckCondition())
                    return false;
            }
        }

        if (!Activate())
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
            targetColliders == null ||
            targetColliders.Length == 0 ||
            range < 0f)
        {
            return false;
        }

        Bounds bodyBounds = _bodyCollider.bounds;
        Vector3 bodyExtents = bodyBounds.extents;

        // 비활성 상태이거나 유효한 영역이 없는 본인 콜라이더는 제외한다.
        if (bodyExtents.sqrMagnitude == 0f)
            return false;
        
        Vector3 bodyCenter = bodyBounds.center;
        float rangeSquared = range * range;
        
        foreach (Collider2D targetCollider in targetColliders)
        {
            // Trigger와 비활성 Collider는 Bounds를 읽기 전에 제외한다.
            if (targetCollider == null ||
                targetCollider.isTrigger ||
                !targetCollider.enabled)
            {
                continue;
            }
            
            Bounds targetBounds = targetCollider.bounds;
            Vector3 targetExtents = targetBounds.extents;

            if (targetExtents.sqrMagnitude == 0f)
                continue;
            
            Vector3 targetCenter = targetBounds.center;
            
            // 두 Bounds 사이의 X·Y 간격을 구한다.
            // 해당 축에서 겹치면 간격은 0이다.
            float gapX = Mathf.Max(
                0f,
                Mathf.Abs(bodyCenter.x - targetCenter.x) -
                bodyExtents.x -
                targetExtents.x);

            float gapY = Mathf.Max(
                0f,
                Mathf.Abs(bodyCenter.y - targetCenter.y) -
                bodyExtents.y -
                targetExtents.y);

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
