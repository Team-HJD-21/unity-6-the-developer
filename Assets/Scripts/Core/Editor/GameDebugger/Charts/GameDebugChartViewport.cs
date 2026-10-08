// 시간·수량 축의 표시 범위와 최신 추적·자동 Y 스케일을 관리합니다.
// GUI와 독립적인 값 객체이며 수동 탐색은 최신 추적을 명시적으로 해제합니다.
using System;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugChartViewport
    {
        public double StartSeconds;
        public double SpanSeconds=60;
        public double MinimumY;
        public double MaximumY=10;
        public bool IsFollowingLatest=true;
        public bool IsAutoY=true;
        public bool IsTrafficView=true;
        public bool IsRequestsVisible=true, IsSpawnsVisible=true, IsDeathsVisible=true, IsRemovalsVisible;
        public double EndSeconds => StartSeconds+SpanSeconds;
        public double SpanY => MaximumY-MinimumY;

        public void Validate()
        {
            SpanSeconds=ClampFinite(SpanSeconds,1,86400,60);
            StartSeconds=ClampFinite(StartSeconds,0,1e9,0);
            MinimumY=ClampFinite(MinimumY,0,1e12,0);
            MaximumY=ClampFinite(MaximumY,MinimumY+0.01,1e12,MinimumY+10);
        }
        public void UpdateDuration(double duration)
        {
            Validate(); duration=ClampFinite(duration,0,1e9,0);
            double latest=Math.Max(0,duration-SpanSeconds);
            StartSeconds=IsFollowingLatest ? latest : Math.Min(StartSeconds,latest);
        }
        public void PanTime(double start,double duration)
        {
            IsFollowingLatest=false;
            StartSeconds=Math.Max(0,Math.Min(start,Math.Max(0,duration-SpanSeconds)));
        }
        public void ZoomTime(double factor,double duration)
        {
            double center=StartSeconds+SpanSeconds/2;
            SpanSeconds=ClampFinite(SpanSeconds*factor,1,86400,60);
            if (!IsFollowingLatest) StartSeconds=Math.Max(0,center-SpanSeconds/2);
            UpdateDuration(duration);
        }
        public void FitY(double visibleMaximum)
        {
            if (!IsAutoY) return;
            MinimumY=0;
            MaximumY=NiceUpper(Math.Max(1,visibleMaximum)*1.15);
        }
        public void ZoomY(double factor)
        {
            IsAutoY=false;
            MaximumY=MinimumY+ClampFinite(SpanY*factor,0.01,1e12,10);
        }
        public void PanY(double minimum)
        {
            double span=SpanY;
            IsAutoY=false; MinimumY=Math.Max(0,minimum); MaximumY=MinimumY+span;
        }
        public static double NiceUpper(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value<=0) return 1;
            double magnitude=Math.Pow(10,Math.Floor(Math.Log10(value)));
            double step=magnitude/2;
            return Math.Ceiling(value/step)*step;
        }
        private static double ClampFinite(double value,double minimum,double maximum,double fallback) =>
            double.IsNaN(value) || double.IsInfinity(value) ? fallback : Math.Max(minimum,Math.Min(value,maximum));
    }
}
