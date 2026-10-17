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
        public int MaximumEnemyCount { get; }
        public EncounterRequestSource Source { get; }
        public EncounterRequest(MatchRequestContext context, DefinitionId presetId, int maximumEnemyCount, EncounterRequestSource source)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (presetId.IsEmpty || presetId.Value.Length > 128) throw new ArgumentException("A valid preset id is required.", nameof(presetId));
            if (maximumEnemyCount < 1) throw new ArgumentOutOfRangeException(nameof(maximumEnemyCount));
            if (!Enum.IsDefined(typeof(EncounterRequestSource), source)) throw new ArgumentOutOfRangeException(nameof(source));
            PresetId = presetId;
            MaximumEnemyCount = maximumEnemyCount;
            Source = source;
        }
        public string GetPayloadKey() => PresetId.Value.Length + ":" + PresetId.Value + ":" + MaximumEnemyCount + ":" + (int)Source;
    }
}
