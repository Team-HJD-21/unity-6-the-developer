using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 함께 행동하는 적의 소속과 공동 목표를 보관한다.
/// 목표 선택은 분대 단위로 수행하고, 구성원에게 같은 목표 Transform을 전달한다.
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
        _target = IsValidTarget(target) ? target : null;
    }

    /// <summary>
    /// 적을 분대 구성원으로 등록하고 유효한 기존 타깃을 전달한다.
    /// </summary>
    /// <param name="enemy">분대에 등록할 적.</param>
    /// <returns>적이 유효하고 아직 등록되지 않았다면 true.</returns>
    public bool AddMember(EnemyController enemy)
    {
        if (enemy == null || _members.Contains(enemy))
            return false;

        if (_squadMode == EnemySquadMode.Individual && !IsEmpty)
            return false;

        _members.Add(enemy);
        
        Transform targetTransform =
            IsValidTarget(_target)
                ? _target.TargetTransform
                : null;
        
        enemy.AssignTarget(targetTransform);
        return true;
    }

    /// <summary>
    /// 네트워크에서 제거된 적을 분대원 목록에서 해제한다.
    /// </summary>
    /// <param name="enemy">제거할 적의 행동 컴포넌트.</param>
    public void RemoveMember(EnemyController enemy)
    {
        _members.Remove(enemy);
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
    /// 저장된 공동 목표를 다시 연결하고 구성원에게 전달한다.
    /// </summary>
    internal void RestoreCheckpointTarget(Transform target)
    {
        _target = target != null ? target.GetComponent<ITargetable>() : null;
        if (target != null && _target == null) throw new System.InvalidOperationException("Saved squad target is not targetable.");
        foreach (var member in _members) member.AssignTarget(target);
    }

    /// <summary>
    /// 현재 목표가 공격 가능하면 유지하고, 아니면 분대 중심에서 새 목표를 찾는다.
    /// 목표가 바뀐 경우에만 모든 구성원에게 새 목표 또는 null을 전달한다.
    /// 후보가 없으면 목표를 비워 두고 분대 단위로 검색 간격을 둔다.
    /// </summary>
    /// <returns>선택되거나 유지된 공동 목표가 있으면 true.</returns>
    public bool SetTarget()
    {
        if (IsValidTarget(_target))
            return true;

        // 기존 목표가 없거나 공격 불가능할 때만 후보를 다시 검색한다.
        ITargetable previousTarget = _target;
        _target = null;

        if (Time.time >= _nextSearchTime && TryGetCenterPosition(out Vector3 center))
        {
            _nextSearchTime = Time.time + RetargetInterval;
            ITargetable nextTarget;
            bool found = _targetRule == EnemyTargetRule.PlayerOnly
                ? EnemySquadTargetSelector.TrySelectNearestPlayer(center, out nextTarget)
                : EnemySquadTargetSelector.TrySelectNearestTarget(center, out nextTarget);

            if (found)
                _target = nextTarget;
        }

        // 목표가 바뀐 경우에만 구성원에게 새 목표(또는 null)를 전달한다.
        if (!ReferenceEquals(previousTarget, _target))
        {
            Transform targetTransform = _target?.TargetTransform;
            foreach (EnemyController enemy in _members)
            {
                if (enemy != null)
                    enemy.AssignTarget(targetTransform);
            }
        }

        return _target != null;
    }

    /// <summary>구성원이 감지한 플레이어를 분대 공동 목표로 선택한다.</summary>
    public bool TryPrioritizePlayer(EnemyController requester, ITargetable player)
    {
        if (!_members.Contains(requester) ||
            !IsValidTarget(player) ||
            player.TargetType != TargetType.Player)
        {
            return false;
        }

        // 이미 선택한 플레이어는 유효한 동안 유지한다.
        if (IsValidTarget(_target) && _target.TargetType == TargetType.Player)
            return false;

        _target = player;
        foreach (EnemyController member in _members)
        {
            if (member != null)
                member.AssignTarget(player.TargetTransform);
        }

        return true;
    }

    /// <summary>활성 구성원 중 첫 번째 몬스터만 공동 타깃 갱신을 실행한다.</summary>
    public void RefreshTarget(EnemyController requester)
    {
        foreach (EnemyController member in _members)
        {
            if (member == null || !member.isActiveAndEnabled)
                continue;

            if (member == requester)
                SetTarget();
            return;
        }
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
