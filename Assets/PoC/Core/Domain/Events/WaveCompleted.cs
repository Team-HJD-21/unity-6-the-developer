// Match에서 웨이브가 완료되었음을 기록합니다.


namespace TeamHJD.Game.Domain
{
    public sealed class WaveCompleted : MatchEvent
    {
        public int WaveIndex { get; }

        public WaveCompleted(MatchId matchId, long sequence, int waveIndex)
            : base(matchId, sequence) => WaveIndex = waveIndex;
    }
}
