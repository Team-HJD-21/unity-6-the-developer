Shader "Weather/TransitionBlend"
{
    Properties
    {
        [NoScaleOffset] _NextTex ("Next Weather", 2D) = "white" {}
        _Blend ("Blend", Range(0, 1)) = 0
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

            TEXTURE2D_X(_NextTex);
            half _Blend;

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 previous = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, input.texcoord);
                half4 next = SAMPLE_TEXTURE2D_X(_NextTex, sampler_LinearClamp, input.texcoord);
                return lerp(previous, next, saturate(_Blend));
            }
            ENDHLSL
        }
    }
}
