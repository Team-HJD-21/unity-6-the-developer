using UnityEngine;

/// <summary>
/// 모든 적이 공유하는 타깃 점수와 재선택 기준을 보관한다.
/// </summary>
[CreateAssetMenu(menuName = "The Developer/PoC Target Selection Settings")]
public class PoCTargetSelectionSettings : ScriptableObject
{
    [Header("점수 가중치")] [Min(0f)] public float maxDistanceWeight = 40f;
    [Min(0f)] public float maxLowHealthWeight = 20f;
    [Min(0f)] public float maxThreatWeight = 15f;

    [Header("타깃 변경")] [Min(0f)] public float retargetInterval = 1f;
    [Min(0f)] public float maxSearchDistance = 15f;
    [Min(0f)] public float switchThreshold = 10f;
}