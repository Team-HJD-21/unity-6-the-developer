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
        public EncounterRequestSource Source;
        public GameDiagnosticKind Kind;
        public double GameSeconds, RealSeconds;
        public int PlannedCount, ActualCount;
        public static GameDebugEventEntry Copy(GameDiagnosticEvent item) => new GameDebugEventEntry {
            Sequence=item.Sequence, MatchId=item.MatchId.ToString(), Revision=item.Revision, RequestId=item.RequestId,
            EnemyType=item.EnemyType, SpawnPointId=item.SpawnPointId, EntityId=item.EntityId, Message=item.Message,
            Source=item.Source, Kind=item.Kind, GameSeconds=item.GameSeconds, RealSeconds=item.RealSeconds,
            PlannedCount=item.PlannedCount, ActualCount=item.ActualCount, SquadOrder=item.SquadOrder };
    }
}
