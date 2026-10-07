// 영역별 기능·기록·차트·검색·스크롤의 직렬화 가능한 편집 상태입니다.
// 게임 설정이나 Match 상태와는 독립적이며 창 reload/배치 전환에도 유지됩니다.
using System;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebuggerPaneState
    {
        public int Page;
        public string SelectedRun="LIVE", ReferenceRun="None", Search="", Bookmark="";
        public string EnemyFilter="", PointFilter="", PresetFilter="", SourceFilter="All";
        public float BinSeconds=5;
        public bool IsRealClock;
        public GameDebugMetric Metric;
        public GameDebugChartViewport ChartViewport=new GameDebugChartViewport();
        public bool IsSpawnInspector;
        public bool IsChartSettingsExpanded, IsChartNotesExpanded;
        public GameDebugMonitorState Monitor=new GameDebugMonitorState();
        public Vector2[] ScrollOffsets=new Vector2[6];
        public BattlefieldDebugPanel BattlefieldPanel=new BattlefieldDebugPanel();

        public void Validate()
        {
            Page=Mathf.Clamp(Page,0,5);
            if (float.IsNaN(BinSeconds) || float.IsInfinity(BinSeconds)) BinSeconds=5;
            BinSeconds=Mathf.Clamp(BinSeconds,0.1f,3600);
            if (!Enum.IsDefined(typeof(GameDebugMetric),Metric)) Metric=GameDebugMetric.Spawned;
            SelectedRun=SelectedRun ?? "LIVE"; ReferenceRun=ReferenceRun ?? "None";
            Search=Search ?? ""; Bookmark=Bookmark ?? "";
            EnemyFilter=EnemyFilter ?? ""; PointFilter=PointFilter ?? ""; PresetFilter=PresetFilter ?? "";
            SourceFilter=SourceFilter ?? "All";
            if (ScrollOffsets==null || ScrollOffsets.Length!=6) ScrollOffsets=new Vector2[6];
            BattlefieldPanel=BattlefieldPanel ?? new BattlefieldDebugPanel();
            ChartViewport=ChartViewport ?? new GameDebugChartViewport();
            ChartViewport.Validate();
            Monitor=Monitor ?? new GameDebugMonitorState();
        }
    }
}
