using System;
using UnityEngine;

public class PoCAIBrain : MonoBehaviour
{
    [SerializeField] private PoCTargetSelectionSettings targetSettings;
    
    float _nextRetargetTime = 0f;
    private ITargetable _currentTarget = null;
    private PoCTargetSelector _targetSelector;
    private PoCMonster _monster;
    
    private void Awake()
    {
        _targetSelector = GetComponent<PoCTargetSelector>();
        _monster = GetComponent<PoCMonster>();
    }

    public void Tick()
    {
        bool shouldRetarget =
            _currentTarget == null ||
            !_currentTarget.IsTargetable ||
            Time.time >= _nextRetargetTime;

        if (shouldRetarget)
        {
            if (!_targetSelector || !_monster)
                return;

            _currentTarget = _targetSelector.SelectTarget();
            
            _monster.SetTarget(_currentTarget?.TargetTransform);

            _nextRetargetTime = Time.time + targetSettings.retargetInterval;
        }

        _monster.Tick();
    }

    
}
