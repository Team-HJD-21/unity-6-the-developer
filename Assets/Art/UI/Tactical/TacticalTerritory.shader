Shader "The Developer/UI/TacticalTerritory"
{
 Properties
 {
  [PerRendererData] _MainTex("Region masks",2D)="black" {}
  _Captured("Captured",Color)=(.63,.78,.53,.20)
  _Available("Available",Color)=(.69,.75,.72,.035)
  _Capturing("Capturing",Color)=(.87,.68,.35,.20)
  _Unoccupied("Unoccupied",Color)=(.08,.12,.11,.055)
  _StencilComp("Stencil Comparison",Float)=8
  _Stencil("Stencil ID",Float)=0
  _StencilOp("Stencil Operation",Float)=0
  _StencilWriteMask("Stencil Write Mask",Float)=255
  _StencilReadMask("Stencil Read Mask",Float)=255
  _ColorMask("Color Mask",Float)=15
 }
 SubShader
 {
  Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
  Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
  Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
  Blend SrcAlpha OneMinusSrcAlpha ColorMask [_ColorMask]
  Pass
  {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
   #include "UnityCG.cginc"
   #include "UnityUI.cginc"
   struct a2v {float4 vertex:POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;};
   struct v2f {float4 vertex:SV_POSITION;float4 color:COLOR;float2 uv:TEXCOORD0;float4 world:TEXCOORD1;};
   sampler2D _MainTex;float4 _MainTex_TexelSize,_ClipRect,_Captured,_Available,_Capturing,_Unoccupied;
   v2f vert(a2v i){v2f o;o.world=i.vertex;o.vertex=UnityObjectToClipPos(i.vertex);o.uv=i.uv;o.color=i.color;return o;}
   fixed4 frag(v2f i):SV_Target
   {
    float2 d=_MainTex_TexelSize.xy*.8;
    float4 m=tex2D(_MainTex,i.uv)*4;
    m+=tex2D(_MainTex,i.uv+float2(d.x,0))*2+tex2D(_MainTex,i.uv-float2(d.x,0))*2;
    m+=tex2D(_MainTex,i.uv+float2(0,d.y))*2+tex2D(_MainTex,i.uv-float2(0,d.y))*2;
    m+=tex2D(_MainTex,i.uv+d)+tex2D(_MainTex,i.uv-d)+tex2D(_MainTex,i.uv+float2(d.x,-d.y))+tex2D(_MainTex,i.uv+float2(-d.x,d.y));m/=16;
    float4 tone=_Captured*m.r+_Available*m.g+_Capturing*m.b+_Unoccupied*m.a;
    // 내부 셀 선 없이 연결된 면을 표시합니다. 정확한 점유 외곽선은 별도 동일 월드 선분입니다.
    fixed4 c=float4(tone.rgb,tone.a);c*=i.color;
    #ifdef UNITY_UI_CLIP_RECT
    c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
    #endif
    return c;
   }
   ENDCG
  }
 }
}
