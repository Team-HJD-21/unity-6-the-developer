using UnityEngine;

public class PoCTargetScoreEvaluator : MonoBehaviour
{
    [SerializeField] private PoCAIProfile targetPreference;
    [SerializeField] private PoCTargetSelectionSettings targetSetting;
    
    public float CalculateScore(ITargetable target)
    {
        float preferenceScore;
        float distanceScore;
        float healthScore;
        float powerScore;
        
        if (!target.IsTargetable) return 0;
        
        // 적 선호도 //
        preferenceScore = (target.TargetType == TargetType.Player)
            ? targetPreference.playerPreference
            : targetPreference.turretPreference;
        
        // 거리 비례 //
        float diffDistance = (transform.position - target.TargetTransform.position).magnitude;
        distanceScore = (diffDistance / targetSetting.maxSearchDistance) * targetSetting.maxDistanceWeight;

        // 체력 비례 //
        // 체력이 낮을수록 1에 가까워짐
        float lowHealthRatio = 1f - target.HealthRatio;
        // 낮은 체력 점수를 최종 점수에 추가
        healthScore = lowHealthRatio * targetSetting.maxLowHealthWeight;
        
        // 화력 비례 //
        powerScore = target.PowerRatio * targetSetting.maxThreatWeight;
        
        // 최종 점수
        float totalScore = preferenceScore + distanceScore + healthScore + powerScore;

        return totalScore;
    }
}
