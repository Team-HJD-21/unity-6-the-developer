// 확정된 Match 환경에서 현재 낮밤 상태를 읽는 편의 API입니다.
using TeamHJD.Game.Domain;

namespace TeamHJD.Game.Contracts
{
    public static class MatchDayNightQueries
    {
        // 낮밤 반환 API
        public static bool TryGetCurrentDayPhase(this IMatchEnvironmentSource source,
            MatchId matchId, out DayNightPhase phase)
        {
            phase = default;
            if (source == null || matchId.IsEmpty) return false;

            MatchEnvironmentSnapshot snapshot = source.Snapshot;
            if (snapshot == null || snapshot.MatchId != matchId || !snapshot.HasTimeOfDay)
                return false;

            switch (snapshot.TimeOfDayId.Value)
            {
                case "day":
                    phase = DayNightPhase.Day;
                    return true;
                case "night":
                    phase = DayNightPhase.Night;
                    return true;
                default:
                    return false;
            }
        }
    }
}
