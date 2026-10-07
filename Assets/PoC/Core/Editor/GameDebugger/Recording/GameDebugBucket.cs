// 시간 구간별 이벤트 집계 결과입니다. 생존 수는 표본이며 확정 사망과 일반 제거를 구분합니다.
// 서버 관측 불가와 실제 0을 구별하고 플레이어별 처치 귀속은 추정하지 않습니다.
namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugBucket
    {
        public double Start, Width;
        public bool HasFullEventCoverage;
        public int Spawned, Removed, Planned, Executed, Failures, CumulativeSpawned;
        public int Requests, Died;
        public bool HasDeathEvents;
        public bool IsServerObservationUnavailable;
        public int Alive = -1, AvailablePower = -1;
        public double OperationalRatio = -1, Area = -1, FrontlineLength = -1;
    }
}
