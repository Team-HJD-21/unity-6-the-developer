// 외부 진단 사실의 직렬화 가능한 복사본입니다. 로그 파싱으로 만들지 않습니다.
using System;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugEventEntry
    {
        public long Sequence, Revision, RequestId;
        public string MatchId, EnemyType, SpawnPointId, EntityId, Message, SquadOrder;
        public string RequestGuid,CauseEventId,OriginKind,SpawnSourceId,SpawnSourceGeneration,DeliveryMode;
        public EncounterRequestSource Source;
        public GameDiagnosticKind Kind;
        public double GameSeconds, RealSeconds;
        public int PlannedCount, ActualCount;
        public bool IsExecutionCountUnknown;
        public bool HasOriginArea;
        public double OriginX, OriginY, OriginWidth, OriginHeight;
        public static GameDebugEventEntry Copy(GameDiagnosticEvent item) => new GameDebugEventEntry {
            Sequence=item.Sequence, MatchId=item.MatchId.ToString(), Revision=item.Revision, RequestId=item.RequestId,
            EnemyType=item.EnemyType, SpawnPointId=item.SpawnPointId, EntityId=item.EntityId, Message=item.Message,
            Source=item.Source, Kind=item.Kind, GameSeconds=item.GameSeconds, RealSeconds=item.RealSeconds,
            PlannedCount=item.PlannedCount, ActualCount=item.ActualCount, SquadOrder=item.SquadOrder,
            IsExecutionCountUnknown=item.IsExecutionCountUnknown,
            HasOriginArea=item.ExecutionContext?.Origin.Area != null,
            OriginX=item.ExecutionContext?.Origin.Area?.Center.X ?? 0,
            OriginY=item.ExecutionContext?.Origin.Area?.Center.Y ?? 0,
            OriginWidth=item.ExecutionContext?.Origin.Area?.Width ?? 0,
            OriginHeight=item.ExecutionContext?.Origin.Area?.Height ?? 0,
            RequestGuid=item.ExecutionContext?.RequestGuid==Guid.Empty?null:item.ExecutionContext?.RequestGuid.ToString("N"),
            CauseEventId=item.ExecutionContext?.CauseEventId==Guid.Empty?null:item.ExecutionContext?.CauseEventId.ToString("N"),
            OriginKind=item.ExecutionContext?.Origin.Kind.ToString(),DeliveryMode=item.ExecutionContext?.Origin.Delivery.ToString(),
            SpawnSourceId=item.ExecutionContext?.Origin.Source?.SourceId.Value,SpawnSourceGeneration=item.ExecutionContext?.Origin.Source?.Generation.ToString("N") };
    }
}
