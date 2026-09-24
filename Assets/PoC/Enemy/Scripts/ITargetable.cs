using UnityEngine;

/// <summary>
/// 플레이어와 터렛이 적 AI의 타깃 후보가 되기 위해 제공해야 하는 정보다.
/// 타깃 선택 로직이 각 대상의 구체적인 구현을 몰라도 동일하게 평가할 수 있도록 한다.
/// </summary>
public interface ITargetable
{
    /// <summary>
    /// AI가 바라보거나 이동할 위치를 반환한다.
    /// </summary>
    Transform TargetTransform { get; }

    /// <summary>
    /// 플레이어, 터렛 등 타깃의 종류를 반환한다.
    /// </summary>
    TargetType TargetType { get; }

    /// <summary>
    /// 현재 체력 비율을 0에서 1 사이로 반환한다.
    /// </summary>
    float HealthRatio { get; }

    /// <summary>
    /// 현재 화력 비율을 0에서 1 사이로 반환한다.
    /// </summary>
    float FirepowerRatio { get; }

    /// <summary>
    /// 현재 AI가 공격 대상으로 선택할 수 있는지 반환한다.
    /// </summary>
    bool CanBeTargeted { get; }
}
