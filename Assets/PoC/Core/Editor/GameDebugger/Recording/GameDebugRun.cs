// 활성 Match와 구분되는 실행 기록입니다. 종료된 기록은 게임 상태를 복원하지 않습니다.
using System;
using System.Collections.Generic;
namespace TeamHJD.Game.Editor
{
    [Serializable]
    public sealed class GameDebugRun
    {
        public int SchemaVersion = 1;
        public string MatchId, Scene, UnityVersion, BuildLabel, StartedAt, ContentVersion, GridDescription, StageId, DifficultyProfileId, MatchMode;
        public string ClockPolicy = "Game: scaled; Host/start wait: realtime; samples: realtime";
        public int RandomSeed;
        public bool HasSeed, IsPartial = true, IsComplete;
        public bool HasDeathEvents;
        public int DroppedSpawnDecisions;
        public List<GameDebugSpawnDecision> SpawnDecisions = new List<GameDebugSpawnDecision>();
        public int DroppedEvents, DroppedSamples;
        public long LastSequence, RuntimeDroppedEvents;
        public List<GameDebugEventEntry> Events = new List<GameDebugEventEntry>();
        public List<GameDebugSample> Samples = new List<GameDebugSample>();
        public List<GameDebugMarker> Markers = new List<GameDebugMarker>();
    }
}
