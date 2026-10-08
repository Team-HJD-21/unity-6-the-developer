// 요청·타임라인의 정렬/필터 결과를 영역별로 캐시합니다.
// IMGUI Layout/Repaint마다 전체 기록을 재정렬하지 않고 자료나 검색어가 바뀔 때만 갱신합니다.
using System;
using System.Collections.Generic;
using System.Linq;
using TeamHJD.Game.Contracts;

namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugListCache
    {
        public sealed class TimelineRow
        {
            public double Time { get; }
            public GameDebugEventEntry Event { get; }
            public GameDebugMarker Marker { get; }
            public TimelineRow(GameDebugEventEntry item) { Event=item; Time=item.GameSeconds; }
            public TimelineRow(GameDebugMarker marker) { Marker=marker; Time=marker.GameSeconds; }
        }

        private GameDebugRun _requestRun, _timelineRun;
        private GameDebugEventEntry _lastRequestEvent, _lastTimelineEvent;
        private GameDebugMarker _lastMarker;
        private int _requestCount=-1, _timelineCount=-1, _markerCount=-1;
        private string _search;
        private GameDebugEventEntry[] _requests=Array.Empty<GameDebugEventEntry>();
        private TimelineRow[] _timeline=Array.Empty<TimelineRow>();

        public IReadOnlyList<GameDebugEventEntry> Requests(GameDebugRun run)
        {
            int count=run?.Events.Count ?? 0;
            GameDebugEventEntry last=count>0 ? run.Events[count-1] : null;
            if(ReferenceEquals(run,_requestRun) && count==_requestCount && ReferenceEquals(last,_lastRequestEvent)) return _requests;
            _requestRun=run; _requestCount=count; _lastRequestEvent=last;
            _requests=run?.Events.Where(e=>e.Kind==GameDiagnosticKind.RequestCompleted || e.Kind==GameDiagnosticKind.RequestFailed)
                .Reverse().Take(200).ToArray() ?? Array.Empty<GameDebugEventEntry>();
            return _requests;
        }

        public IReadOnlyList<TimelineRow> Timeline(GameDebugRun run,string search)
        {
            search ??= "";
            int events=run?.Events.Count ?? 0, markers=run?.Markers.Count ?? 0;
            GameDebugEventEntry lastEvent=events>0 ? run.Events[events-1] : null;
            GameDebugMarker lastMarker=markers>0 ? run.Markers[markers-1] : null;
            if(ReferenceEquals(run,_timelineRun) && events==_timelineCount && markers==_markerCount &&
                ReferenceEquals(lastEvent,_lastTimelineEvent) && ReferenceEquals(lastMarker,_lastMarker) && search==_search) return _timeline;
            _timelineRun=run; _timelineCount=events; _markerCount=markers;
            _lastTimelineEvent=lastEvent; _lastMarker=lastMarker; _search=search;
            _timeline=run==null ? Array.Empty<TimelineRow>() : run.Events.Select(e=>new TimelineRow(e))
                .Concat(run.Markers.Select(m=>new TimelineRow(m)))
                .Where(row=>search.Length==0 || SearchText(row).IndexOf(search,StringComparison.OrdinalIgnoreCase)>=0)
                .OrderByDescending(row=>row.Time).Take(300).ToArray();
            return _timeline;
        }

        private static string SearchText(TimelineRow row) => row.Event==null ? row.Marker.Message ?? "" :
            row.Event.Source+" "+row.Event.Kind+" "+row.Event.EnemyType+" "+row.Event.Message;
    }
}
