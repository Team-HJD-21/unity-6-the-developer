// 판단 당시 XY Grid·전선·후보·실제 생성 위치를 읽기 전용으로 그립니다.
// 현재 Scene 계산을 재실행하지 않으며 선택은 상세 표와 연동됩니다.
using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
namespace TeamHJD.Game.Editor
{
    public sealed class GameDebugSpawnMap : VisualElement
    {
        private GameDebugSpawnDecision _decision;
        private string _selectedPoint="";
        private string _monsterFilter="All";
        private readonly GameDebugSpawnViewState _view;
        private Vector2 _dragStart, _drag, _panStart;
        private int _pointerId=-1;
        private bool _dragging;
        public event Action<string> PointSelected;
        public event Action<string> ActualSelected;
        public GameDebugSpawnMap() : this(new GameDebugSpawnViewState()) { }
        public GameDebugSpawnMap(GameDebugSpawnViewState view)
        {
            _view=view ?? throw new ArgumentNullException(nameof(view));
            _view.Validate();
            name="spawn-map";
            style.height=320;style.minHeight=220;style.overflow=Overflow.Hidden;
            style.backgroundColor=EditorGUIUtility.isProSkin?new Color(.13f,.13f,.13f):new Color(.85f,.85f,.85f);
            var xAxis=new Label("X"){pickingMode=PickingMode.Ignore};xAxis.style.position=Position.Absolute;xAxis.style.left=58;xAxis.style.bottom=8;Add(xAxis);
            var yAxis=new Label("Y"){pickingMode=PickingMode.Ignore};yAxis.style.position=Position.Absolute;yAxis.style.left=5;yAxis.style.bottom=62;Add(yAxis);
            generateVisualContent+=Draw;
            RegisterCallback<WheelEvent>(e=>{_view.Zoom=Mathf.Clamp(_view.Zoom*Mathf.Pow(1.1f,-e.delta.y),.5f,8);MarkDirtyRepaint();e.StopPropagation();});
            RegisterCallback<PointerDownEvent>(e=>{
                if(e.button!=0 && e.button!=2)return;
                _dragStart=_drag=e.localPosition;_panStart=_view.Pan;
                _dragging=true;_pointerId=e.pointerId;this.CapturePointer(e.pointerId);
                e.StopPropagation();
            });
            RegisterCallback<PointerMoveEvent>(e=>{
                if(!_dragging || e.pointerId!=_pointerId)return;
                var p=(Vector2)e.localPosition;_view.Pan+=p-_drag;_drag=p;MarkDirtyRepaint();e.StopPropagation();
            });
            RegisterCallback<PointerUpEvent>(e=>{
                if(!_dragging || e.pointerId!=_pointerId)return;
                _dragging=false;_pointerId=-1;this.ReleasePointer(e.pointerId);e.StopPropagation();
                // 작은 손 떨림은 클릭으로 처리하되 지도 이동을 요청 선택으로 오인하지 않습니다.
                if (!IsClickGesture(_dragStart,e.localPosition) || e.button!=0) return;
                _view.Pan=_panStart;
                MarkDirtyRepaint();
                if(_decision==null)return;
                foreach(var actual in _decision.Actuals)
                    if((_monsterFilter=="All"||actual.Enemy==_monsterFilter)&&Vector2.Distance(Project(actual.X,actual.Y),e.localPosition)<4)
                    {ActualSelected?.Invoke(actual.Entity);return;}
                GameDebugSpawnDecision.Candidate best=null;float distance=12;
                foreach(var c in _decision.Candidates) {
                    float d=Vector2.Distance(Project(c.X,c.Y),e.localPosition);
                    if(d<distance){distance=d;best=c;}
                }
                if(best!=null)PointSelected?.Invoke(best.Id);
            });
            RegisterCallback<PointerCaptureOutEvent>(_=>{_dragging=false;_pointerId=-1;});
            RegisterCallback<MouseMoveEvent>(e=>{
                tooltip="XY · 휠: 확대/축소 · 드래그: 이동 · 지점: 상세 선택";
                if(_decision==null)return;
                foreach(var a in _decision.Actuals)
                    if((_monsterFilter=="All"||a.Enemy==_monsterFilter)&&Vector2.Distance(Project(a.X,a.Y),e.localMousePosition)<4)
                    {tooltip=$"Object {a.Entity} · {a.Enemy} · point {a.Point}\nSpawned XY ({a.X:F2}, {a.Y:F2})";return;}
                foreach(var c in _decision.Candidates)
                    if(Vector2.Distance(Project(c.X,c.Y),e.localMousePosition)<10)
                        tooltip=$"{c.Id} · cell({c.CellX},{c.CellY}) · enemies {c.Enemies} · sets {c.Sets}\n{c.Reason}";
            });
        }
        public void SetData(GameDebugSpawnDecision decision,string selectedPoint)
        {_decision=decision;_selectedPoint=selectedPoint??"";MarkDirtyRepaint();}
        public void FrameAll(){_view.Zoom=1;_view.Pan=Vector2.zero;MarkDirtyRepaint();}
        public static bool IsClickGesture(Vector2 start,Vector2 end) => (end-start).sqrMagnitude<=16;
        public void SetMonsterFilter(string enemy){_monsterFilter=enemy??"All";MarkDirtyRepaint();}
        private Vector2 Project(double x,double y)
        {
            var g=_decision.Grid;
            float scale=Math.Min(Math.Max(1,contentRect.width-40)/(float)g.Width,Math.Max(1,contentRect.height-40)/(float)g.Height)*_view.Zoom;
            return new Vector2(contentRect.width*.5f+(float)(x-g.CenterX)*scale,contentRect.height*.5f-(float)(y-g.CenterY)*scale)+_view.Pan;
        }
        private static void Line(Painter2D p,Vector2 a,Vector2 b,Color color,float width=1)
        {p.strokeColor=color;p.lineWidth=width;p.BeginPath();p.MoveTo(a);p.LineTo(b);p.Stroke();}
        private static void Dot(Painter2D p,Vector2 at,float radius,Color color)
        {p.fillColor=color;p.BeginPath();p.Arc(at,radius,Angle.Degrees(0),Angle.Degrees(360));p.ClosePath();p.Fill();}
        private void Draw(MeshGenerationContext context)
        {
            if(_decision?.Grid==null)return;
            var p=context.painter2D;var g=_decision.Grid;double minX=g.CenterX-g.Width*.5,minY=g.CenterY-g.Height*.5;
            foreach(var cell in _decision.Cells)
            {
                var a=Project(minX+cell.X*g.Width/g.CellsX,minY+(cell.Y+1)*g.Height/g.CellsY);
                var b=Project(minX+(cell.X+1)*g.Width/g.CellsX,minY+cell.Y*g.Height/g.CellsY);
                p.fillColor=cell.Enemies>0?new Color(.68f,.28f,.21f,.12f+Math.Min(.4f,cell.Enemies*.03f)):new Color(.3f,.55f,.4f,.3f);
                p.BeginPath();p.MoveTo(a);p.LineTo(new Vector2(b.x,a.y));p.LineTo(b);p.LineTo(new Vector2(a.x,b.y));p.ClosePath();p.Fill();
            }
            Color grid=new Color(.5f,.5f,.5f,.25f);
            // 거대한 authored Grid는 표시선만 샘플링합니다. 판단 셀 좌표는 원본 값을 유지합니다.
            int strideX=Math.Max(1,(int)Math.Ceiling(g.CellsX/128d)),strideY=Math.Max(1,(int)Math.Ceiling(g.CellsY/128d));
            for(int x=0;x<=g.CellsX;x+=strideX)
                Line(p,Project(minX+x*g.Width/g.CellsX,minY),Project(minX+x*g.Width/g.CellsX,minY+g.Height),grid);
            for(int y=0;y<=g.CellsY;y+=strideY)
                Line(p,Project(minX,minY+y*g.Height/g.CellsY),Project(minX+g.Width,minY+y*g.Height/g.CellsY),grid);
            foreach(var e in _decision.Frontline)Line(p,Project(e.X1,e.Y1),Project(e.X2,e.Y2),GameDebugVisualStyle.Frontline,2);
            foreach(var c in _decision.Candidates)
            {
                var at=Project(c.X,c.Y);
                bool selected=c.Id==_selectedPoint;
                if(selected)Dot(p,at,8,GameDebugVisualStyle.Selection);
                // 미배정에는 적합 후보의 예산 소진도 포함됩니다. 전부 '제외' 빨강으로 표시하지 않습니다.
                Dot(p,at,selected?5:4,c.Sets>0?GameDebugVisualStyle.AllocatedPoint:GameDebugVisualStyle.UnallocatedPoint);
            }
            foreach(var a in _decision.Actuals)
                if(_monsterFilter=="All"||a.Enemy==_monsterFilter)
                {
                    var point=Project(a.X,a.Y);
                    if(a.Entity==_view.SelectedEntity)Dot(p,point,6,GameDebugVisualStyle.Selection);
                    Dot(p,point,2,GameDebugVisualStyle.Series(GameDebugMetric.Spawned,EditorGUIUtility.isProSkin));
                }
            Line(p,new Vector2(12,contentRect.height-18),new Vector2(55,contentRect.height-18),Color.gray);
            Line(p,new Vector2(12,contentRect.height-18),new Vector2(12,contentRect.height-60),Color.gray);
        }
    }
}
