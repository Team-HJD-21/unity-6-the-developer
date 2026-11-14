using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Presentation
{
    /// <summary>읽기 환경의 Match/시간대가 확인된 경우만 음악 context로 바꿉니다. 임의의 낮 기본값은 없습니다.</summary>
    public static class GameplayMusicContextQueries
    {
        public static GameplayMusicContext ReadMatch(MatchId match, bool timeAvailable,
            MatchPlayerLocation location, MatchEnvironmentSnapshot environment)
        {
            if (match.IsEmpty) return GameplayMusicContext.None;
            if (location == MatchPlayerLocation.Spaceship) return GameplayMusicContext.Spaceship;
            if (location != MatchPlayerLocation.Field || !timeAvailable || environment == null ||
                environment.MatchId != match || !environment.HasTimeOfDay) return GameplayMusicContext.None;
            return environment.TimeOfDayId.Value switch
            {
                "day" => GameplayMusicContext.Day,
                "night" => GameplayMusicContext.Night,
                _ => GameplayMusicContext.None
            };
        }
    }
}
