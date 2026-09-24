using System.Collections.Generic;
using UnityEngine;


[RequireComponent(typeof(PoCTargetScoreEvaluator))]
public class PoCTargetSelector : MonoBehaviour
{
    [SerializeField] private PoCTargetSelectionSettings targetSettings;
    
    private ITargetable _target;
    private PoCTargetScoreEvaluator _scoreEvaluator;
    
    // 탐색 결과를 재사용하는 리스트
    private readonly List<Collider2D> _overlapResults = new(32);

    // 점수를 계산할 타깃 후보 목록
    private readonly List<ITargetable> _targetPool = new(32);

    private ContactFilter2D _contactFilter;
    
    void Awake()
    {
        _scoreEvaluator = GetComponent<PoCTargetScoreEvaluator>();

        _contactFilter = new ContactFilter2D
        {
            useLayerMask = false,
            // layerMask = targetLayerMask, 추후 최적화를 위해 layer을 통한 필터링 가능
            useDepth = false,
            useNormalAngle = false,
            useTriggers = true
        };
    }
    
    public ITargetable  SelectTarget()
    {
        if (_scoreEvaluator == null || targetSettings == null)
            return null;
        
        CollectTargets();
        
        ITargetable bestTarget = null;
        float bestScore = float.MinValue;

        // 후보 중 점수가 가장 높은 타깃 탐색
        foreach (ITargetable candidate in _targetPool)
        {
            float score = _scoreEvaluator.CalculateScore(candidate);

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }
        
        // 선택 가능한 타깃이 없으면 기존 타깃 해제
        if (bestTarget == null)
        {
            _target = null;
            return null;
        }

        // 기존 타깃이 없거나 유효하지 않으면 즉시 선택
        if (_target == null ||
            !_target.IsTargetable ||
            !_targetPool.Contains(_target))
        {
            _target = bestTarget;
            return _target;
        }

        // 현재 타깃의 최신 점수
        float currentScore = _scoreEvaluator.CalculateScore(_target);

        // 최소 점수 차이를 넘었을 때만 변경
        if (bestScore >= currentScore + targetSettings.switchThreshold)
        {
            _target = bestTarget;
        }

        return _target;
    }

    private void CollectTargets()
    {
        // 기존 저장 공간을 유지하고 내용만 초기화
        _overlapResults.Clear();
        _targetPool.Clear();
        
        // 탐색 범위 내부의 Collider2D 수집
        Physics2D.OverlapCircle(
            transform.position,
            targetSettings.maxSearchDistance,
            _contactFilter,
            _overlapResults
        );

        foreach (Collider2D targetCollider in _overlapResults)
        {
            // Collider의 부모까지 확인
            PoCTargetable targetable =
                targetCollider.GetComponentInParent<PoCTargetable>();
            
            if (targetable == null || !targetable.IsTargetable)
                continue;
            
            // Collider가 여러 개인 오브젝트의 중복 등록 방지
            if (!_targetPool.Contains(targetable))
                _targetPool.Add(targetable);
        }
    }
}
