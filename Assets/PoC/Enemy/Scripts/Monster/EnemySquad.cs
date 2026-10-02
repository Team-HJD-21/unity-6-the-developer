using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SpawnInstruction 하나로 생성된 적의 소속과 공동 목표를 보관한다.
/// 목표 선택은 분대 단위로 수행하고, 구성원에게 같은 목표 Transform을 전달한다.
/// </summary>
public class EnemySquad
{
    private readonly List<EnemyController> _members;
    private ITargetable _target;

    /// <summary>
    /// 생성 직후 적을 등록할 빈 분대를 만든다.
    /// </summary>
    public EnemySquad()
    {
        _members = new List<EnemyController>();
    }

    /// <summary>
    /// 적을 구성원으로 등록하고 EnemyAIBrain의 개별 타깃 선택을 중지한다.
    /// </summary>
    /// <param name="enemy">이번 생성 명령으로 만들어진 적.</param>
    /// <returns>필수 컴포넌트가 있고 중복 등록되지 않았다면 true.</returns>
    public bool AddMember(EnemyController enemy)
    {
        if (enemy == null || _members.Contains(enemy))
            return false;

        EnemyAIBrain brain = enemy.GetComponent<EnemyAIBrain>();
        if (brain == null)
            return false;

        _members.Add(enemy);
        brain.AssignSquad(this);
        return true;
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

    /// <summary>
    /// 현재 목표가 공격 가능하면 유지하고, 아니면 분대 중심에서 새 목표를 찾는다.
    /// 목표가 바뀐 경우에만 모든 구성원에게 새 목표 또는 null을 전달한다.
    /// 후보가 없으면 목표를 비워 두고 다음 호출에서 다시 찾는다.
    /// </summary>
    /// <returns>선택되거나 유지된 공동 목표가 있으면 true.</returns>
    public bool SetTarget()
    {
        if (_target != null && _target.CanBeTargeted)
            return true;

        // 기존 목표가 없거나 공격 불가능할 때만 후보를 다시 검색한다.
        ITargetable previousTarget = _target;
        _target = null;

        if (TryGetCenterPosition(out Vector3 center) &&
            EnemySquadTargetSelector.TrySelectNearestTarget(center, out ITargetable nextTarget))
            _target = nextTarget;

        // 목표가 바뀐 경우에만 구성원에게 새 목표(또는 null)를 전달한다.
        if (!ReferenceEquals(previousTarget, _target))
        {
            Transform targetTransform = _target?.TargetTransform;
            foreach (EnemyController enemy in _members)
            {
                if (enemy != null)
                    enemy.SetTarget(targetTransform);
            }
        }

        return _target != null;
    }
}
