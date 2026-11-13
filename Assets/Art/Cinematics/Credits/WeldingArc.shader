Shader "TheDeveloper/Credits/WeldingArc"
{
    Properties
    {
        _Phase ("Arc phase", Float) = 0
        _Intensity ("Arc intensity", Range(0,2)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Cull Off ZWrite Off Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct input { float4 vertex:POSITION; float2 uv:TEXCOORD0; };
            struct output { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; };
            float _Phase, _Intensity;
            output vert(input v) { output o; o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;return o; }
            fixed4 frag(output i):SV_Target
            {
                float2 p=(i.uv-.5)*2;
                float r=length(p), a=atan2(p.y,p.x);
                float core=exp(-r*r*95);
                float corona=exp(-r*r*15)*(.72+.18*sin(a*7+_Phase)+.1*sin(a*13-_Phase*1.7));
                float rays=pow(saturate(cos(a*5+_Phase*.2)),18)*exp(-r*8)*.22;
                float3 color=float3(.72,.88,1)*core*1.8+float3(.14,.3,.5)*(corona+rays);
                return float4(color*_Intensity,0);
            }
            ENDCG
        }
    }
}
