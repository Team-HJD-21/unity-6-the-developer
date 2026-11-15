Shader "The Developer/UI/BossSuction"
{
    Properties
    {
        [PerRendererData] _MainTex ("World frame", 2D) = "black" {}
        _Amount ("Suction", Range(0,1)) = 0
        _Clock ("Animation seconds", Float) = 0
        _Aspect ("Screen aspect", Float) = 1.7778
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off ZWrite Off ZTest Always
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex;
            float _Amount, _Clock, _Aspect;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float3 sampleWorld(float2 uv)
            {
                float inside=step(0,uv.x)*step(uv.x,1)*step(0,uv.y)*step(uv.y,1);
                float edge=min(min(uv.x,1-uv.x),min(uv.y,1-uv.y));
                inside*=lerp(1,smoothstep(0,.14,edge),saturate(_Amount*3));
                return tex2D(_MainTex,saturate(uv)).rgb*inside;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 p=i.uv-.5; p.x*=_Aspect;
                float r=length(p), a=atan2(p.y,p.x);
                float strength=_Amount*_Amount;
                float twist=_Amount*3.4*exp(-r*1.8);
                float ripple=sin(r*28-_Clock*8)*.018*_Amount;
                float scale=exp(strength*(2.4+r*.7));
                float2 warped=float2(cos(a+twist),sin(a+twist))*(r+ripple)*scale;
                warped.x/=_Aspect;
                float2 uv=warped+.5;
                float2 smear=warped*.07*_Amount;
                float3 c=sampleWorld(uv)*.4;
                c+=sampleWorld(uv-smear)*.3+sampleWorld(uv-smear*2)*.2+sampleWorld(uv-smear*3)*.1;
                float split=.008*strength;
                c.r=lerp(c.r,sampleWorld(uv+warped*split).r,_Amount*.6);
                c.b=lerp(c.b,sampleWorld(uv-warped*split).b,_Amount*.6);
                float rays=pow(saturate(sin(a*21+r*6-_Clock*2)),18)*smoothstep(.12,.7,r);
                c+=float3(.13,.23,.3)*rays*strength*(1-_Amount)*2;
                c*=1-saturate(r*.65)*_Amount*.65;
                c*=1-smoothstep(.77,1,_Amount);
                return fixed4(c,1)*i.color;
            }
            ENDCG
        }
    }
}
