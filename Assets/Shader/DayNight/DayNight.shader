Shader "DayNight/Night"
{
    Properties
    {
        _NightColor ("Night Color", Color) = (0.12, 0.16, 0.28, 1)
        _NightStrength ("Night Color Strength", Range(0, 1)) = 0.55
        _VignetteStrength ("Vignette Strength", Range(0, 1)) = 0.45
        _VignetteSize ("Vignette Size", Range(0, 1)) = 0.55
        [HideInInspector] _EffectBlend ("Effect Blend", Range(0, 1)) = 1
        [HideInInspector] _PlayerLightPosition ("Player Light Position", Vector) = (0, 0, 0, 0)
        [HideInInspector] _PlayerLightRange ("Player Light Range", Vector) = (5, 9, 0, 0)
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "../Weather/WeatherCoordinates.hlsl"

            half4 _NightColor;
            half _NightStrength;
            half _VignetteStrength;
            half _VignetteSize;
            half _EffectBlend;
            float4 _PlayerLightPosition;
            float4 _PlayerLightRange;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 effectUV = StageCameraViewportUV(input.positionCS);
                float distanceToPlayer = distance(StageCameraWorldXY(input.positionCS), _PlayerLightPosition.xy);
                half light = (1.0 - smoothstep(_PlayerLightRange.x, _PlayerLightRange.y, distanceToPlayer)) * _PlayerLightRange.z;
                half darkness = _EffectBlend * (1.0 - light);
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                color.rgb = lerp(color.rgb, color.rgb * _NightColor.rgb, _NightStrength * darkness);

                float2 centered = effectUV * 2.0 - 1.0;
                float distanceFromCenter = length(centered);
                half vignette = smoothstep(_VignetteSize, 1.4, distanceFromCenter);
                color.rgb *= 1.0 - vignette * _VignetteStrength * darkness;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
