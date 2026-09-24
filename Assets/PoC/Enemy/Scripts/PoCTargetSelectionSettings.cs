using UnityEngine;

[CreateAssetMenu(menuName = "The Developer/PoC Target Selection Settings")]
public class PoCTargetSelectionSettings : ScriptableObject
{
    [Header("점수 가중치")] // 각 점수 요소는 0-1로 정규화 이후 아래 값과 multiply
    public float maxDistanceWeight = 40f;      // 거리 점수 최댓값
    public float maxLowHealthWeight = 20f;     // 낮은 체력 점수 최댓값
    public float maxThreatWeight = 20f;        // 위협도 점수 최댓값

    [Header("타깃 변경")]
    public float retargetInterval = 1f;  // 타깃 재평가 주기
    public float maxSearchDistance = 100f; // 최대 탐색 거리
    public float switchThreshold = 20f;  // 타깃 변경에 필요한 점수 차이
}
