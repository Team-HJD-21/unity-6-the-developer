using UnityEngine;

/// <summary>
/// 적 유형별 타깃 선호 점수를 보관한다.
/// </summary>
[CreateAssetMenu(menuName = "The Developer/PoC AI Profile")]
public class PoCAIProfile : ScriptableObject
{
    [Header("타깃 선호 점수")] public float playerPreference;
    public float turretPreference;
}