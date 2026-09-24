using UnityEngine;

/// <summary>
/// 타깃 선택에 사용되는 최종 점수를 계산한다.
/// AI 유형 선호도, 거리, 낮은 체력은 가산하고 높은 화력은 위험 요소로 감점한다.
/// </summary>
public class PoCTargetScoreEvaluator : MonoBehaviour
{
    [SerializeField] private PoCAIProfile targetPreference;
    [SerializeField] private PoCTargetSelectionSettings targetSetting;

    /// <summary>
    /// 유형 선호도와 거리, 체력, 화력을 기준으로 타깃 선택 점수를 계산한다.
    /// </summary>
    /// <param name="target">점수를 계산할 타깃.</param>
    /// <returns>선택 점수 또는 평가할 수 없을 때 음의 무한대.</returns>
    public float CalculateScore(ITargetable target)
    {
        // 파괴되었거나 선택할 수 없는 타깃과 설정 누락은 평가 대상에서 제외한다.
        if (target is not Object targetObject || targetObject == null || !target.CanBeTargeted ||
            targetPreference == null || targetSetting == null)
            return float.NegativeInfinity;

        // AI 유형별로 플레이어와 터렛에 서로 다른 기본 선호 점수를 적용한다.
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

        // 체력이 낮은 타깃일수록 높은 마무리 점수를 부여한다.
        float lowHealthRatio = 1f - Mathf.Clamp01(target.HealthRatio);
        float healthScore = lowHealthRatio * targetSetting.maxLowHealthWeight;

        // 화력이 높은 타깃은 위험하므로 선택 점수에서 감점한다.
        float threatPenalty = Mathf.Clamp01(target.PowerRatio) * targetSetting.maxThreatWeight;

        return preferenceScore + distanceScore + healthScore - threatPenalty;
    }
}
