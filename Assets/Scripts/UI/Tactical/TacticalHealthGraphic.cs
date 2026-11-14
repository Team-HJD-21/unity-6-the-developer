using UnityEngine;
using UnityEngine.UI;

/// <summary>체력과 실제 피해 잔상을 같은 눈금 위에 표시합니다. 수치/권한은 소유 Controller가 전달합니다.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public sealed class TacticalHealthGraphic : MaskableGraphic
{
    [Range(0,1)] public float Fraction=1, Trail=1;
    public void Set(float fraction,float trail,Color tone)
    { Fraction=Mathf.Clamp01(fraction);Trail=Mathf.Max(Fraction,Mathf.Clamp01(trail));color=tone;SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();var r=rectTransform.rect;
        void Quad(float x,float y,float xx,float yy,Color c)
        {
            int i=vh.currentVertCount;vh.AddVert(new Vector2(x,y),c,Vector2.zero);vh.AddVert(new Vector2(x,yy),c,Vector2.zero);
            vh.AddVert(new Vector2(xx,yy),c,Vector2.zero);vh.AddVert(new Vector2(xx,y),c,Vector2.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
        Quad(r.xMin-1,r.yMin-1,r.xMax+1,r.yMax+1,new Color(.025f,.04f,.035f,.98f));
        Quad(r.xMin,r.yMin,r.xMax,r.yMax,new Color(.18f,.22f,.19f,.95f));
        Quad(r.xMin,r.yMin,r.xMin+r.width*Trail,r.yMax,new Color(.86f,.43f,.31f));
        Quad(r.xMin,r.yMin,r.xMin+r.width*Fraction,r.yMax,color);
        int steps=r.width>=100?10:5;
        for(int n=1;n<steps;n++){float x=r.xMin+r.width*n/steps;Quad(x-.5f,r.yMin,x+.5f,r.yMax,new Color(.03f,.05f,.04f,.7f));}
    }
}
