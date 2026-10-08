// 외부 JSON의 스폰 판단 자료를 크기·좌표·수량 기준으로 검증합니다.
// 예전 기록의 미계측 자료는 빈 목록으로 정규화하고 추측 데이터를 만들지 않습니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
namespace TeamHJD.Game.Editor
{
    public static class GameDebugSpawnValidation
    {
        public static void Validate(GameDebugRun run)
        {
            if (run.SpawnDecisions==null) run.SpawnDecisions=new List<GameDebugSpawnDecision>();
            if (run.SpawnDecisions.Count>GameDebugSpawnDecision.MaximumRecords || run.DroppedSpawnDecisions<0) Invalid();
            foreach(var d in run.SpawnDecisions)
            {
                if (d==null || d.MatchId!=run.MatchId || d.RequestId<0 || d.Revision<0 || d.Sequence<0 ||
                    !Finite(d.GameSeconds)||!Finite(d.RealSeconds)||d.GameSeconds<0||d.RealSeconds<0||
                    d.Planned<0||d.Spawned<0||d.DroppedItems<0||d.DroppedActuals<0||d.DroppedActuals>d.DroppedItems||d.Grid==null) Invalid();
                Text(d.Source);Text(d.Preset);Text(d.Policy);Text(d.Outcome);
                var g=d.Grid;
                if(!Finite(g.CenterX)||!Finite(g.CenterY)||!Finite(g.Width)||!Finite(g.Height)||g.Width<=0||g.Height<=0||
                    g.CellsX<=0||g.CellsY<=0||(long)g.CellsX*g.CellsY>BattlefieldGridConfiguration.MaximumCellCount) Invalid();
                Check(d.Cells,SpawnDecisionSnapshot.MaximumGeometry);
                Check(d.Frontline,SpawnDecisionSnapshot.MaximumGeometry);
                Check(d.Candidates,SpawnDecisionSnapshot.MaximumCandidates);
                Check(d.Plans,SpawnDecisionSnapshot.MaximumPlans);
                Check(d.Actuals,SpawnDecisionSnapshot.MaximumActuals);
                foreach(var c in d.Cells)
                    if(c==null||c.X<0||c.X>=g.CellsX||c.Y<0||c.Y>=g.CellsY||c.Players<0||c.Turrets<0||c.Enemies<0) Invalid();
                foreach(var e in d.Frontline)
                    if(e==null||!Finite(e.X1)||!Finite(e.X2)||!Finite(e.Y1)||!Finite(e.Y2)) Invalid();
                foreach(var c in d.Candidates)
                {
                    if(c==null||!Finite(c.X)||!Finite(c.Y)||!Finite(c.Distance)||c.Distance<0||c.Sets<0||c.Enemies<0||
                        c.CellX< -1||c.CellX>=g.CellsX||c.CellY< -1||c.CellY>=g.CellsY||
                        c.CellService<0||c.PointService<0||c.CellPendingEnemies<0||c.CellScore<0) Invalid();
                    Text(c.Id);Text(c.Reason);
                }
                foreach(var p in d.Plans) {if(p==null||p.Count<0) Invalid();Text(p.Point);Text(p.Enemy);Text(p.ExecutionReason);}
                foreach(var a in d.Actuals) {if(a==null||!Finite(a.X)||!Finite(a.Y)) Invalid();Text(a.Point);Text(a.Enemy);Text(a.Entity);}
            }
        }
        private static bool Finite(double v)=>!double.IsNaN(v)&&!double.IsInfinity(v);
        private static void Check<T>(List<T> list,int maximum) {if(list==null||list.Count>maximum) Invalid();}
        private static void Text(string v) {if(v!=null&&v.Length>4096) Invalid();}
        private static void Invalid()=>throw new ArgumentException("스폰 판단 기록의 크기·좌표·수량이 잘못되었습니다.");
    }
}
