// 현재 웨이브 위치와 진행 상황을 나타내는 불변 카운터입니다.


namespace TeamHJD.Game.Domain
{
    public sealed class WaveState
    {
        public int CurrentIndex { get; }
        public int TotalCount { get; }
        public int SpawnedCount { get; }
        public int DefeatedCount { get; }

        public WaveState(int currentIndex, int totalCount, int spawnedCount, int defeatedCount)
        {
            CurrentIndex = currentIndex;
            TotalCount = totalCount;
            SpawnedCount = spawnedCount;
            DefeatedCount = defeatedCount;
        }
    }
}
