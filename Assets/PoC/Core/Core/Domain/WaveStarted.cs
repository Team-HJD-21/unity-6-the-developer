// 웨이브가 진행 단계에 진입했음을 기록합니다.


namespace TeamHJD.Game.Domain
{
    public sealed class WaveStarted : MatchEvent
    {
        public int WaveIndex { get; }

        public WaveStarted(MatchId matchId, long sequence, int waveIndex)
            : base(matchId, sequence) => WaveIndex = waveIndex;
    }
}
