// Host가 확정한 날씨·시간대·예고를 Match/revision과 함께 불변으로 전달합니다.
// 미제공 정보는 빈 식별자로 구분하며 Client의 임의 낮/맑음 기본값으로 대체하지 않습니다.
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
namespace TeamHJD.Game.Domain
{
    public sealed class MatchEnvironmentSnapshot
    {
        public const int MaximumForecast = 32, MaximumIdBytes = 256;
        public MatchId MatchId { get; }
        public long Revision { get; }
        public double GameSeconds { get; }
        public DefinitionId WeatherId { get; }
        public DefinitionId TimeOfDayId { get; }
        public bool HasWeather => !WeatherId.IsEmpty;
        public bool HasTimeOfDay => !TimeOfDayId.IsEmpty;
        public IReadOnlyList<DefinitionId> Forecast { get; }
        public MatchEnvironmentSnapshot(MatchId matchId, long revision, double gameSeconds,
            DefinitionId weatherId, DefinitionId timeOfDayId, IEnumerable<DefinitionId> forecast = null)
        {
            if (matchId.IsEmpty) throw new ArgumentException("A Match ID is required.", nameof(matchId));
            if (revision < 1) throw new ArgumentOutOfRangeException(nameof(revision));
            if (double.IsNaN(gameSeconds) || double.IsInfinity(gameSeconds) || gameSeconds < 0)
                throw new ArgumentOutOfRangeException(nameof(gameSeconds));
            ValidateId(weatherId); ValidateId(timeOfDayId);
            var copy = new List<DefinitionId>();
            if (forecast != null) foreach (DefinitionId value in forecast)
            {
                if (copy.Count >= MaximumForecast) throw new ArgumentException("Forecast exceeds the contract limit.", nameof(forecast));
                ValidateId(value);
                if (value.IsEmpty) throw new ArgumentException("Forecast cannot contain an unknown weather.", nameof(forecast));
                copy.Add(value);
            }
            if (weatherId.IsEmpty && copy.Count > 0) throw new ArgumentException("Forecast requires a known weather source.", nameof(forecast));
            MatchId = matchId; Revision = revision; GameSeconds = gameSeconds;
            WeatherId = weatherId; TimeOfDayId = timeOfDayId; Forecast = new ReadOnlyCollection<DefinitionId>(copy);
        }
        private static void ValidateId(DefinitionId value)
        {
            if (!value.IsEmpty && new UTF8Encoding(false, true).GetByteCount(value.Value) > MaximumIdBytes)
                throw new ArgumentException("Environment ID exceeds its wire limit.");
        }
        public bool HasSameValues(MatchEnvironmentSnapshot other)
        {
            if (other == null || MatchId != other.MatchId || WeatherId != other.WeatherId ||
                TimeOfDayId != other.TimeOfDayId || Forecast.Count != other.Forecast.Count) return false;
            for (int i = 0; i < Forecast.Count; i++) if (Forecast[i] != other.Forecast[i]) return false;
            return true;
        }
    }
}
