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
        [HideInInspector] _ExpansionField("Old/confirmed distance and arrival",2D)="black" {}
        [HideInInspector] _BoundaryBlend("Settled boundary",Range(0,1))=1
        [HideInInspector] _ExpansionTravel("Wave distance",Float)=0
        [HideInInspector] _ExpansionLoss("Loss transition",Float)=0
        [HideInInspector] _ExpansionGlow("New ground glow",Float)=0
        [HideInInspector] _ExpansionCarryFade("Previous ground afterglow",Float)=0
        [HideInInspector] _WaveWidth("Soft wave width",Float)=1
        [HideInInspector] _ExpansionPixelSize("Pixel size and widths",Vector)=(1,1,3.5,2.7)
        _ExpansionColor("New ground accent",Color)=(.40,.76,.50,.16)
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
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float3 positionOS:POSITION; float4 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float4 uv:TEXCOORD0; };
            TEXTURE2D(_ExpansionField); SAMPLER(sampler_ExpansionField);
            CBUFFER_START(UnityPerMaterial)
                float4 _InsideColor, _OutsideColor, _ExpansionColor, _ExpansionPixelSize, _ExpansionField_TexelSize;
                float _InnerWidth, _OuterWidth, _Falloff, _LineOpacity, _FlowIntensity, _FlowSpeed;
                float _BoundaryBlend, _ExpansionTravel, _ExpansionLoss, _ExpansionGlow, _ExpansionCarryFade, _WaveWidth;
            CBUFFER_END
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS);
                output.uv = input.uv;
                return output;
            }
            float4 ReadField(float2 uv) { return SAMPLE_TEXTURE2D(_ExpansionField,sampler_ExpansionField,uv); }
            float DisplayDistance(float4 field)
            {
                return _ExpansionLoss>.5 ? lerp(field.r,field.g,_BoundaryBlend) :
                    max(field.r,min(field.g,_ExpansionTravel-field.b));
            }
            half4 Expansion(float2 uv)
            {
                float4 field=ReadField(uv);
                float2 stepUV=_ExpansionField_TexelSize.xy;
                float2 gradient=float2(
                    DisplayDistance(ReadField(uv+float2(stepUV.x,0)))-DisplayDistance(ReadField(uv-float2(stepUV.x,0))),
                    DisplayDistance(ReadField(uv+float2(0,stepUV.y)))-DisplayDistance(ReadField(uv-float2(0,stepUV.y))));
                gradient/=max(_ExpansionPixelSize.xy*2,.00001);
                float signedDistance=DisplayDistance(field)/max(length(gradient),.25);
                float inside=step(0,signedDistance);
                half4 tone=lerp(_OutsideColor,_InsideColor,inside);
                float width=max(.001,lerp(_ExpansionPixelSize.w,_ExpansionPixelSize.z,inside));
                float distance=abs(signedDistance)/width;
                float alpha=tone.a*pow(saturate(1-distance),_Falloff)+(1-smoothstep(0,.045,distance))*_LineOpacity;
                float fresh=abs(field.a)*step(0,field.g);
                float reveal=smoothstep(-_WaveWidth*.3,_WaveWidth*.3,_ExpansionTravel-field.b);
                float front=(1-smoothstep(0,_WaveWidth,abs(_ExpansionTravel-field.b)))*fresh;
                float glow=(max(field.a,0)*reveal*_ExpansionGlow+max(-field.a,0)*_ExpansionCarryFade)*step(0,field.g);
                float groundAlpha=_ExpansionColor.a*glow;
                alpha=alpha*(1-_BoundaryBlend)+groundAlpha;
                float3 color=lerp(tone.rgb,_ExpansionColor.rgb,saturate(glow+front*.25));
                return half4(color,saturate(alpha));
            }
            half4 Frag(Varyings input):SV_Target
            {
                if(input.uv.z>.5)return Expansion(input.uv.xy);
                float inside = step(0, input.uv.x);
                half4 tint = lerp(_OutsideColor, _InsideColor, inside);
                float distance = abs(input.uv.x);
                float fade = pow(saturate(1-distance), _Falloff);
                float edgeOpacity = 1-smoothstep(0,.045,distance);
                float flow = pow(saturate(.5+.5*cos(6.2831853*(input.uv.y-_Time.y*_FlowSpeed))),10);
                // 바깥쪽은 일정한 낮은 밝기를 유지하고 안쪽에만 빛을 흐르게 합니다.
                float highlight = flow * _FlowIntensity * exp(-distance*6) * inside;
                return half4(lerp(tint.rgb, min(tint.rgb+.12,1), flow*.2*inside),
                    saturate(tint.a*fade + edgeOpacity*_LineOpacity + highlight)*_BoundaryBlend);
            }
            ENDHLSL
        }
    }
}
