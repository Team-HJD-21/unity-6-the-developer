// 시간 구간별 이벤트 집계 결과입니다. 생존 수는 표본이고 처치 수는 제공하지 않습니다.
namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugBucket
    {
        public double Start, Width;
        public bool HasFullEventCoverage;
        public int Spawned, Removed, Planned, Executed, Failures, CumulativeSpawned;
        public int Alive = -1, AvailablePower = -1;
        public double OperationalRatio = -1, Area = -1, FrontlineLength = -1;
    }
}
