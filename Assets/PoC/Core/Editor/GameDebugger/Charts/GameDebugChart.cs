// 시간 구간 막대/선을 UI Toolkit의 Painter2D로 그립니다.
// N/A 표본은 선을 잇지 않으며 A/B는 동일한 시간 축 위에 표시합니다.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugChart : VisualElement
    {
        private IReadOnlyList<GameDebugBucket> _primary, _reference;
        private GameDebugMetric _metric;
        private double _duration=1, _maximum=1;
        public string AxisDescription => $"0 .. {_duration:F1}s   |   Y 0 .. {_maximum:F2}   |   A cyan / B orange   |   gaps = N/A";
        public GameDebugChart()
        {
            style.height = 250; style.minHeight=180;
            generateVisualContent += Draw;
            RegisterCallback<MouseMoveEvent>(OnHover);
        }
        public void SetData(IReadOnlyList<GameDebugBucket> primary, IReadOnlyList<GameDebugBucket> reference, GameDebugMetric metric)
        {
            _primary=primary; _reference=reference; _metric=metric; _duration=_maximum=1;
            foreach (var list in new[] { primary, reference })
                if (list != null) foreach (var item in list)
                { _duration=Math.Max(_duration,item.Start+item.Width); _maximum=Math.Max(_maximum,Value(item,metric)); }
            MarkDirtyRepaint();
        }
        public static double Value(GameDebugBucket item, GameDebugMetric metric)
        {
            bool eventMetric=metric==GameDebugMetric.Spawned || metric==GameDebugMetric.Removed || metric==GameDebugMetric.Planned || metric==GameDebugMetric.Executed || metric==GameDebugMetric.Failures || metric==GameDebugMetric.CumulativeSpawned;
            if (!item.HasFullEventCoverage && eventMetric)
            {
                int count=metric==GameDebugMetric.Spawned?item.Spawned:metric==GameDebugMetric.Removed?item.Removed:metric==GameDebugMetric.Planned?item.Planned:metric==GameDebugMetric.Executed?item.Executed:metric==GameDebugMetric.Failures?item.Failures:item.CumulativeSpawned;
                if (count==0) return -1;
            }
            switch (metric)
            {
                case GameDebugMetric.Spawned: return item.Spawned;
                case GameDebugMetric.Removed: return item.Removed;
                case GameDebugMetric.Alive: return item.Alive;
                case GameDebugMetric.CumulativeSpawned: return item.CumulativeSpawned;
                case GameDebugMetric.Planned: return item.Planned;
                case GameDebugMetric.Executed: return item.Executed;
                case GameDebugMetric.Failures: return item.Failures;
                case GameDebugMetric.AvailablePower: return item.AvailablePower;
                case GameDebugMetric.OperationalRatio: return item.OperationalRatio;
                case GameDebugMetric.TerritoryArea: return item.Area;
                default: return item.FrontlineLength;
            }
        }
        private bool IsHistogram => _metric==GameDebugMetric.Spawned || _metric==GameDebugMetric.Removed || _metric==GameDebugMetric.Planned || _metric==GameDebugMetric.Executed || _metric==GameDebugMetric.Failures;
        private void Draw(MeshGenerationContext context)
        {
            float width=contentRect.width, height=contentRect.height-12;
            if (width<=0 || height<=0) return;
            var painter=context.painter2D;
            painter.strokeColor=new Color(0.3f,0.35f,0.4f); painter.lineWidth=1;
            for (int index=0;index<=4;index++)
            { float y=height*index/4; painter.BeginPath(); painter.MoveTo(new Vector2(0,y)); painter.LineTo(new Vector2(width,y)); painter.Stroke(); }
            DrawSeries(painter,_primary,new Color(0.2f,0.8f,0.85f),width,height,false);
            DrawSeries(painter,_reference,new Color(1f,0.65f,0.3f),width,height,true);
        }
        private void DrawSeries(Painter2D painter, IReadOnlyList<GameDebugBucket> data, Color color, float width, float height, bool reference)
        {
            if (data==null) return;
            painter.strokeColor=color; painter.fillColor=new Color(color.r,color.g,color.b,reference?0.45f:0.65f); painter.lineWidth=2;
            bool drawing=false;
            foreach (var bucket in data)
            {
                double value=Value(bucket,_metric);
                if (value<0) { if (drawing) painter.Stroke(); drawing=false; continue; }
                float x=(float)(bucket.Start/_duration)*width, y=height-(float)(value/_maximum)*height;
                if (IsHistogram)
                {
                    float size=Math.Max(1,(float)(bucket.Width/_duration)*width-2);
                    painter.BeginPath(); painter.MoveTo(new Vector2(x,height)); painter.LineTo(new Vector2(x,y));
                    painter.LineTo(new Vector2(x+size,y)); painter.LineTo(new Vector2(x+size,height)); painter.ClosePath(); painter.Fill();
                }
                else
                {
                    x+=(float)(bucket.Width/_duration)*width*0.5f;
                    if (!drawing) { painter.BeginPath(); painter.MoveTo(new Vector2(x,y)); drawing=true; }
                    else painter.LineTo(new Vector2(x,y));
                }
            }
            if (drawing) painter.Stroke();
            // 하나의 유효 표본만 있어도 보이도록 선 그래프에 작은 정점을 표시합니다.
            if (!IsHistogram) foreach (var bucket in data)
            {
                double value=Value(bucket,_metric); if (value<0) continue;
                float x=(float)((bucket.Start+bucket.Width*0.5)/_duration)*width, y=height-(float)(value/_maximum)*height;
                painter.fillColor=color; painter.BeginPath(); painter.MoveTo(new Vector2(x-1.5f,y-1.5f));
                painter.LineTo(new Vector2(x+1.5f,y-1.5f)); painter.LineTo(new Vector2(x+1.5f,y+1.5f)); painter.LineTo(new Vector2(x-1.5f,y+1.5f)); painter.ClosePath(); painter.Fill();
            }
        }
        private void OnHover(MouseMoveEvent evt)
        {
            double seconds=evt.localMousePosition.x/Math.Max(1,contentRect.width)*_duration;
            tooltip=$"{seconds:F2}s";
            foreach (var data in new[] { _primary, _reference })
                if (data!=null) foreach (var item in data)
                    if (seconds>=item.Start && seconds<item.Start+item.Width)
                    { double value=Value(item,_metric); tooltip+=$"\n{(ReferenceEquals(data,_primary)?"A":"B")} [{item.Start:F1}, {item.Start+item.Width:F1}): {(value<0?"N/A":value.ToString("F2"))}{(item.HasFullEventCoverage?"":" · partial/retained")}"; break; }
        }
    }
}
