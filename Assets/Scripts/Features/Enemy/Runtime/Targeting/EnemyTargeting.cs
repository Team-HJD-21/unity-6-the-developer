using UnityEngine;

/// <summary>
/// 배정된 타깃과 컴포넌트·콜라이더 참조를 보관한다.
/// </summary>
public class EnemyTargeting : MonoBehaviour
{
    // 실행 중 참조와 상태
    private Transform _target;
    private TargetableComponent _targetComponent;
    private Collider2D[] _targetColliders;

    /// <summary>현재 배정된 이동·공격 타깃을 반환한다.</summary>
    public Transform Target => _target;

    /// <summary>타깃 지정 시 저장한 타깃 상태 컴포넌트를 반환한다.</summary>
    public TargetableComponent TargetComponent => _targetComponent;

    /// <summary>사거리 검사에서 재사용할 타깃 콜라이더 목록을 반환한다.</summary>
    public Collider2D[] TargetColliders => _targetColliders;

    /// <summary>타깃 참조와 콜라이더 목록 갱신이 끝나면 알린다.</summary>
    public event System.Action TargetChanged;

    // Unity 생명주기

    /// <summary>제거되는 몬스터의 타깃 이벤트 구독을 정리한다.</summary>
    private void OnDestroy()
    {
        UnsubscribeTarget();
    }

    // 외부 호출과 상태 변경

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

    // 내부 처리

    /// <summary>현재 보관 중인 타깃의 상실 이벤트일 때만 참조를 비운다.</summary>
    private void OnTargetabilityLost(ITargetable target)
    {
        // 분대 이벤트가 먼저 새 타깃을 배정했다면 이전 이벤트는 무시한다.
        if (!ReferenceEquals(_targetComponent, target))
            return;

        SetTarget(null);
    }

    /// <summary>기존 타깃에 연결한 상실 이벤트 구독을 해제한다.</summary>
    private void UnsubscribeTarget()
    {
        if (!ReferenceEquals(_targetComponent, null))
            _targetComponent.TargetabilityLost -= OnTargetabilityLost;
    }
}
