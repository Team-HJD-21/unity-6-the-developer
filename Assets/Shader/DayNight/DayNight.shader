// Built-in Render Pipeline의 화면을 어둡게 물들이고 가장자리에 비네팅을 더한다.
Shader "DayNight/Night"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Screen", 2D) = "white" {}
        _NightColor ("Night Color", Color) = (0.12, 0.16, 0.28, 1)
        _NightStrength ("Night Color Strength", Range(0, 1)) = 0.55
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.45
        _VignetteSize ("Vignette Size", Range(0, 1)) = 0.55
        [HideInInspector] _EffectBlend ("Effect Blend", Range(0, 1)) = 1
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
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            sampler2D _MainTex;
            half4 _NightColor;
            half _NightStrength;
            half _VignetteStrength;
            half _VignetteSize;
            half _EffectBlend;

            half4 frag(v2f input) : SV_Target
            {
                half4 color = tex2D(_MainTex, input.uv);
                color.rgb = lerp(color.rgb, color.rgb * _NightColor.rgb, _NightStrength * _EffectBlend);

                float2 centered = input.uv * 2.0 - 1.0;
                float distanceFromCenter = length(centered);
                half vignette = smoothstep(_VignetteSize, 1.4, distanceFromCenter);
                color.rgb *= 1.0 - vignette * _VignetteStrength * _EffectBlend;
                return color;
            }
            ENDCG
        }
    }

    Fallback Off
}
