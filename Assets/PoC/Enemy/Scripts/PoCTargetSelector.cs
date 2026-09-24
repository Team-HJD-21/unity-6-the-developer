using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 탐색 범위 안의 유효한 후보를 평가하고 변경 기준에 따라 타깃을 선택한다.
/// </summary>
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

    private void Awake()
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

    public ITargetable SelectTarget()
    {
        if (_scoreEvaluator == null || targetSettings == null)
            return null;

        CollectTargets();

        ITargetable bestTarget = null;
        float bestScore = float.NegativeInfinity;

        // 후보 중 점수가 가장 높은 타깃을 탐색한다.
        foreach (ITargetable candidate in _targetPool)
        {
            float score = _scoreEvaluator.CalculateScore(candidate);

            if (score > bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }

        // 선택 가능한 타깃이 없으면 기존 타깃을 해제한다.
        if (bestTarget == null)
        {
            _target = null;
            return null;
        }

        // 기존 타깃이 없거나 유효하지 않으면 즉시 선택한다.
        if (!IsCurrentTargetValid())
        {
            _target = bestTarget;
            return _target;
        }

        // 현재 타깃의 최신 점수를 계산한다.
        float currentScore = _scoreEvaluator.CalculateScore(_target);

        // 최소 점수 차이를 넘었을 때만 변경한다.
        if (bestTarget != _target &&
            bestScore >= currentScore + targetSettings.switchThreshold)
            _target = bestTarget;

        return _target;
    }

    private bool IsCurrentTargetValid()
    {
        return _target is Object targetObject &&
               targetObject != null &&
               _target.CanBeTargeted &&
               _targetPool.Contains(_target);
    }

    private void CollectTargets()
    {
        // 기존 저장 공간을 유지하고 내용만 초기화한다.
        _overlapResults.Clear();
        _targetPool.Clear();

        // 탐색 범위 내부의 Collider2D를 수집한다.
        Physics2D.OverlapCircle(
            transform.position,
            targetSettings.maxSearchDistance,
            _contactFilter,
            _overlapResults
        );

        foreach (Collider2D targetCollider in _overlapResults)
        {
            // Collider의 부모까지 확인한다.
            PoCTargetable targetable =
                targetCollider.GetComponentInParent<PoCTargetable>();

            if (targetable == null || !targetable.CanBeTargeted)
                continue;

            // Collider가 여러 개인 오브젝트의 중복 등록을 방지한다.
            if (!_targetPool.Contains(targetable))
                _targetPool.Add(targetable);
        }
    }
}