// Immutable counters describing the current wave position and progress.


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
