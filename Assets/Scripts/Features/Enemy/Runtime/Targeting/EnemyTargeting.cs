using UnityEngine;

/// <summary>
/// 배정된 타깃과 컴포넌트·콜라이더 참조를 보관한다.
/// </summary>
public class EnemyTargeting : MonoBehaviour
{
    private Transform _target;
    private TargetableComponent _targetComponent;
    private Collider2D[] _targetColliders;

    public Transform Target => _target;
    public TargetableComponent TargetComponent => _targetComponent;
    public Collider2D[] TargetColliders => _targetColliders;

    /// <summary>타깃 참조와 콜라이더 목록 갱신이 끝나면 알린다.</summary>
    public event System.Action TargetChanged;
    
    /// <summary>
    /// 타깃 지정 시 필요한 참조를 저장하고 상실 이벤트를 연결한다.
    /// </summary>
    public void SetTarget(Transform target)
    {
        UnsubscribeTarget();

        // 이전 타깃 참조부터 비운다.
        _target = null;
        _targetComponent = null;
        _targetColliders = null;

        if (target == null)
        {
            TargetChanged?.Invoke();
            return;
        }

        TargetableComponent targetComponent =
            target.GetComponent<TargetableComponent>();

        // 공격 불가 대상은 저장하지 않는다.
        if (targetComponent != null && !targetComponent.CanBeTargeted)
        {
            TargetChanged?.Invoke();
            return;
        }

        _target = target;
        _targetComponent = targetComponent;
        _targetColliders = target.GetComponentsInChildren<Collider2D>(true);

        if (_targetComponent != null)
            _targetComponent.TargetabilityLost += OnTargetabilityLost;
        
        TargetChanged?.Invoke();
    }

    private void OnTargetabilityLost(ITargetable target)
    {
        // 분대 이벤트가 먼저 새 타깃을 배정했다면 이전 이벤트는 무시한다.
        if (!ReferenceEquals(_targetComponent, target))
            return;

        SetTarget(null);
    }

    private void UnsubscribeTarget()
    {
        if (!ReferenceEquals(_targetComponent, null))
            _targetComponent.TargetabilityLost -= OnTargetabilityLost;
    }

    private void OnDestroy()
    {
        UnsubscribeTarget();
    }
}