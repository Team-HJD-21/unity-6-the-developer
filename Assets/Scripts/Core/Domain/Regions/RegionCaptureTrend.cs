// 최근 점유 변화의 작은 읽기 값입니다. 현재 점유 원본이나 Mission 최고 진행률을 대체하지 않습니다.
// 초기 CU/복원 표본은 성장 사건이 아니며 NetRatio는 상실 때 음수일 수 있습니다.
using System;

namespace TeamHJD.Game.Domain
{
    public readonly struct RegionCaptureTrend
    {
        public Guid Epoch { get; }
        public double GameSeconds { get; }
        public double ObservedSeconds { get; }
        public double CurrentRatio { get; }
        public double NetRatio { get; }
        public int PositiveChanges { get; }
        public bool HasHistory => Epoch != Guid.Empty && ObservedSeconds > 0;
        public RegionCaptureTrend(Guid epoch, double seconds, double observed, double ratio, double net, int positiveChanges)
        {
            if (epoch == Guid.Empty || !double.IsFinite(seconds) || seconds < 0 || !double.IsFinite(observed) || observed < 0 ||
                !double.IsFinite(ratio) || ratio < 0 || ratio > 1 || !double.IsFinite(net) || net < -1 || net > 1 || positiveChanges < 0)
                throw new ArgumentOutOfRangeException(nameof(seconds));
            Epoch = epoch;
            GameSeconds = seconds;
            ObservedSeconds = observed;
            CurrentRatio = ratio;
            NetRatio = net;
            PositiveChanges = positiveChanges;
        }
    }
}
