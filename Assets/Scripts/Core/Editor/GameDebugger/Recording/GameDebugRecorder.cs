// 크기 제한 실행 기록과 이벤트 중복 수신 제거를 담당하는 순수 수집기입니다.
using System;
using System.Collections.Generic;
using TeamHJD.Game.Contracts;
namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugRecorder
    {
        public const int MaximumRuns = 8, MaximumEvents = 8192, MaximumSamples = 7200;
        private readonly List<GameDebugRun> _runs = new List<GameDebugRun>();
        public IReadOnlyList<GameDebugRun> Runs => _runs.AsReadOnly();
        public int Version { get; private set; }
        public GameDebugRun Find(string id) => _runs.Find(run => run.MatchId == id);
        public void RestoreArchives(IEnumerable<GameDebugRun> runs)
        {
            foreach (var run in runs)
            {
                if (run == null || Find(run.MatchId) != null) continue;
                if (_runs.Count >= MaximumRuns) _runs.RemoveAt(0);
                _runs.Add(run);
            }
            Version++;
        }
        public GameDebugRun Accept(GameDiagnosticEvent item, Func<GameDebugRun> createRun)
        {
            if (item.MatchId.IsEmpty) return null;
            GameDebugRun run = Find(item.MatchId.ToString());
            if (run == null)
            {
                run = createRun();
                run.MatchId = item.MatchId.ToString();
                if (_runs.Count >= MaximumRuns) _runs.RemoveAt(0);
                _runs.Add(run);
            }
            if (item.Sequence <= run.LastSequence) return run;
            if (run.LastSequence > 0 && item.Sequence > run.LastSequence + 1) run.IsPartial = true;
            run.LastSequence = item.Sequence;
            if (item.Kind == GameDiagnosticKind.MatchStarted) run.IsPartial = false;
            if (run.Events.Count >= MaximumEvents) { run.Events.RemoveAt(0); run.DroppedEvents++; run.IsPartial = true; }
            run.Events.Add(GameDebugEventEntry.Copy(item));
            if (item.HasDeathEvents) run.HasDeathEvents=true;
            if (item.IsSpawnDecisionOmitted) run.DroppedSpawnDecisions++;
            if (item.SpawnDecision != null)
            {
                if (run.SpawnDecisions.Count>=GameDebugSpawnDecision.MaximumRecords)
                { run.SpawnDecisions.RemoveAt(0); run.DroppedSpawnDecisions++; }
                run.SpawnDecisions.Add(GameDebugSpawnDecision.Copy(item));
            }
            if (item.Kind == GameDiagnosticKind.MatchEnded) run.IsComplete = true;
            Version++;
            return run;
        }
        public void AddSample(GameDebugRun run, GameDebugSample sample)
        {
            if (run == null || run.IsComplete) return;
            if (run.Samples.Count >= MaximumSamples) { run.Samples.RemoveAt(0); run.DroppedSamples++; run.IsPartial = true; }
            run.Samples.Add(sample);
            Version++;
        }
    }
}
