using UnityEngine;

public interface ITargetable
{
    // AI가 바라보거나 이동할 위치
    Transform TargetTransform { get; }

    // 플레이어, 터렛 등 타깃 종류
    TargetType TargetType { get; }

    // 현재 체력 비율 (0~1)
    float HealthRatio { get; }
 
    // 현재 화력 비율
    float PowerRatio { get; }
    
    // 현재 공격 가능한 타깃인지 여부
    bool IsTargetable { get; }
}