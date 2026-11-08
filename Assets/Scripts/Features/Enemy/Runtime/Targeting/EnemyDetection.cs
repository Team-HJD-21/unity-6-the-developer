using System;
using System.Collections.Generic;
using TeamHJD.Game.Enemy.Contracts;
using UnityEngine;

/// <summary>플레이어별 감지 등록을 관리하고 탐지 상태를 제공한다.</summary>
public class EnemyDetection : MonoBehaviour, IDetectableTarget
{
    private readonly HashSet<int> _detectorIds = new();
    private bool _isDetected;

    public bool IsDetected => _isDetected;

    /// <summary>탐지 여부가 바뀌었을 때 변경된 상태를 전달한다.</summary>
    public event Action<bool> DetectionChanged;
    
    /// <summary>
    /// 감지자를 중복 없이 등록하고 한 명 이상 감지 중이면 탐지 상태를 유지한다.
    /// </summary>
    public void RegisterDetector(int detectorId)
    {
        _detectorIds.Add(detectorId);
        ApplyDetectionState(_detectorIds.Count > 0);
    }

    /// <summary>
    /// 해당 감지자를 해제하고 마지막 감지자까지 사라졌을 때 탐지를 해제한다.
    /// </summary>
    public void UnregisterDetector(int detectorId)
    {
        _detectorIds.Remove(detectorId);
        ApplyDetectionState(_detectorIds.Count > 0);
    }

    /// <summary>서버의 계산 결과 또는 Client가 받은 탐지 상태를 적용한다.</summary>
    internal void ApplyDetectionState(bool detected)
    {
        if (_isDetected == detected)
            return;

        _isDetected = detected;
        DetectionChanged?.Invoke(detected);
    }
    
    private void OnDisable()
    {
        _detectorIds.Clear();
        ApplyDetectionState(false);
    }
}
