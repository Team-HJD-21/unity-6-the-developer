using UnityEngine;
using UnityEngine.UI;

/// <summary>기존 HUD와 같은 얇은 단색 벡터 심볼. 폰트 문자로 아이콘을 대신하지 않습니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TacticalSymbolGraphic : MaskableGraphic
{
    public enum Symbol { Map, Settings, Captured, Available, Capturing, Unoccupied, Frontline, Previous, Close, ClearGoal, RangeAll, RangeSelected, TerrainVisible, TerrainHidden, Active, Off, Locked, Cooling, NoPower, Destroyed, Busy, Threat, Impact, OutsideTerritory, RegionExit }
    public Symbol Shape;
    public void Set(Symbol shape,Color tone){Shape=shape;color=tone;SetVerticesDirty();}
    public static Symbol ForState(TacticalPresentationRules.TurretState state)=>state switch
    {
        TacticalPresentationRules.TurretState.Active=>Symbol.Active,TacticalPresentationRules.TurretState.Off=>Symbol.Off,
        TacticalPresentationRules.TurretState.Locked=>Symbol.Locked,TacticalPresentationRules.TurretState.Cooling=>Symbol.Cooling,
        TacticalPresentationRules.TurretState.NoPower=>Symbol.NoPower,TacticalPresentationRules.TurretState.Destroyed=>Symbol.Destroyed,_=>Symbol.Busy
    };
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;float unit=Mathf.Min(r.width,r.height)/24;
        Vector2 P(float x,float y)=>r.center+new Vector2(x,y)*unit;
        void Line(float x,float y,float xx,float yy,float width=1.5f)
        {
            Vector2 a=P(x,y),b=P(xx,yy),n=(b-a).normalized;n=new Vector2(-n.y,n.x)*width*unit*.5f;
            int i=vh.currentVertCount;vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);
            vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        void Box(float left,float bottom,float right,float top)
        {Line(left,bottom,left,top);Line(left,top,right,top);Line(right,top,right,bottom);Line(right,bottom,left,bottom);}
        void Circle(float radius,float start=0,float end=360)
        {for(int n=0;n<24;n++){float a=Mathf.Lerp(start,end,n/24f)*Mathf.Deg2Rad,b=Mathf.Lerp(start,end,(n+1)/24f)*Mathf.Deg2Rad;Line(Mathf.Cos(a)*radius,Mathf.Sin(a)*radius,Mathf.Cos(b)*radius,Mathf.Sin(b)*radius);}}
        switch(Shape)
        {
            case Symbol.OutsideTerritory:
                Line(-8,8,-3,8);Line(3,8,8,8);Line(-8,-8,-3,-8);Line(3,-8,8,-8);
                Line(-8,8,-8,3);Line(-8,-3,-8,-8);Line(8,8,8,3);Line(8,-3,8,-8);break;
            case Symbol.RegionExit:
                Line(-9,8,-4,8);Line(1,8,5,8);Line(-9,-8,-4,-8);Line(1,-8,5,-8);
                Line(-9,8,-9,3);Line(-9,-3,-9,-8);Line(5,8,5,4);Line(5,-4,5,-8);
                Line(-2,0,11,0,2);Line(7,4,11,0,2);Line(7,-4,11,0,2);break;
            case Symbol.Capturing: Circle(8,25,325);Line(0,-3,0,3,2);break;
            case Symbol.Previous:
                Line(7,6,-6,6);Line(-6,6,-6,-6);Line(-6,-6,7,-6);Line(-6,6,-1,11);Line(-6,6,-11,1);break;
            case Symbol.Close: Line(-6,-6,6,6,2);Line(-6,6,6,-6,2);break;
            case Symbol.ClearGoal: Circle(8);Line(-5,-5,5,5,2);Line(-3,10,-3,3);Line(-3,10,4,7);break;
            case Symbol.RangeAll: Circle(9);Circle(4);Line(-11,0,-7,0);Line(7,0,11,0);break;
            case Symbol.RangeSelected: Circle(9,25,155);Circle(9,205,335);Box(-3,-3,3,3);break;
            case Symbol.TerrainVisible:
            case Symbol.TerrainHidden:
                Line(-9,-7,-3,7);Line(-3,7,3,-7);Line(-9,-7,3,-7);Line(1,-7,6,4);Line(6,4,10,-7);Line(1,-7,10,-7);
                if(Shape==Symbol.TerrainHidden)Line(-10,10,10,-10,2);break;
            case Symbol.Active: Circle(7,140,400);Line(0,2,0,10,2);break;
            case Symbol.Off: Circle(7);Line(-4,0,4,0,2);break;
            case Symbol.Locked: Box(-6,-8,6,1);Line(-4,1,-4,6);Line(-4,6,4,6);Line(4,6,4,1);Line(0,-2,0,-5);break;
            case Symbol.Cooling: Circle(8);Line(0,0,0,5);Line(0,0,4,-2);break;
            case Symbol.NoPower: Line(2,9,-4,0,2);Line(-4,0,4,0,2);Line(4,0,-2,-9,2);break;
            case Symbol.Destroyed: Line(-7,-7,7,7,2);Line(-7,7,7,-7,2);break;
            case Symbol.Busy: Line(-3,-6,-3,6,3);Line(3,-6,3,6,3);break;
            case Symbol.Threat: Circle(7);Line(-10,0,-4,0);Line(4,0,10,0);Line(0,-10,0,-4);Line(0,4,0,10);break;
            case Symbol.Impact: Line(-8,2,-3,2,2);Line(-3,2,0,-7,2);Line(0,-7,3,8,2);Line(3,8,5,2,2);Line(5,2,9,2,2);break;
            case Symbol.Map:
                Line(-10,-7,-10,8);Line(-10,8,-3,5);Line(-3,5,4,8);Line(4,8,10,5);Line(10,5,10,-9);
                Line(10,-9,4,-6);Line(4,-6,-3,-9);Line(-3,-9,-10,-7);Line(-3,-9,-3,5);Line(4,-6,4,8);
                Line(-7,-3,-5,0);Line(-5,0,0,-2);Line(0,-2,6,2,2.1f);break;
            case Symbol.Settings:
                for(int i=0;i<3;i++){float y=7-i*7;Line(-9,y,9,y);float x=i==1?4:-3;Box(x-2,y-2,x+2,y+2);}break;
            case Symbol.Frontline:
                Line(-10,-5,-3,5,2.2f);Line(-3,5,3,-2,2.2f);Line(3,-2,10,6,2.2f);break;
            default:
                Box(-8,-8,8,8);
                if(Shape==Symbol.Captured){Line(-5,0,-1,-4,2);Line(-1,-4,5,5,2);}
                if(Shape==Symbol.Capturing){Line(-6,-6,6,6);Line(-6,6,6,-6);}
                if(Shape==Symbol.Unoccupied){Line(-5,-5,5,5);}
                break;
        }
    }
}
