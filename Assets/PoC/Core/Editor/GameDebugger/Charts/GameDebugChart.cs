// 여러 관측 지표를 공통 시간축의 선 그래프로 표시합니다.
// 축·범례·상하좌우 탐색·최신 추적과 가시 구간 자동 Y 범위를 제공합니다.
// N/A는 선을 끊고, 제거와 실제 사망을 서로 다른 사실로 표시합니다.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugChart : VisualElement
    {
        private readonly GameDebugChartViewport _viewport;
        private readonly IMGUIContainer _canvas;
        private IReadOnlyList<GameDebugBucket> _primary, _reference;
        private GameDebugMetric _metric;
        private double _duration=1, _globalMaximum=1;
        private double _selectedSeconds=-1;
        private Vector2 _panStart;
        private double _panTime, _panMinimum;
        private bool _isPanning;
        private string _clock="Game";
        public GameDebugChartViewport Viewport => _viewport;
        public string AxisDescription => $"{_clock} seconds · {_viewport.StartSeconds:F1}–{_viewport.EndSeconds:F1}s · Y {_viewport.MinimumY:G3}–{_viewport.MaximumY:G3}";
        public GameDebugChart() : this(new GameDebugChartViewport()) { }
        public GameDebugChart(GameDebugChartViewport viewport)
        {
            _viewport=viewport ?? throw new ArgumentNullException(nameof(viewport));
            _viewport.Validate();
            style.height=390; style.minHeight=280;
            _canvas=new IMGUIContainer(DrawGUI); _canvas.style.flexGrow=1; Add(_canvas);
            RegisterCallback<MouseMoveEvent>(_=>_canvas.MarkDirtyRepaint());
        }
        public void SetData(IReadOnlyList<GameDebugBucket> primary, IReadOnlyList<GameDebugBucket> reference,
            GameDebugMetric metric, bool useRealClock=false)
        {
            _primary=primary; _reference=reference; _metric=metric; _clock=useRealClock?"Real":"Game";
            _duration=1;
            foreach (var list in new[]{primary,reference}) if (list!=null)
                foreach (var item in list) _duration=Math.Max(_duration,item.Start+item.Width);
            _viewport.UpdateDuration(_duration);
            _canvas.MarkDirtyRepaint();
        }
        public static double Value(GameDebugBucket item,GameDebugMetric metric)
        {
            if (metric==GameDebugMetric.Died && !item.HasDeathEvents) return -1;
            double value;
            switch (metric)
            {
                case GameDebugMetric.Spawned: value=item.Spawned; break;
                case GameDebugMetric.Removed: value=item.Removed; break;
                case GameDebugMetric.Alive: return item.Alive;
                case GameDebugMetric.CumulativeSpawned: value=item.CumulativeSpawned; break;
                case GameDebugMetric.Planned: value=item.Planned; break;
                case GameDebugMetric.Executed: value=item.Executed; break;
                case GameDebugMetric.Failures: value=item.Failures; break;
                case GameDebugMetric.AvailablePower: return item.AvailablePower;
                case GameDebugMetric.OperationalRatio: return item.OperationalRatio;
                case GameDebugMetric.TerritoryArea: return item.Area;
                case GameDebugMetric.FrontlineLength: return item.FrontlineLength;
                case GameDebugMetric.Requests: value=item.Requests; break;
                default: value=item.Died; break;
            }
            return value==0 && !item.HasFullEventCoverage ? -1 : value;
        }
        private IEnumerable<GameDebugMetric> Metrics()
        {
            if (!_viewport.IsTrafficView) { yield return _metric; yield break; }
            if (_viewport.IsRequestsVisible) yield return GameDebugMetric.Requests;
            if (_viewport.IsSpawnsVisible) yield return GameDebugMetric.Spawned;
            if (_viewport.IsDeathsVisible) yield return GameDebugMetric.Died;
            if (_viewport.IsRemovalsVisible) yield return GameDebugMetric.Removed;
        }
        private static Color MetricColor(GameDebugMetric metric)
        {
            switch(metric)
            {
                case GameDebugMetric.Requests: return new Color(0.9f,0.69f,0.3f);
                case GameDebugMetric.Died: return new Color(0.92f,0.39f,0.43f);
                case GameDebugMetric.Removed: return new Color(0.65f,0.57f,0.84f);
                default: return new Color(0.36f,0.77f,0.64f);
            }
        }
        private static string MetricName(GameDebugMetric metric) => metric==GameDebugMetric.Requests?"요청 횟수" :
            metric==GameDebugMetric.Spawned?"실제 생성" : metric==GameDebugMetric.Died?"실제 사망" :
            metric==GameDebugMetric.Removed?"제거 (Despawn)" : metric.ToString();
        private static string Unit(GameDebugMetric metric) => metric==GameDebugMetric.OperationalRatio?"비율" :
            metric==GameDebugMetric.TerritoryArea?"면적 (world²)" : metric==GameDebugMetric.FrontlineLength?"길이 (world)" :
            metric==GameDebugMetric.AvailablePower?"전력" : "수량 / 구간";
        private void DrawGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                _viewport.IsFollowingLatest=GUILayout.Toggle(_viewport.IsFollowingLatest,"Follow latest",EditorStyles.toolbarButton);
                _viewport.IsAutoY=GUILayout.Toggle(_viewport.IsAutoY,"Auto Y",EditorStyles.toolbarButton);
                if (GUILayout.Button("X +",EditorStyles.toolbarButton,GUILayout.Width(36))) _viewport.ZoomTime(0.5,_duration);
                if (GUILayout.Button("X −",EditorStyles.toolbarButton,GUILayout.Width(36))) _viewport.ZoomTime(2,_duration);
                if (GUILayout.Button("Y +",EditorStyles.toolbarButton,GUILayout.Width(36))) _viewport.ZoomY(0.5);
                if (GUILayout.Button("Y −",EditorStyles.toolbarButton,GUILayout.Width(36))) _viewport.ZoomY(2);
                if (GUILayout.Button("Fit",EditorStyles.toolbarButton,GUILayout.Width(38)))
                { _viewport.SpanSeconds=Math.Max(1,_duration); _viewport.IsFollowingLatest=true; _viewport.IsAutoY=true; }
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                if (_viewport.IsTrafficView)
                {
                    LegendToggle("요청",ref _viewport.IsRequestsVisible,MetricColor(GameDebugMetric.Requests));
                    LegendToggle("생성",ref _viewport.IsSpawnsVisible,MetricColor(GameDebugMetric.Spawned));
                    LegendToggle("사망",ref _viewport.IsDeathsVisible,MetricColor(GameDebugMetric.Died));
                    LegendToggle("제거",ref _viewport.IsRemovalsVisible,MetricColor(GameDebugMetric.Removed));
                }
                else GUILayout.Label(MetricName(_metric),EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.Label(_reference==null?"A":"A 실선 / B 점선",EditorStyles.miniLabel);
            }
            _viewport.UpdateDuration(_duration);
            var metrics=Metrics().ToArray();
            double visibleMaximum=1; _globalMaximum=1;
            foreach (var data in new[]{_primary,_reference}) if (data!=null)
                foreach (var bucket in data) foreach (var metric in metrics)
                {
                    double value=Value(bucket,metric);
                    _globalMaximum=Math.Max(_globalMaximum,value);
                    if (bucket.Start+bucket.Width>=_viewport.StartSeconds && bucket.Start<=_viewport.EndSeconds)
                        visibleMaximum=Math.Max(visibleMaximum,value);
                }
            _viewport.FitY(visibleMaximum);
            Rect frame=GUILayoutUtility.GetRect(100,10000,200,10000,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            if (frame.width<120 || frame.height<100) return;
            var plot=new Rect(frame.x+58,frame.y+18,frame.width-80,frame.height-69);
            EditorGUI.DrawRect(plot,EditorGUIUtility.isProSkin?new Color(0.13f,0.13f,0.13f):new Color(0.92f,0.92f,0.92f));
            HandleInput(plot);
            if (Event.current.type==EventType.Repaint)
            {
                DrawAxes(plot);
                GUI.BeginGroup(plot);
                foreach (var metric in metrics)
                { DrawSeries(_primary,metric,plot,false); DrawSeries(_reference,metric,plot,true); }
                if (_selectedSeconds>=_viewport.StartSeconds && _selectedSeconds<=_viewport.EndSeconds)
                {
                    float x=TimeX(_selectedSeconds,plot.width);
                    Handles.color=EditorGUIUtility.isProSkin?Color.white:Color.black;
                    Handles.DrawLine(new Vector3(x,0),new Vector3(x,plot.height));
                }
                GUI.EndGroup();
            }
            var horizontal=new Rect(plot.x,plot.yMax+28,plot.width,16);
            float start=GUI.HorizontalScrollbar(horizontal,(float)_viewport.StartSeconds,(float)_viewport.SpanSeconds,0,(float)Math.Max(_duration,_viewport.SpanSeconds));
            if (Math.Abs(start-_viewport.StartSeconds)>0.02) _viewport.PanTime(start,_duration);
            double extent=Math.Max(_globalMaximum*1.2,_viewport.MaximumY)+_viewport.SpanY;
            double top=extent-_viewport.MaximumY;
            float next=GUI.VerticalScrollbar(new Rect(plot.xMax+3,plot.y,16,plot.height),(float)top,(float)_viewport.SpanY,0,(float)extent);
            if (Math.Abs(next-top)>0.02) _viewport.PanY(extent-next-_viewport.SpanY);
            GUILayout.Label(SelectionDescription(metrics),EditorStyles.miniLabel);
            if (_primary==null || _primary.Count==0) GUILayout.Label("NO DATA · Match 기록을 선택하세요.",EditorStyles.centeredGreyMiniLabel);
        }
        private static void LegendToggle(string text,ref bool value,Color color)
        {
            Rect swatch=GUILayoutUtility.GetRect(8,8,GUILayout.Width(8));
            EditorGUI.DrawRect(swatch,color);
            value=GUILayout.Toggle(value,text,EditorStyles.miniButton,GUILayout.Width(46));
        }
        private void DrawAxes(Rect plot)
        {
            var text=EditorStyles.miniLabel;
            var grid=EditorGUIUtility.isProSkin?new Color(0.27f,0.27f,0.27f):new Color(0.75f,0.75f,0.75f);
            for (int index=0;index<=4;index++)
            {
                float y=plot.yMax-plot.height*index/4;
                Handles.color=grid; Handles.DrawLine(new Vector3(plot.x,y),new Vector3(plot.xMax,y));
                double value=_viewport.MinimumY+_viewport.SpanY*index/4;
                GUI.Label(new Rect(plot.x-54,y-8,50,16),value.ToString("0.##",CultureInfo.InvariantCulture),text);
            }
            for (int index=0;index<=5;index++)
            {
                float x=plot.x+plot.width*index/5;
                Handles.color=grid; Handles.DrawLine(new Vector3(x,plot.y),new Vector3(x,plot.yMax));
                double seconds=_viewport.StartSeconds+_viewport.SpanSeconds*index/5;
                GUI.Label(new Rect(x-20,plot.yMax+3,48,16),seconds.ToString("0.#",CultureInfo.InvariantCulture),text);
            }
            GUI.Label(new Rect(plot.x,plot.y-18,plot.width,16),_viewport.IsTrafficView?"요청 횟수 / 객체 수 (bin)":Unit(_metric),text);
            GUI.Label(new Rect(plot.xMax-120,plot.yMax+16,120,14),_clock+" time (seconds)",text);
        }
        private float TimeX(double seconds,float width) => (float)((seconds-_viewport.StartSeconds)/_viewport.SpanSeconds)*width;
        private float ValueY(double value,float height) => height-(float)((value-_viewport.MinimumY)/_viewport.SpanY)*height;
        private void DrawSeries(IReadOnlyList<GameDebugBucket> data,GameDebugMetric metric,Rect plot,bool reference)
        {
            if (data==null) return;
            Color color=MetricColor(metric);
            if (reference) color=new Color(Mathf.Min(1,color.r+0.22f),Mathf.Min(1,color.g+0.12f),Mathf.Min(1,color.b+0.14f),0.8f);
            Handles.color=color;
            Vector3 previous=default; bool hasPrevious=false;
            foreach (var item in data)
            {
                double value=Value(item,metric), seconds=item.Start+item.Width/2;
                if (value<0) { hasPrevious=false; continue; }
                var point=new Vector3(TimeX(seconds,plot.width),ValueY(value,plot.height));
                if (hasPrevious && seconds>=_viewport.StartSeconds && seconds-item.Width<=_viewport.EndSeconds)
                {
                    if (reference) Handles.DrawDottedLine(previous,point,4);
                    else Handles.DrawAAPolyLine(2,previous,point);
                }
                if (seconds>=_viewport.StartSeconds && seconds<=_viewport.EndSeconds && point.y>=0 && point.y<=plot.height)
                    EditorGUI.DrawRect(new Rect(point.x-2,point.y-2,4,4),color);
                previous=point; hasPrevious=true;
            }
        }
        private void HandleInput(Rect plot)
        {
            var evt=Event.current;
            if (evt.type==EventType.MouseDown && plot.Contains(evt.mousePosition))
            {
                if (evt.button==0)
                { _selectedSeconds=_viewport.StartSeconds+(evt.mousePosition.x-plot.x)/plot.width*_viewport.SpanSeconds; evt.Use(); }
                if (evt.button==2)
                { _isPanning=true; _panStart=evt.mousePosition; _panTime=_viewport.StartSeconds; _panMinimum=_viewport.MinimumY; evt.Use(); }
            }
            if (evt.type==EventType.MouseDrag && _isPanning)
            {
                Vector2 delta=evt.mousePosition-_panStart;
                _viewport.PanTime(_panTime-delta.x/plot.width*_viewport.SpanSeconds,_duration);
                _viewport.PanY(_panMinimum+delta.y/plot.height*_viewport.SpanY); evt.Use();
            }
            if (evt.type==EventType.MouseUp) _isPanning=false;
            if (evt.type==EventType.ScrollWheel && plot.Contains(evt.mousePosition))
            {
                if (evt.control || evt.command) _viewport.ZoomTime(evt.delta.y>0?1.2:0.8,_duration);
                else if (evt.alt) _viewport.ZoomY(evt.delta.y>0?1.2:0.8);
                else if (evt.shift) _viewport.PanTime(_viewport.StartSeconds+evt.delta.y*_viewport.SpanSeconds/20,_duration);
                else _viewport.PanY(_viewport.MinimumY-evt.delta.y*_viewport.SpanY/20);
                evt.Use();
            }
        }
        private string SelectionDescription(GameDebugMetric[] metrics)
        {
            if (_selectedSeconds<0) return "클릭: 구간 선택 · 가운데 드래그: 이동 · Ctrl+휠: X 확대 · Alt+휠: Y 확대 · Shift+휠: 시간 이동";
            var parts=new List<string>();
            foreach (var data in new[]{_primary,_reference})
            {
                var item=data?.FirstOrDefault(b=>_selectedSeconds>=b.Start && _selectedSeconds<b.Start+b.Width);
                if (item==null) continue;
                parts.Add((ReferenceEquals(data,_primary)?"A":"B")+" "+string.Join(" / ",metrics.Select(m=>{
                    double value=Value(item,m); return MetricName(m)+": "+(value<0?"N/A":value.ToString("0.##",CultureInfo.InvariantCulture)); })));
            }
            return $"{_selectedSeconds:F2}s · "+string.Join(" | ",parts);
        }
    }
}
