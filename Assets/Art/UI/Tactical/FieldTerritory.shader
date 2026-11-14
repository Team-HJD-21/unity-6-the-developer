// 확정 점유 경계의 양쪽에 투명한 바닥 띠와 약한 흐름을 그립니다.
// 실제 월드 객체의 Sorting Layer 아래에서 동작하며 조명·피격 색은 변경하지 않습니다.
Shader "The Developer/World/FieldTerritory"
{
    Properties
    {
        _InsideColor("Captured side", Color) = (.25,.70,.41,.24)
        _OutsideColor("Uncaptured side", Color) = (.76,.36,.35,.20)
        _InnerWidth("Inside width (world)", Float) = 3.5
        _OuterWidth("Outside width (world)", Float) = 2.7
        _Falloff("Opacity falloff", Range(.5,4)) = 1.7
        _LineOpacity("Thin boundary opacity", Range(0,1)) = .16
        _FlowIntensity("Flow intensity", Range(0,1)) = .12
        _FlowSpeed("Flow cycles per second", Range(0,1)) = .12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            Tags { "LightMode"="Universal2D" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
                float4 _InsideColor, _OutsideColor;
                float _InnerWidth, _OuterWidth, _Falloff, _LineOpacity, _FlowIntensity, _FlowSpeed;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float inside = step(0, input.uv.x);
                half4 tint = lerp(_OutsideColor, _InsideColor, inside);
                float distance = abs(input.uv.x);
                float fade = pow(saturate(1-distance), _Falloff);
                float edgeOpacity = 1-smoothstep(0,.045,distance);
                float flow = pow(saturate(.5+.5*cos(6.2831853*(input.uv.y-_Time.y*_FlowSpeed))),10);
                // 바깥쪽은 일정한 낮은 밝기를 유지하고 안쪽에만 빛을 흐르게 합니다.
                float highlight = flow * _FlowIntensity * exp(-distance*6) * inside;
                return half4(lerp(tint.rgb, min(tint.rgb+.12,1), flow*.2*inside),
                    saturate(tint.a*fade + edgeOpacity*_LineOpacity + highlight));
            }
            ENDHLSL
        }
    }
}
