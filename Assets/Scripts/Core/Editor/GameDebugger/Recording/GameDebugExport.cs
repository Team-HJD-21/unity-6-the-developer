// 실행 기록을 CSV/JSON으로 변환하고 읽기 전용 비교 기록을 검증합니다.
// 파일 경로 선택과 저장은 Window에서 명시적으로 실행합니다.
using System;
using System.Globalization;
using System.Text;
using UnityEngine;

namespace TeamHJD.Game.Editor
{
    public static class GameDebugExport
    {
        public static string ToJson(GameDebugRun run) => JsonUtility.ToJson(run, true);
        public static GameDebugRun FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json) || json.Length > 16*1024*1024) throw new ArgumentException("비교 기록은 16MiB 이하 JSON이어야 합니다.");
            var run = JsonUtility.FromJson<GameDebugRun>(json);
            if (run == null || run.SchemaVersion != 1 || string.IsNullOrWhiteSpace(run.MatchId) ||
                run.Events == null || run.Samples == null || run.Markers == null ||
                run.Events.Count > GameDebugRecorder.MaximumEvents || run.Samples.Count > GameDebugRecorder.MaximumSamples || run.Markers.Count > 512)
                throw new ArgumentException("지원하지 않는 schema 또는 크기/식별자입니다.");
            foreach (var item in run.Events)
                if (item == null || item.MatchId != run.MatchId || !Finite(item.GameSeconds) || !Finite(item.RealSeconds) || item.GameSeconds<0 || item.RealSeconds<0 || item.PlannedCount<0 || item.ActualCount<0)
                    throw new ArgumentException("이벤트 ID/시간/수량이 잘못되었습니다.");
            foreach (var item in run.Samples)
                if (item == null || !Finite(item.GameSeconds) || !Finite(item.RealSeconds) || item.GameSeconds<0 || item.RealSeconds<0 || !Finite(item.TerritoryArea) || !Finite(item.FrontlineLength))
                    throw new ArgumentException("표본 시간이 잘못되었습니다.");
            foreach (var item in run.Markers)
                if (item == null || !Finite(item.GameSeconds) || !Finite(item.RealSeconds) || item.GameSeconds<0 || item.RealSeconds<0)
                    throw new ArgumentException("북마크 시간이 잘못되었습니다.");
            GameDebugSpawnValidation.Validate(run);
            return run;
        }
        private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
        public static string CsvCell(string value)
        {
            value = value ?? "";
            // 외부 텍스트를 spreadsheet 수식으로 실행하지 않도록 합니다.
            string trimmed = value.TrimStart();
            if (trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) value = "'"+value;
            return "\""+value.Replace("\"", "\"\"")+"\"";
        }
        private static string Number(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        public static string EventsCsv(GameDebugRun run)
        {
            var text = new StringBuilder("match_id,sequence,request_id,revision,source,kind,game_seconds,real_seconds,preset,enemy_type,spawn_point,entity_id,planned,actual,message,request_guid,cause_event_id,origin,spawn_source_id,source_generation,delivery\n");
            foreach (var item in run.Events)
                text.AppendLine(string.Join(",", CsvCell(run.MatchId), item.Sequence, item.RequestId, item.Revision, item.Source, item.Kind,
                    Number(item.GameSeconds), Number(item.RealSeconds), CsvCell(item.SquadOrder), CsvCell(item.EnemyType), CsvCell(item.SpawnPointId),
                    CsvCell(item.EntityId), item.PlannedCount, item.ActualCount, CsvCell(item.Message),
                    CsvCell(item.RequestGuid),CsvCell(item.CauseEventId),CsvCell(item.OriginKind),CsvCell(item.SpawnSourceId),
                    CsvCell(item.SpawnSourceGeneration),CsvCell(item.DeliveryMode)));
            return text.ToString();
        }
        public static string SamplesCsv(GameDebugRun run)
        {
            var text = new StringBuilder("match_id,revision,game_seconds,real_seconds,alive,unattributed,turrets,activated,operational,available_power,maximum_power,time_scale,territory_area,frontline_length\n");
            foreach (var item in run.Samples)
                text.AppendLine(string.Join(",", CsvCell(run.MatchId), item.Revision, Number(item.GameSeconds), Number(item.RealSeconds), item.AliveEnemies,
                    item.UnattributedEnemies, item.TurretCount, item.ActivatedTurrets, item.OperationalTurrets, item.AvailablePower, item.MaximumPower,
                    Number(item.TimeScale), Number(item.TerritoryArea), Number(item.FrontlineLength)));
            return text.ToString();
        }
    }
}
