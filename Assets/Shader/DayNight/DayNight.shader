Shader "DayNight/Night"
{
    Properties
    {
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
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _NightColor;
            half _NightStrength;
            half _VignetteStrength;
            half _VignetteSize;
            half _EffectBlend;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 effectUV = input.texcoord;
                #if UNITY_UV_STARTS_AT_TOP
                    effectUV.y = 1.0 - effectUV.y;
                #endif
                half4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                color.rgb = lerp(color.rgb, color.rgb * _NightColor.rgb, _NightStrength * _EffectBlend);

                float2 centered = effectUV * 2.0 - 1.0;
                float distanceFromCenter = length(centered);
                half vignette = smoothstep(_VignetteSize, 1.4, distanceFromCenter);
                color.rgb *= 1.0 - vignette * _VignetteStrength * _EffectBlend;
                return color;
            }
            ENDHLSL
        }
    }

    Fallback Off
}
