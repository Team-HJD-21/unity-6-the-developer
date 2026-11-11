using System.Collections.Generic;
using UnityEngine;
using RuntimeTargetSelector = TeamHJD.Game.Enemies.Targeting.EnemyTargetSelector;

/// <summary>
/// 함께 행동하는 적의 소속과 공동 목표를 보관한다.
/// 목표 선택은 분대 단위로 수행하고, 구성원에게 같은 목표 Transform을 전달한다.
/// 집계 제외 구성원도 목표를 공유하며 공격 인원에는 포함하지 않는다.
/// </summary>
public class EnemySquad
{
    private const float RetargetInterval = 0.5f;

    private readonly List<EnemyController> _members;
    private ITargetable _target;
    private readonly EnemySquadMode _squadMode;
    private readonly EnemyTargetRule _targetRule;
    private float _nextSearchTime;

    /// <summary>
    /// 등록된 분대원이 모두 제거되었는지 반환한다.
    /// </summary>
    public bool IsEmpty => _members.Count == 0;

    /// <summary>
    /// 생성 직후 적을 등록할 빈 분대를 만든다.
    /// </summary>
    public EnemySquad() : this(EnemySquadMode.Shared, EnemyTargetRule.PlayerOrTurret)
    {
    }

    /// <summary>분대 구성 방식과 타깃 선택 규칙을 지정한다.</summary>
    public EnemySquad(
        EnemySquadMode squadMode,
        EnemyTargetRule targetRule,
        Transform initialTarget = null)
    {
        _members = new List<EnemyController>();
        _squadMode = squadMode;
        _targetRule = targetRule;

        ITargetable target = initialTarget != null
            ? initialTarget.GetComponent<ITargetable>()
            : null;
        TryChangeTarget(IsValidTarget(target) ? target : null);
    }

    /// <summary>
    /// 구성원을 등록하고 집계 대상이면 기존 타깃에 한 명을 추가 배정한다.
    /// </summary>
    /// <param name="enemy">분대에 등록할 적.</param>
    /// <returns>유효한 적이 분대 구성 규칙에 따라 등록되었으면 true.</returns>
    public bool AddMember(EnemyController enemy)
    {
        if (enemy == null || _members.Contains(enemy))
            return false;

        if (_squadMode == EnemySquadMode.Individual && !IsEmpty)
            return false;

        if (_target != null)
        {
            bool cannotAssign = !IsValidTarget(_target) ||
                (enemy.IsIncludedInAttackerLimit && !_target.TryAssignAttackers(1));

            if (cannotAssign)
            {
                // 목록을 늘리기 전에 기존 집계 대상의 배정을 반환한다.
                TryChangeTarget(null);
                _nextSearchTime = 0f;
            }
        }

        _members.Add(enemy);
        enemy.AssignTarget(_target?.TargetTransform);
        return true;
    }

    /// <summary>
    /// 실제 제거된 구성원이 집계 대상이면 한 명을 반환하고 목록에서 해제한다.
    /// </summary>
    /// <param name="enemy">제거할 적의 행동 컴포넌트.</param>
    public void RemoveMember(EnemyController enemy)
    {
        if (ReferenceEquals(enemy, null) || !_members.Remove(enemy))
            return;

        // 목록에서 제거된 경우에만 반환해 중복 제거 요청의 이중 차감을 막는다.
        if (enemy.IsIncludedInAttackerLimit)
            _target?.UnassignAttackers(1);

        if (IsEmpty)
            TryChangeTarget(null);
    }

    /// <summary>
    /// 현재 참조할 수 있는 구성원의 월드 위치를 평균 내 분대 중심을 구한다.
    /// </summary>
    /// <param name="center">참조 가능한 구성원이 있을 때 계산한 분대 중심.</param>
    /// <returns>참조 가능한 구성원이 한 명 이상이면 true.</returns>
    public bool TryGetCenterPosition(out Vector3 center)
    {
        center = Vector3.zero;
        int validMemberCount = 0;

        foreach (EnemyController enemy in _members)
        {
            if (enemy == null)
                continue;

            center += enemy.transform.position;
            validMemberCount++;
        }

        if (validMemberCount == 0)
            return false;

        center /= validMemberCount;
        return true;
    }

    internal Transform CheckpointTarget => _target?.TargetTransform;

    /// <summary>
    /// 저장된 목표를 다시 연결한다. 다른 목표이면 기존 배정부터 반환한다.
    /// </summary>
    internal void RestoreCheckpointTarget(Transform target)
    {
        ITargetable restoredTarget = target != null
            ? target.GetComponent<ITargetable>()
            : null;

        if (target != null && restoredTarget == null)
        {
            throw new System.InvalidOperationException(
                "Saved squad target is not targetable.");
        }

        ITargetable nextTarget = IsValidTarget(restoredTarget) ? restoredTarget : null;

        if (!ReferenceEquals(_target, nextTarget))
        {
            // 복원 대상이 포화 상태이면 이전 목표로 돌아가지 않고 재검색을 기다린다.
            TryChangeTarget(null);
            TryChangeTarget(nextTarget);
        }

        _nextSearchTime = 0f;
    }

