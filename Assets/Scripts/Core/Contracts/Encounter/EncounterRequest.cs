// Encounter 생성 의도를 식별자와 함께 전달합니다.
// Snapshot은 Client 입력에 넣지 않고 실행 권한 측의 최신 자료를 조회합니다.
using System;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class EncounterRequest
    {
        public MatchRequestContext Context { get; }
        public DefinitionId PresetId { get; }
        /// <summary>자동 편성이면 Planner가 현재 상황에서 편성을 선택합니다. PresetId는 사용하지 않습니다.</summary>
        public bool UsesAutomaticComposition { get; }
        public int MaximumEnemyCount { get; }
        public EncounterRequestSource Source { get; }
        public EncounterSpawnOrigin Origin { get; }
        public Guid CauseEventId { get; }
        public EncounterRequest(MatchRequestContext context, DefinitionId presetId, int maximumEnemyCount, EncounterRequestSource source,
            EncounterSpawnOrigin origin = null, Guid causeEventId = default)
            : this(context, presetId, maximumEnemyCount, source, origin, causeEventId, false) { }

        /// <summary>Match의 정규 생산/자동 편성 요청입니다. 고정 프리셋 요청과 같은 권한·중복 검증을 받습니다.</summary>
        public static EncounterRequest Automatic(MatchRequestContext context, int maximumEnemyCount, EncounterRequestSource source,
            EncounterSpawnOrigin origin = null, Guid causeEventId = default)
            => new EncounterRequest(context, new DefinitionId(string.Empty), maximumEnemyCount, source, origin, causeEventId, true);

        private EncounterRequest(MatchRequestContext context, DefinitionId presetId, int maximumEnemyCount, EncounterRequestSource source,
            EncounterSpawnOrigin origin, Guid causeEventId, bool automatic)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (!automatic && (presetId.IsEmpty || presetId.Value.Length > 128))
                throw new ArgumentException("A valid preset id is required.", nameof(presetId));
            if (maximumEnemyCount < 1) throw new ArgumentOutOfRangeException(nameof(maximumEnemyCount));
            if (!Enum.IsDefined(typeof(EncounterRequestSource), source)) throw new ArgumentOutOfRangeException(nameof(source));
            PresetId = presetId;
            UsesAutomaticComposition = automatic;
            MaximumEnemyCount = maximumEnemyCount;
            Source = source;
            Origin = origin ?? EncounterSpawnOrigin.Automatic; CauseEventId = causeEventId;
        }
        public string GetPayloadKey() => (UsesAutomaticComposition ? "automatic:" : "preset:") + PresetId.Value.Length + ":" + PresetId.Value + ":" + MaximumEnemyCount + ":" + (int)Source +
            ":" + CauseEventId.ToString("N") + ":" + Origin.GetPayloadKey();
    }
}
