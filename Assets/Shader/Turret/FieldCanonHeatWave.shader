Shader "FieldTurrets/CanonHeatWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Silhouette", 2D) = "white" {}
        _DistortionPixels ("Distortion (pixels)", Range(0, 12)) = 8
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
        [PerRendererData] _AlphaTex ("External Alpha", 2D) = "white" {}
        [PerRendererData] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha


        Pass
        {
            CGPROGRAM
            #pragma vertex HeatVert
            #pragma fragment HeatFrag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"

            sampler2D _CameraSortingLayerTexture;
            float _DistortionPixels;

            struct heat_v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float2 worldPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            heat_v2f HeatVert(appdata_t input)
            {
                v2f sprite = SpriteVert(input);
                heat_v2f output;
                output.vertex = sprite.vertex;
                output.texcoord = sprite.texcoord;
                output.worldPosition = mul(unity_ObjectToWorld, UnityFlipSprite(input.vertex.xyz, _Flip)).xy;
                output.color = sprite.color;
                return output;
            }

            fixed4 HeatFrag(heat_v2f input) : SV_Target
            {
                float mask = SampleSpriteTexture(input.texcoord).a;
                clip(mask - 0.01);

                float2 pos = input.worldPosition;
                float time = _Time.y;
                float wave = sin(pos.y * 2.8 - time * 7.0 + sin(pos.x * 1.7 + time * 1.2) * 0.7);
                float detail = sin(pos.y * 5.9 - time * 4.7 + pos.x * 2.4);
                float2 distortion = float2(wave + detail * 0.35, detail * 0.35);
                float2 screenUv = input.vertex.xy / _ScreenParams.xy;
                screenUv += distortion * (_DistortionPixels * input.color.a) / _ScreenParams.xy;
                fixed3 refracted = tex2D(_CameraSortingLayerTexture, saturate(screenUv)).rgb;
                return fixed4(refracted, mask * input.color.a * 0.8);
            }
            ENDCG
        }
    }
}
