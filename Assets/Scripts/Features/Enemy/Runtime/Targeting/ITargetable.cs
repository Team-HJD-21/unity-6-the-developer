using System;
using UnityEngine;

/// <summary>
/// 플레이어와 터렛이 적 AI의 타깃 후보가 되기 위해 제공해야 하는 정보다.
/// 타깃 선택 로직이 각 대상의 구체적인 구현을 몰라도 동일하게 평가할 수 있도록 한다.
/// </summary>
public interface ITargetable
{
    /// <summary>타깃 가능한 상태에서 불가능한 상태로 전환되면 발생한다.</summary>
    event Action<ITargetable> TargetabilityLost;

    /// <summary>
    /// AI가 바라보거나 이동할 위치를 반환한다.
    /// </summary>
    Transform TargetTransform { get; }

    /// <summary>
    /// 플레이어, 터렛 등 타깃의 종류를 반환한다.
    /// </summary>
    TargetType TargetType { get; }

    /// <summary>이동 중인 적을 포함해 이 타깃에 배정할 수 있는 최대 집계 인원을 반환한다.</summary>
    int MaxAttackers { get; }

    /// <summary>모든 분대가 이 타깃에 배정한 집계 대상 인원의 합을 반환한다.</summary>
    int AssignedAttackerCount { get; }

    /// <summary>수용 가능한 경우 요청한 인원을 배정한다.</summary>
    /// <param name="count">추가할 양수 인원. 제외 대상은 이 호출에서 집계하지 않는다.</param>
    /// <returns>타깃이 유효하고 요청한 인원 전체를 배정했으면 true.</returns>
    bool TryAssignAttackers(int count);

    /// <summary>기존에 배정된 인원을 해제한다.</summary>
    /// <param name="count">호출자가 이전에 배정했던 인원 중 반환할 수.</param>
    void UnassignAttackers(int count);

    /// <summary>
    /// 현재 체력 비율을 0에서 1 사이로 반환한다.
    /// </summary>
    float HealthRatio { get; }

    /// <summary>
    /// 현재 이론 DPS / 활성화 전력 효율을 반환한다. 0~1 정규화 값이 아니다.
    /// </summary>
    float FirepowerRatio { get; }

    /// <summary>
    /// 현재 AI가 공격 대상으로 선택할 수 있는지 반환한다.
    /// </summary>
    bool CanBeTargeted { get; }
}
