using UnityEngine;

/// <summary>
/// 타깃의 거리, 체력, 화력과 유형 선호도를 합산해 선택 점수를 계산한다.
/// </summary>
public class PoCTargetScoreEvaluator : MonoBehaviour
{
    [SerializeField] private PoCAIProfile targetPreference;
    [SerializeField] private PoCTargetSelectionSettings targetSetting;

    public float CalculateScore(ITargetable target)
    {
        if (target is not Object targetObject || targetObject == null || !target.IsTargetable ||
            targetPreference == null || targetSetting == null)
            return float.NegativeInfinity;

        float preferenceScore = target.TargetType == TargetType.Player
            ? targetPreference.playerPreference
            : targetPreference.turretPreference;

        // 가까운 타깃일수록 높은 거리 점수를 부여한다.
        float diffDistance = (transform.position - target.TargetTransform.position).magnitude;
        float searchDistance = Mathf.Max(targetSetting.maxSearchDistance, Mathf.Epsilon);
        float distanceRatio = 1f - Mathf.Clamp01(
            diffDistance / searchDistance
        );
        float distanceScore = distanceRatio * targetSetting.maxDistanceWeight;

        float lowHealthRatio = 1f - Mathf.Clamp01(target.HealthRatio);
        float healthScore = lowHealthRatio * targetSetting.maxLowHealthWeight;

        // 화력이 높은 타깃은 위험하므로 선택 점수에서 감점한다.
        float threatPenalty = Mathf.Clamp01(target.PowerRatio) * targetSetting.maxThreatWeight;

        return preferenceScore + distanceScore + healthScore - threatPenalty;
    }
}