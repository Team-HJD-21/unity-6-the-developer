// 요청의 생성 의도를 불변 값으로 표현합니다. 동적 source를 가짜 SpawnPoint ID로 바꾸지 않습니다.
using System;
namespace TeamHJD.Game.Domain
{
    public sealed class EncounterSpawnOrigin
    {
        public static EncounterSpawnOrigin Automatic { get; } = new EncounterSpawnOrigin(SpawnOriginKind.Automatic);
        public SpawnOriginKind Kind { get; }
        public SpawnDeliveryMode Delivery { get; }
        public DefinitionId PointId { get; }
        public SpawnSourceReference Source { get; }
        public SpawnArea Area { get; }
        private EncounterSpawnOrigin(SpawnOriginKind kind, SpawnDeliveryMode delivery = SpawnDeliveryMode.Immediate,
            DefinitionId point = default, SpawnSourceReference source = null, SpawnArea area = null)
        { Kind = kind; Delivery = delivery; PointId = point; Source = source; Area = area; }
        public static EncounterSpawnOrigin AtPoint(DefinitionId point)
        {
            if (point.IsEmpty || point.Value.Length > 128) throw new ArgumentException("A bounded authored point ID is required.");
            return new EncounterSpawnOrigin(SpawnOriginKind.AuthoredPoint, point: point);
        }
        public static EncounterSpawnOrigin FromSource(SpawnSourceReference source, SpawnDeliveryMode delivery = SpawnDeliveryMode.Immediate)
        {
            Validate(delivery);
            return new EncounterSpawnOrigin(SpawnOriginKind.DynamicSource, delivery, source: source ?? throw new ArgumentNullException(nameof(source)));
        }
        public static EncounterSpawnOrigin AtWorld(SpawnArea area, SpawnDeliveryMode delivery = SpawnDeliveryMode.Immediate)
        {
            if (area == null) throw new ArgumentNullException(nameof(area));
            Validate(delivery);
            return new EncounterSpawnOrigin(area.Width == 0 && area.Height == 0 ? SpawnOriginKind.WorldPoint : SpawnOriginKind.WorldArea,
                delivery, area: area);
        }
        private static void Validate(SpawnDeliveryMode delivery)
        { if (!Enum.IsDefined(typeof(SpawnDeliveryMode), delivery)) throw new ArgumentOutOfRangeException(nameof(delivery)); }
        public string GetPayloadKey() => $"{(int)Kind}:{(int)Delivery}:{PointId.Value}:{Source?.GetPayloadKey()}:{Area?.GetPayloadKey()}";
    }
}
