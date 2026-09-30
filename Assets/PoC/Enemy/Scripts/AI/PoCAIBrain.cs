using UnityEngine;

/// <summary>
/// 몬스터 AI의 판단 흐름을 관리한다.
/// 타깃이 유효하지 않거나 재탐색 주기가 지나면 새 타깃을 선택하고,
/// 선택 결과를 <see cref="PoCMonster"/>에 전달해 행동을 실행시킨다.
/// </summary>
[RequireComponent(typeof(PoCTargetSelector), typeof(PoCMonster))]
public class PoCAIBrain : MonoBehaviour
{
    [SerializeField] private PoCTargetSelectionSettings targetSettings;

    private float _nextRetargetTime;
    private ITargetable _currentTarget;
    private PoCTargetSelector _targetSelector;
    private PoCMonster _monster;

    /// <summary>
    /// 타깃 선택기와 행동 실행 컴포넌트를 초기화한다.
    /// </summary>
    private void Awake()
    {
        _targetSelector = GetComponent<PoCTargetSelector>();
        _monster = GetComponent<PoCMonster>();
    }

    /// <summary>
    /// 타깃을 갱신하고 몬스터가 해당 타깃에 대한 행동을 실행하도록 한다.
    /// </summary>
    public void UpdateAI()
    {
        UpdateTarget();
        _monster.ExecuteBehavior();
    }

    /// <summary>
    /// 현재 타깃이 유효하지 않거나 재탐색 주기가 지나면 새로운 타깃을 선택한다.
    /// </summary>
    private void UpdateTarget()
    {
        // 타깃 선택에 필요한 참조가 준비되지 않았으면 판단을 중단한다.
        if (targetSettings == null || _targetSelector == null || _monster == null)
            return;

        // 인터페이스 참조만 남은 파괴된 Unity 오브젝트도 유효하지 않은 타깃으로 처리한다.
        bool hasValidTarget =
            _currentTarget is Object targetObject &&
            targetObject != null &&
            _currentTarget.CanBeTargeted;

        // 타깃을 잃었거나 재탐색 시간이 되었을 때만 점수를 다시 비교한다.
        bool shouldRetarget = !hasValidTarget || Time.time >= _nextRetargetTime;

        if (shouldRetarget)
        {
            // 새 타깃을 선택해 몬스터에 전달하고 다음 재탐색 시점을 예약한다.
            _currentTarget = _targetSelector.SelectTarget();
            _monster.SetTarget(_currentTarget?.TargetTransform);
            _nextRetargetTime = Time.time + targetSettings.retargetInterval;
        }
    }
}
