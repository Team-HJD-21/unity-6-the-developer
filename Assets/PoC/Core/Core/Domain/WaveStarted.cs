// Records that a wave has entered its running phase.


namespace TeamHJD.Game.Domain
{
    public sealed class WaveStarted : MatchEvent
    {
        public int WaveIndex { get; }

        public WaveStarted(MatchId matchId, long sequence, int waveIndex)
            : base(matchId, sequence) => WaveIndex = waveIndex;
    }
}
