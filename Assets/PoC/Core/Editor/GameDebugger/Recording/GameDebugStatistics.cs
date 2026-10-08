// 원시 이벤트·표본을 시간 구간으로 집계합니다. GUI와 게임 정책에 독립적입니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Editor
{
    public static class GameDebugStatistics
    {
        public static IReadOnlyList<GameDebugBucket> Build(GameDebugRun run, double width, bool useRealTime,
            string enemyFilter = "", string pointFilter = "", string presetFilter = "", EncounterRequestSource? sourceFilter = null)
        {
            if (run == null) return Array.Empty<GameDebugBucket>();
            if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
            double duration = 0;
            foreach (var item in run.Events) duration = Math.Max(duration, useRealTime ? item.RealSeconds : item.GameSeconds);
            foreach (var item in run.Samples) duration = Math.Max(duration, useRealTime ? item.RealSeconds : item.GameSeconds);
            width = Math.Max(width, (duration + 0.0001d) / 2048d);
            int count = Math.Min(2048, (int)Math.Floor(duration / width) + 1);
            var buckets = new List<GameDebugBucket>(count);
            bool hasRequestBreakdown=string.IsNullOrEmpty(enemyFilter) && string.IsNullOrEmpty(pointFilter);
            for (int index = 0; index < count; index++) buckets.Add(new GameDebugBucket {
                Start=index*width, Width=width, HasFullEventCoverage=!run.IsPartial, HasDeathEvents=run.HasDeathEvents,
                Requests=hasRequestBreakdown ? 0 : -1 });
            foreach (GameDebugEventEntry item in run.Events)
            {
                if (sourceFilter.HasValue && item.Source != sourceFilter.Value) continue;
                if (!string.IsNullOrEmpty(presetFilter) && item.SquadOrder != presetFilter) continue;
                if (!string.IsNullOrEmpty(enemyFilter) && !string.Equals(item.EnemyType, enemyFilter, StringComparison.Ordinal)) continue;
                if (!string.IsNullOrEmpty(pointFilter) && !string.Equals(item.SpawnPointId, pointFilter, StringComparison.Ordinal)) continue;
                int index = Math.Min(count-1, Math.Max(0, (int)Math.Floor((useRealTime ? item.RealSeconds : item.GameSeconds) / width)));
                GameDebugBucket bucket = buckets[index];
                if (item.Kind == GameDiagnosticKind.RequestStarted && hasRequestBreakdown) bucket.Requests++;
                if (item.Kind == GameDiagnosticKind.EnemySpawned) bucket.Spawned += item.ActualCount;
                if (item.Kind == GameDiagnosticKind.EnemyDied) bucket.Died += item.ActualCount;
                if (item.Kind == GameDiagnosticKind.EnemyRemoved) bucket.Removed += item.ActualCount;
                if (item.Kind == GameDiagnosticKind.RequestCompleted || item.Kind == GameDiagnosticKind.RequestFailed)
                { bucket.Planned += item.PlannedCount; bucket.Executed += item.ActualCount; }
                if (item.Kind == GameDiagnosticKind.RequestFailed) bucket.Failures++;
            }
            foreach (GameDebugSample sample in run.Samples)
            {
                int index = Math.Min(count-1, Math.Max(0, (int)Math.Floor((useRealTime ? sample.RealSeconds : sample.GameSeconds) / width)));
                GameDebugBucket bucket = buckets[index];
                bucket.Alive = sample.AliveEnemies; bucket.AvailablePower = sample.AvailablePower;
                bucket.IsServerObservationUnavailable=sample.AliveEnemies<0;
                bucket.OperationalRatio = sample.TurretCount == 0 ? -1d : (double)sample.OperationalTurrets / sample.TurretCount;
                bucket.Area = sample.TerritoryArea; bucket.FrontlineLength = sample.FrontlineLength;
            }
            int cumulative = 0;
            foreach (GameDebugBucket bucket in buckets) { cumulative += bucket.Spawned; bucket.CumulativeSpawned = cumulative; }
            return buckets.AsReadOnly();
        }
    }
}
