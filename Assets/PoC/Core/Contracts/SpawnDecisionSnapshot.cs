// 스폰 판단 시점의 공간·후보·계획·실제 생성 사실을 불변 자료로 보관합니다.
// 관측 상한을 넘으면 누락 수를 표시하며 게임 정책이나 Unity 객체를 소유하지 않습니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Contracts
{
    public sealed class SpawnDecisionSnapshot
    {
        public const int MaximumCandidates=256, MaximumPlans=512, MaximumActuals=512, MaximumGeometry=1024;
        public const int MaximumRecordedDecisions=32;
        public BattlefieldGridConfiguration Grid {get;}
        public IReadOnlyList<Cell> Cells {get;}
        public IReadOnlyList<Edge> Frontline {get;}
        public IReadOnlyList<Candidate> Candidates {get;}
        public IReadOnlyList<Plan> Plans {get;}
        public IReadOnlyList<Actual> Actuals {get;}
        public int DroppedItems {get;}
        public int DroppedActuals {get;}
        public string Policy {get;}
        public string Outcome {get;}
        public SpawnDecisionSnapshot(BattlefieldSpatialSnapshot battlefield, IEnumerable<Candidate> candidates,
            IEnumerable<Plan> plans, IEnumerable<Actual> actuals, string outcome)
        {
            Grid=battlefield.Grid.Configuration;
            Cells=Array.AsReadOnly(battlefield.Grid.OccupiedCells.Take(MaximumGeometry)
                .Select(c=>new Cell(c.X,c.Y,c.PlayerCount,c.TurretCount,c.EnemyCount)).ToArray());
            var positions=battlefield.Vertices.ToDictionary(v=>v.EntityId,v=>v.Position);
            Frontline=Array.AsReadOnly(battlefield.FrontlineEdges.Take(MaximumGeometry)
                .Select(e=>new Edge(positions[e.First],positions[e.Second])).ToArray());
            var c=candidates.ToArray(); var p=plans.ToArray(); var a=actuals.ToArray();
            Candidates=Array.AsReadOnly(c.Take(MaximumCandidates).ToArray());
            Plans=Array.AsReadOnly(p.Take(MaximumPlans).ToArray());
            Actuals=Array.AsReadOnly(a.Take(MaximumActuals).ToArray());
            DroppedActuals=Math.Max(0,a.Length-MaximumActuals);
            DroppedItems=Math.Max(0,c.Length-MaximumCandidates)+Math.Max(0,p.Length-MaximumPlans)+Math.Max(0,a.Length-MaximumActuals)
                +Math.Max(0,battlefield.Grid.OccupiedCells.Count-MaximumGeometry)+Math.Max(0,battlefield.FrontlineEdges.Count-MaximumGeometry);
            Policy="Lowest cell enemy pressure → nearest frontline → ordinal point ID; complete preset sets only";
            Outcome=outcome;
        }
        public sealed class Cell
        {
            public int X{get;} public int Y{get;} public int Players{get;} public int Turrets{get;} public int Enemies{get;}
            public Cell(int x,int y,int players,int turrets,int enemies) {X=x;Y=y;Players=players;Turrets=turrets;Enemies=enemies;}
        }
        public sealed class Edge
        {
            public BattlefieldPoint First{get;} public BattlefieldPoint Second{get;}
            public Edge(BattlefieldPoint first,BattlefieldPoint second) {First=first;Second=second;}
        }
        public sealed class Candidate
        {
            public string Id{get;} public BattlefieldPoint Position{get;} public int CellX{get;} public int CellY{get;}
            public int EnemyCount{get;} public int AssignedSets{get;} public double Distance{get;}
            public bool HasFrontline{get;} public string Reason{get;}
            public Candidate(string id,BattlefieldPoint position,int cellX,int cellY,int enemies,int assigned,double distance,bool hasFrontline,string reason)
            {Id=id;Position=position;CellX=cellX;CellY=cellY;EnemyCount=enemies;AssignedSets=assigned;Distance=distance;HasFrontline=hasFrontline;Reason=reason;}
        }
        public sealed class Plan
        {
            public string PointId{get;} public string EnemyType{get;} public int Count{get;} public string ExecutionReason{get;}
            public Plan(string point,string enemy,int count,string reason="") {PointId=point;EnemyType=enemy;Count=count;ExecutionReason=reason;}
        }
        public sealed class Actual
        {
            public string PointId{get;} public string EnemyType{get;} public string EntityId{get;} public BattlefieldPoint Position{get;}
            public Actual(string point,string enemy,string entity,BattlefieldPoint position) {PointId=point;EnemyType=enemy;EntityId=entity;Position=position;}
        }
    }
}
