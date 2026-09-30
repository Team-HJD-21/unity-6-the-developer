using UnityEngine;

/// <summary>
/// 모든 적 AI가 공유하는 점수 가중치와 타깃 재선택 기준을 보관한다.
/// AI 유형별 선호 점수는 <see cref="PoCAIProfile"/>에서 별도로 관리한다.
/// </summary>
[CreateAssetMenu(menuName = "The Developer/PoC Target Selection Settings")]
public class PoCTargetSelectionSettings : ScriptableObject
{
    /// <summary>
    /// 가장 가까운 타깃이 받을 수 있는 최대 거리 점수다.
    /// </summary>
    [Header("점수 가중치")] [Min(0f)] public float maxDistanceWeight = 40f;

    /// <summary>
    /// 체력이 가장 낮은 타깃이 받을 수 있는 최대 체력 점수다.
    /// </summary>
    [Min(0f)] public float maxLowHealthWeight = 20f;

    /// <summary>
    /// 화력이 가장 높은 타깃에서 차감할 최대 위협 점수다.
    /// </summary>
    [Min(0f)] public float maxThreatWeight = 15f;

    /// <summary>
    /// 타깃 점수를 다시 평가하는 시간 간격이다.
    /// </summary>
    [Header("타깃 변경")] [Min(0f)] public float retargetInterval = 1f;

    /// <summary>
    /// 타깃 후보를 탐색할 최대 거리다.
    /// </summary>
    [Min(0f)] public float maxSearchDistance = 15f;

    /// <summary>
    /// 기존 타깃을 교체하기 위해 필요한 최소 점수 차이다.
    /// </summary>
    [Min(0f)] public float switchThreshold = 10f;
}
