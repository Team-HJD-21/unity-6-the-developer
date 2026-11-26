Shader "FieldTurrets/HeatEmission"
{
    Properties
    {
        [HideInInspector] _MainTex("Sprite Texture", 2D) = "white" {}
        [NoScaleOffset] _EmissionTex("Heat Texture", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Blend SrcAlpha OneMinusSrcAlpha, One OneMinusSrcAlpha
        Cull Off
        ZWrite Off

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/Core2D.hlsl"
        // Keep the heat atlas bound when Unity batches masked sprites.
        #define _MainTex _EmissionTex
        #define sampler_MainTex sampler_EmissionTex

        struct Attributes { COMMON_2D_INPUTS half4 color : COLOR; };
        struct Varyings { COMMON_2D_OUTPUTS half4 color : COLOR; };

        #include "Packages/com.unity.render-pipelines.universal/Shaders/2D/Include/2DCommon.hlsl"

        Varyings HeatVertex(Attributes input)
        {
            SetUpSpriteInstanceProperties();
            input.positionOS = UnityFlipSprite(input.positionOS, unity_SpriteProps.xy);
            Varyings output = CommonUnlitVertex(input);
            output.color = input.color * unity_SpriteColor;
            return output;
        }

        half4 HeatFragment(Varyings input) : SV_Target
        {
            return CommonUnlitFragment(input, input.color);
        }
        ENDHLSL

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM
            #pragma vertex HeatVertex
            #pragma fragment HeatFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            ENDHLSL
        }

        Pass
        {
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex HeatVertex
            #pragma fragment HeatFragment
            #pragma multi_compile_instancing
            #pragma multi_compile _ DEBUG_DISPLAY
            ENDHLSL
        }
    }
}
