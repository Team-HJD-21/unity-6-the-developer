using UnityEngine;

[CreateAssetMenu(menuName = "The Developer/PoC AI Profile")]
public class PoCAIProfile : ScriptableObject
{
    [Header("타깃 선호 점수")]
    public float playerPreference; // 플레이어 선호 점수
    public float turretPreference; // 터렛 선호 점수
}