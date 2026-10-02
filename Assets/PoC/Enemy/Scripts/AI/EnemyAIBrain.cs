using UnityEngine;

/// <summary>
/// 서버에서 호출되는 적의 행동 흐름을 관리한다.
/// 분대 소속 여부에 따라 개별 타깃 선택을 실행하거나 건너뛰고, 이동·공격 행동을 실행한다.
/// </summary>
[RequireComponent(typeof(EnemyTargetSelector), typeof(EnemyController))]
public class EnemyAIBrain : MonoBehaviour
{
    [SerializeField] private EnemyTargetSelectionSettings targetSettings;

    private float _nextRetargetTime;
    private ITargetable _currentTarget;
    private EnemyTargetSelector _targetSelector;
    private EnemyController _monster;
    private EnemySquad _squad;

    /// <summary>
    /// 기존 개별 타깃 선택기와 이동·공격을 담당할 컴포넌트를 찾는다.
    /// </summary>
    private void Awake()
    {
        _targetSelector = GetComponent<EnemyTargetSelector>();
        _monster = GetComponent<EnemyController>();
    }

    /// <summary>
    /// 서버의 EnemyNetworkController가 매 프레임 호출한다.
    /// 분대가 없는 적만 직접 타깃을 선택하고, 분대원은 전달받은 목표로 행동한다.
    /// </summary>
    public void UpdateAI()
    {
        if (_squad == null)
            UpdateTarget();

        _monster.ExecuteBehavior();
    }

    /// <summary>
    /// 분대에 등록하고 이전 개별 타깃을 해제한다.
    /// 이후 UpdateAI에서는 개별 선택을 건너뛰고 분대가 전달한 목표만 따른다.
    /// </summary>
    /// <param name="squad">이 적을 관리할 분대.</param>
    public void AssignSquad(EnemySquad squad)
    {
        _squad = squad;
        _currentTarget = null;
        _monster.SetTarget(null);
    }

    /// <summary>
    /// 분대에 속하지 않은 적의 기존 점수 기반 타깃을 필요할 때만 다시 선택한다.
    /// </summary>
    private void UpdateTarget()
    {
        // 타깃 선택에 필요한 참조가 준비되지 않았으면 판단을 중단한다.
        if (targetSettings == null || _targetSelector == null || _monster == null)
            return;

        // 기존 개별 선택 경로에서는 파괴된 Unity 오브젝트 참조도 유효하지 않게 처리한다.
        bool hasValidTarget =
            _currentTarget is Object targetObject &&
            targetObject != null &&
            _currentTarget.CanBeTargeted;

        // 타깃을 잃었거나 설정된 재탐색 주기가 지났을 때만 후보 점수를 비교한다.
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