    /// <summary>
    /// 배정된 목표는 유지하고, 목표가 없을 때 분대 중심에서 새 목표를 찾는다.
    /// 타깃 불가 전환은 이벤트로 처리하며 후보가 없을 때만 검색 간격을 둔다.
    /// </summary>
    /// <returns>선택되거나 유지된 공동 목표가 있으면 true.</returns>
    public bool SetTarget()
    {
        if (_target != null)
            return true;

        if (Time.time < _nextSearchTime)
            return false;

        _nextSearchTime = Time.time + RetargetInterval;

        if (!TryGetCenterPosition(out Vector3 center))
            return false;

        TargetType? requiredType = _targetRule == EnemyTargetRule.PlayerOnly
            ? TargetType.Player
            : null;

        TargetableComponent[] candidates =
            Object.FindObjectsByType<TargetableComponent>(FindObjectsSortMode.None);

        bool found = RuntimeTargetSelector.TryFindNearest(
            center, candidates, requiredType, out ITargetable nextTarget, GetAttackerCount());

        if (!found)
            return false;

        return TryChangeTarget(nextTarget);
    }

    /// <summary>플레이어 우선 전환 규칙을 확인하고 공통 타깃 변경을 요청한다.</summary>
    /// <param name="requester">플레이어를 감지한 현재 분대원.</param>
    /// <param name="player">감지 영역에서 전달받은 플레이어 후보.</param>
    /// <returns>플레이어로 전환했으면 true. 이미 플레이어를 추적 중이면 false.</returns>
    public bool TryPrioritizePlayer(EnemyController requester, ITargetable player)
    {
        // 이미 선택한 플레이어는 타깃 불가 이벤트가 발생할 때까지 유지한다.
        if (_target != null && _target.TargetType == TargetType.Player)
            return false;

        if (!_members.Contains(requester) ||
            player == null ||
            player.TargetType != TargetType.Player)
        {
            return false;
        }

        // 플레이어로 우선 전환할 때는 배정 실패 시 기존 유효 타깃을 유지한다.
        return TryChangeTarget(player);
    }

    /// <summary>목표가 없고 재검색 시점일 때 대표 활성 구성원만 검색한다.</summary>
    public void RefreshTarget(EnemyController requester)
    {
        if (_target != null || Time.time < _nextSearchTime)
            return;

        foreach (EnemyController member in _members)
        {
            if (member == null || !member.isActiveAndEnabled)
                continue;

            if (member == requester)
                SetTarget();
            return;
        }
    }

    /// <summary>검색·타깃 변경 시에만 집계 대상 구성원 수를 계산한다.</summary>
    private int GetAttackerCount()
    {
        int count = 0;

        foreach (EnemyController member in _members)
        {
            if (member != null && member.IsIncludedInAttackerLimit)
                count++;
        }

        return count;
    }

    /// <summary>새 타깃에 집계 대상 인원을 배정하고 기존 배정을 반환한다.</summary>
    /// <param name="target">새 목표. null이면 기존 목표와 배정을 해제한다.</param>
    /// <returns>타깃 변경 또는 해제에 성공했으면 true.</returns>
    private bool TryChangeTarget(ITargetable target)
    {
        if (ReferenceEquals(_target, target))
            return true;

        int count = GetAttackerCount();

        if (target != null)
        {
            if (!IsValidTarget(target))
                return false;

            // 새 배정을 먼저 확보해 플레이어 우선 전환 실패 시 기존 배정을 유지한다.
            // 구성원이 없는 초기 상태와 집계 제외 분대는 인원 배정 호출을 생략한다.
            if (count > 0 && !target.TryAssignAttackers(count))
                return false;
        }

        if (!ReferenceEquals(_target, null))
        {
            _target.TargetabilityLost -= OnTargetabilityLost;
            _target.UnassignAttackers(count);
        }

        _target = target;

        if (!ReferenceEquals(_target, null))
            _target.TargetabilityLost += OnTargetabilityLost;

        Transform targetTransform = _target?.TargetTransform;

        foreach (EnemyController member in _members)
        {
            if (member != null)
                member.AssignTarget(targetTransform);
        }

        return true;
    }

    /// <summary>현재 타깃을 해제하고 즉시 새 타깃을 찾는다.</summary>
    private void OnTargetabilityLost(ITargetable target)
    {
        if (!ReferenceEquals(_target, target))
            return;

        // 공격 불가능한 기존 타깃과 배정을 먼저 해제한 뒤 새 후보를 찾는다.
        TryChangeTarget(null);

        _nextSearchTime = 0f;
        SetTarget();
    }

    /// <summary>
    /// Unity 오브젝트 수명과 타깃 유효성을 확인하고 분대의 PlayerOnly 규칙을 적용한다.
    /// </summary>
    private bool IsValidTarget(ITargetable target)
    {
        if (target == null ||
            (target is Object targetObject && targetObject == null) ||
            !target.CanBeTargeted || target.TargetTransform == null)
        {
            return false;
        }

        return _targetRule != EnemyTargetRule.PlayerOnly ||
               target.TargetType == TargetType.Player;
    }
}
