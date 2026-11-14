// 현재 Region 원본 옆에서 최근 변경만 최대64개 보관합니다. 프레임마다 표본을 추가하지 않습니다.
// 시간/Match/Grid 세대가 바뀌면 기준을 다시 잡아 초기 배치·rollback을 급격한 확장으로 세지 않습니다.
// 고정 창의 순증가와 양의 변화 횟수만 공급하며 추가 출현의 기준/예산은 Enemy Policy가 결정합니다.
using System;
using System.Collections.Generic;

namespace TeamHJD.Game.Domain
{
    public sealed class RegionCaptureHistory
    {
        private readonly struct Sample
        {
            public double Seconds { get; }
            public int Cells { get; }
            public Sample(double seconds, int cells) { Seconds = seconds; Cells = cells; }
        }

        private readonly List<Sample> _samples = new List<Sample>();
        private readonly int _capacity;
        private readonly double _window;
        private MatchId _match;
        private Guid _grid, _epoch;
        private double _lastSeconds;
        public RegionCaptureTrend Current { get; private set; }
        public int RetainedSamples => _samples.Count;

        public RegionCaptureHistory(double windowSeconds = 120, int capacity = 64)
        {
            if (!double.IsFinite(windowSeconds) || windowSeconds <= 0 || capacity < 2 || capacity > 256)
                throw new ArgumentOutOfRangeException(nameof(windowSeconds));
            _window = windowSeconds;
            _capacity = capacity;
        }

        /// <summary>권위 공간 갱신/요청 수집이 같은 Field 시각으로 관측합니다. 해금만 바뀌면 표본은 늘지 않습니다.</summary>
        public RegionCaptureTrend Observe(RegionGridSnapshot region, Guid epoch, double gameSeconds)
        {
            if (region == null) throw new ArgumentNullException(nameof(region));
            if (epoch == Guid.Empty || !double.IsFinite(gameSeconds) || gameSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));

            bool newBaseline = _samples.Count == 0 || _match != region.MatchId || _grid != region.GridId ||
                _epoch != epoch || gameSeconds < _lastSeconds;
            if (newBaseline)
            {
                _samples.Clear();
                _match = region.MatchId;
                _grid = region.GridId;
                _epoch = epoch;
                _samples.Add(new Sample(gameSeconds, region.CapturedCellCount));
            }
            else if (_samples[_samples.Count - 1].Cells != region.CapturedCellCount)
            {
                // 같은 수집 시각의 여러 확정은 한 관측으로 합칩니다. 회차 수를 인위적으로 늘리지 않습니다.
                if (_samples[_samples.Count - 1].Seconds == gameSeconds)
                    _samples[_samples.Count - 1] = new Sample(gameSeconds, region.CapturedCellCount);
                else
                    _samples.Add(new Sample(gameSeconds, region.CapturedCellCount));
            }

            _lastSeconds = gameSeconds;
            double earliest = gameSeconds - _window;
            // 창 직전의 마지막 상태 하나는 그 경계의 점유 기준으로 남깁니다.
            while (_samples.Count > 1 && (_samples[1].Seconds <= earliest || _samples.Count > _capacity))
                _samples.RemoveAt(0);

            int positive = 0;
            for (int i = 1; i < _samples.Count; i++)
                if (_samples[i].Cells > _samples[i - 1].Cells) positive++;
            int eligible = region.Definition.CapturableCellCount;
            double observed = gameSeconds - Math.Max(_samples[0].Seconds, earliest);
            double net = eligible == 0 ? 0 : (double)(region.CapturedCellCount - _samples[0].Cells) / eligible;
            Current = new RegionCaptureTrend(epoch, gameSeconds, observed, region.CaptureRatio, net, positive);
            return Current;
        }

        /// <summary>정의 Reset/해제 시 과거 추세를 버립니다. 현재 점유는 RegionRuntime만 소유합니다.</summary>
        public void Clear()
        {
            _samples.Clear();
            Current = default;
            _epoch = Guid.Empty;
            _grid = Guid.Empty;
            _match = default;
            _lastSeconds = 0;
        }
    }
}
