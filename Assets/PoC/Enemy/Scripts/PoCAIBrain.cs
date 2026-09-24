using UnityEngine;

/// <summary>
/// 타깃 재선택 시점을 판단하고 선택된 타깃을 몬스터 행동에 전달한다.
/// </summary>
[RequireComponent(typeof(PoCTargetSelector), typeof(PoCMonster))]
public class PoCAIBrain : MonoBehaviour
{
    [SerializeField] private PoCTargetSelectionSettings targetSettings;

    private float _nextRetargetTime;
    private ITargetable _currentTarget;
    private PoCTargetSelector _targetSelector;
    private PoCMonster _monster;

    private void Awake()
    {
        _targetSelector = GetComponent<PoCTargetSelector>();
        _monster = GetComponent<PoCMonster>();
    }

    public void Tick()
    {
        if (targetSettings == null || _targetSelector == null || _monster == null)
            return;

        bool hasValidTarget =
            _currentTarget is Object targetObject &&
            targetObject != null &&
            _currentTarget.IsTargetable;

        bool shouldRetarget = !hasValidTarget || Time.time >= _nextRetargetTime;

        if (shouldRetarget)
        {
            _currentTarget = _targetSelector.SelectTarget();
            _monster.SetTarget(_currentTarget?.TargetTransform);
            _nextRetargetTime = Time.time + targetSettings.retargetInterval;
        }

        _monster.Tick();
    }
}