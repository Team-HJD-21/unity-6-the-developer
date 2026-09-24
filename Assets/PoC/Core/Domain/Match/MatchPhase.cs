// Match 하나의 현재 생명주기 단계를 나타냅니다.


namespace TeamHJD.Game.Domain
{
    public enum MatchPhase
    {
        Preparing,
        Running,
        Completed,
        Disposed
    }
}
