// 확정된 Match 환경에서 현재 날씨만 읽는 편의 API입니다.
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    // WeatherManager가 Core에 전달한 SnapShot 정보에서 현재 날씨 정보를 읽어보는 쿼리 클래스
    public static class MatchWeatherQueries
    {
        public static bool TryGetCurrentWeather(this IMatchEnvironmentSource source,
            MatchId matchId, out DefinitionId weatherId)
        {
            weatherId = default;
            if (source == null || matchId.IsEmpty) return false;

            MatchEnvironmentSnapshot snapshot = source.Snapshot;
            if (snapshot == null || snapshot.MatchId != matchId || !snapshot.HasWeather)
                return false;

            weatherId = snapshot.WeatherId;
            return true;
        }
    }
}
