// Built-in Render Pipeline의 OnRenderImage/Graphics.Blit에서 사용하는 화면 효과.
Shader "Weather/HeatWave"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Screen", 2D) = "white" {}
        _HeatTint ("Heat Tint", Color) = (1, 0.28, 0.38, 1)
        _HeatTintStrength ("Heat Tint Strength", Range(0, 1)) = 0.55
        _LightBrightness ("Light Brightness", Range(0, 3)) = 1.2
        _LightHazeStrength ("Light Haze Strength", Range(0, 1)) = 0.45
        _LightHazeLength ("Light Haze Length", Range(0.05, 1)) = 0.45
        _ScreenHazeStrength ("Screen Haze Strength", Range(0, 1)) = 0.35
        _ScreenHazeLength ("Screen Haze Length", Range(0.02, 0.5)) = 0.10
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
            half4 _HeatTint;
            half _HeatTintStrength;
            half _LightBrightness;
            half _LightHazeStrength;
            float _LightHazeLength;
            half _ScreenHazeStrength;
            float _ScreenHazeLength;

            half4 frag(v2f input) : SV_Target
            {
                float2 uv = input.uv;
                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 lightPosition = float2(1.0, 1.0);
                float2 fromLight = float2((uv.x - lightPosition.x) * aspect,
                    uv.y - lightPosition.y);
                float distanceToLight = length(fromLight);

                // 광원 아래로 내려갈수록 넓어지는 뜨거운 공기 영역을 만든다.
                float belowLight = max(lightPosition.y - uv.y, 0.0);
                float coneWidth = 0.10 + belowLight * 0.28;
                float lightHazeMask = exp(-pow(fromLight.x / coneWidth, 2.0))
                    * exp(-belowLight / max(_LightHazeLength, 0.001))
                    * step(uv.y, lightPosition.y);
                lightHazeMask = max(lightHazeMask,
                    exp(-distanceToLight * distanceToLight * 45.0) * 0.55);

                // 두 아지랑이의 파장과 강도를 독립적으로 적용한다.
                float globalWave = sin(uv.y * 6.2831853
                    / max(_ScreenHazeLength, 0.001) + _Time.y * 3.4
                    + sin(uv.x * 17.0 + _Time.y * 1.2) * 0.7);
                float globalDetail = sin(uv.y * 6.2831853
                    / max(_ScreenHazeLength * 0.47, 0.001) - _Time.y * 4.7
                    + uv.x * 9.0);
                float lightWave = sin(uv.y * 6.2831853
                    / max(_LightHazeLength * 0.22, 0.001) + _Time.y * 7.0
                    + uv.x * 24.0);
                float2 distortion = float2(
                    (globalWave + globalDetail * 0.35) * _ScreenHazeStrength * 0.003
                        + lightWave * lightHazeMask * _LightHazeStrength * 0.009,
                    globalDetail * _ScreenHazeStrength * 0.0015
                        + globalWave * lightHazeMask * _LightHazeStrength * 0.002);

                half4 color = tex2D(_MainTex, saturate(uv + distortion));
                // 원본 장면의 명암을 남기면서 전체 화면을 선홍색으로 물들인다.
                color.rgb = lerp(color.rgb,
                    color.rgb * lerp(half3(1, 1, 1), _HeatTint.rgb, 0.45)
                        + _HeatTint.rgb * 0.12,
                    _HeatTintStrength);

                float core = exp(-distanceToLight * distanceToLight * 150.0);
                float halo = exp(-distanceToLight * distanceToLight * 11.0);
                color.rgb += half3(1.0, 0.79, 0.63)
                    * _LightBrightness * (core * 0.85 + halo * 0.26);
                return color;
            }
            ENDCG
        }
    }
}
