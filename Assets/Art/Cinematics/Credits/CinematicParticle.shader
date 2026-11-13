Shader "TheDeveloper/Credits/HotParticle"
{
    Properties { _MainTex ("Soft particle", 2D) = "white" {} _Intensity ("Light intensity", Range(0,4)) = 2.2 }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True"}
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex; float _Intensity;
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; };
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex);o.color=v.color;o.uv=v.uv;return o; }
            half4 frag(v2f i):SV_Target
            {
                float2 p=(i.uv-.5)*2;
                float falloff=pow(saturate(1-dot(p,p)),1.5);
                float a=tex2D(_MainTex,i.uv).a*falloff;
                return half4(i.color.rgb*_Intensity,i.color.a*a);
            }
            ENDCG
        }
    }
}
