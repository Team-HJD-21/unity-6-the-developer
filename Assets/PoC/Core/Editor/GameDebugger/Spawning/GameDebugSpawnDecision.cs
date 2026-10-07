// 요청별 스폰 판단을 JSON 가능한 독립 복사본으로 보관합니다.
// 당시 Grid·후보·계획·실제 생성 위치를 묶으며 현재 Scene 상태로 덮어쓰지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugSpawnDecision
    {
        public const int MaximumRecords=SpawnDecisionSnapshot.MaximumRecordedDecisions;
        public long RequestId,Revision,Sequence;
        public double GameSeconds,RealSeconds;
        public string MatchId,Source,Preset,Policy,Outcome;
        public int Planned,Spawned,DroppedItems,DroppedActuals;
        public GridMetadata Grid;
        public List<Cell> Cells=new List<Cell>();
        public List<Edge> Frontline=new List<Edge>();
        public List<Candidate> Candidates=new List<Candidate>();
        public List<Plan> Plans=new List<Plan>();
        public List<Actual> Actuals=new List<Actual>();
        public static GameDebugSpawnDecision Copy(GameDiagnosticEvent item)
        {
            var s=item.SpawnDecision;
            return new GameDebugSpawnDecision {
                RequestId=item.RequestId,Revision=item.Revision,Sequence=item.Sequence,MatchId=item.MatchId.ToString(),
                GameSeconds=item.GameSeconds,RealSeconds=item.RealSeconds,Source=item.Source.ToString(),Preset=item.SquadOrder,
                Planned=item.PlannedCount,Spawned=item.ActualCount,Policy=s.Policy,Outcome=s.Outcome,DroppedItems=s.DroppedItems,DroppedActuals=s.DroppedActuals,
                Grid=new GridMetadata(s.Grid),
                Cells=s.Cells.Select(c=>new Cell{X=c.X,Y=c.Y,Players=c.Players,Turrets=c.Turrets,Enemies=c.Enemies}).ToList(),
                Frontline=s.Frontline.Select(e=>new Edge{X1=e.First.X,Y1=e.First.Y,X2=e.Second.X,Y2=e.Second.Y}).ToList(),
                Candidates=s.Candidates.Select(c=>new Candidate{Id=c.Id,X=c.Position.X,Y=c.Position.Y,CellX=c.CellX,CellY=c.CellY,
                    Enemies=c.EnemyCount,Sets=c.AssignedSets,Distance=c.Distance,HasFrontline=c.HasFrontline,Reason=c.Reason}).ToList(),
                Plans=s.Plans.Select(p=>new Plan{Point=p.PointId,Enemy=p.EnemyType,Count=p.Count,ExecutionReason=p.ExecutionReason}).ToList(),
                Actuals=s.Actuals.Select(a=>new Actual{Point=a.PointId,Enemy=a.EnemyType,Entity=a.EntityId,X=a.Position.X,Y=a.Position.Y}).ToList()
            };
        }
        [Serializable] public sealed class Cell {public int X,Y,Players,Turrets,Enemies;}
        [Serializable] public sealed class Edge {public double X1,Y1,X2,Y2;}
        [Serializable] public sealed class Candidate
        {public string Id,Reason;public double X,Y,Distance;public int CellX,CellY,Enemies,Sets;public bool HasFrontline;}
        [Serializable] public sealed class Plan {public string Point,Enemy,ExecutionReason;public int Count;}
        [Serializable] public sealed class Actual {public string Point,Enemy,Entity;public double X,Y;}
    }
}
