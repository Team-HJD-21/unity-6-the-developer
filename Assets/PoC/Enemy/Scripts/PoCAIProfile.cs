using UnityEngine;

/// <summary>
/// AI 유형마다 달라지는 플레이어와 터렛의 기본 선호 점수를 보관한다.
/// 모든 AI가 공유하는 계산 기준은 <see cref="PoCTargetSelectionSettings"/>에서 관리한다.
/// </summary>
[CreateAssetMenu(menuName = "The Developer/PoC AI Profile")]
public class PoCAIProfile : ScriptableObject
{
    /// <summary>
    /// 플레이어 타깃에 기본으로 더할 선호 점수다.
    /// </summary>
    [Header("타깃 선호 점수")] public float playerPreference;

    /// <summary>
    /// 터렛 타깃에 기본으로 더할 선호 점수다.
    /// </summary>
    public float turretPreference;
}
