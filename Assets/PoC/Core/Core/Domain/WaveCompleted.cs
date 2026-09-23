// Records completion of a wave in a match.


namespace TeamHJD.Game.Domain
{
    public sealed class WaveCompleted : MatchEvent
    {
        public int WaveIndex { get; }

        public WaveCompleted(MatchId matchId, long sequence, int waveIndex)
            : base(matchId, sequence) => WaveIndex = waveIndex;
    }
}
