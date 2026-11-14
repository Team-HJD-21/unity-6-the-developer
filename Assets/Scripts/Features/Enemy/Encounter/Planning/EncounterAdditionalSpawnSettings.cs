// 상황 기반 내부 출현의 시험값을 한 묶음으로 둡니다. 정규 생산에 예산을 추가하지 않습니다.
// 기존 편성 한 개를 같은 비용 예산 안에서 지연 출현으로 옮길 때만 사용합니다.
using System;

[Serializable]
public sealed class EncounterAdditionalSpawnSettings
{
    public bool enabled = true;
    public double maximumBudgetShare = .5;
    public double minimumNetCaptureRatio = .15;
    public int minimumPositiveChanges = 2;
    public double minimumObservedSeconds = 30;
    public double cooldownSeconds = 90;
    public double warningSeconds = 3;
    public double minimumPlayerDistance = 8;

    public bool IsValid => double.IsFinite(maximumBudgetShare) && maximumBudgetShare > 0 && maximumBudgetShare <= .5 &&
        double.IsFinite(minimumNetCaptureRatio) && minimumNetCaptureRatio > 0 && minimumNetCaptureRatio <= 1 &&
        minimumPositiveChanges >= 2 && double.IsFinite(minimumObservedSeconds) && minimumObservedSeconds > 0 &&
        double.IsFinite(cooldownSeconds) && cooldownSeconds > 0 && double.IsFinite(warningSeconds) && warningSeconds >= .25 &&
        double.IsFinite(minimumPlayerDistance) && minimumPlayerDistance >= 0;
}
