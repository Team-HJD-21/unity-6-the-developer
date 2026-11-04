Shader "Weather/TransitionBlend"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Previous Weather", 2D) = "white" {}
        [NoScaleOffset] _NextTex ("Next Weather", 2D) = "white" {}
        _Blend ("Blend", Range(0, 1)) = 0
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            sampler2D _MainTex;
            sampler2D _NextTex;
            half _Blend;

            half4 frag(v2f input) : SV_Target
            {
                half4 previous = tex2D(_MainTex, input.uv);
                half4 next = tex2D(_NextTex, input.uv);
                return lerp(previous, next, saturate(_Blend));
            }
            ENDCG
        }
    }
}
