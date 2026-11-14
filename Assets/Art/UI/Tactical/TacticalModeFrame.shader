Shader "The Developer/UI/TacticalModeFrame"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Frame tone", Color) = (.63,.75,.71,1)
        _Progress ("Mode amount", Range(0,1)) = 0
        _Cover ("Full screen membrane", Range(0,1)) = 0
        _Reveal ("Center wave", Range(0,1)) = 0
        _Resolution ("Screen pixels", Vector) = (1920,1080,0,0)
        _CellSize ("Hex spacing in pixels", Float) = 28
        _EdgeWidth ("Edge fade in pixels", Float) = 64
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex:POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex:SV_POSITION; fixed4 color:COLOR; float2 uv:TEXCOORD0; float4 world:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            fixed4 _Color; float4 _ClipRect,_Resolution; float _Progress,_Cover,_Reveal,_CellSize,_EdgeWidth;
            v2f vert(appdata v)
            {
                v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world=v.vertex;o.vertex=UnityObjectToClipPos(v.vertex);o.uv=v.uv;o.color=v.color*_Color;return o;
            }
            fixed4 frag(v2f i):SV_Target
            {
                float2 pixels=i.uv*_Resolution.xy;
                float2 opposite=(1-i.uv)*_Resolution.xy;
                float edge=min(min(pixels.x,pixels.y),min(opposite.x,opposite.y));
                float2 radial=(pixels-_Resolution.xy*.5)/max(1,length(_Resolution.xy*.5));
                float radius=length(radial),front=_Reveal*1.08;
                float band=(1-smoothstep(.008,.045,abs(radius-front)))*step(.001,_Reveal)*step(_Reveal,.99);
                // 파동 경계의 패턴만 굴절시키고 UI/장면 텍스처는 왜곡하지 않습니다.
                float2 wave=normalize(radial+float2(.0001,.0001))*sin((radius-front)*130)*band*3.2;
                float2 p=(pixels+wave)/max(12,_CellSize),repeat=float2(1,1.7320508);
                float2 a=(frac(p/repeat)-.5)*repeat;
                float2 b=(frac((p-repeat*.5)/repeat)-.5)*repeat;
                float2 cell=dot(a,a)<dot(b,b)?a:b;
                float2 q=abs(cell);float d=max(q.x,dot(q,float2(.5,.8660254)));
                float aa=max(.008,fwidth(d));
                float hex=1-smoothstep(aa*.4,aa*1.4,abs(d-.5));
                float rim=1-smoothstep(12,max(24,_EdgeWidth),edge);
                float frame=1-smoothstep(.5,1.5,abs(edge-9));
                float cleared=(1-smoothstep(front-.018,front+.018,radius))*smoothstep(0,.015,_Reveal);
                float membrane=_Cover*(1-cleared);
                float residue=_Progress*(rim*(.03+hex*.20)+frame*.30);
                float fringe=band*_Cover*.32;
                fixed4 c;
                c.rgb=i.color.rgb;
                // 색 분리는 얇은 파동 가장자리에서만 사용합니다.
                c.rgb+=fringe*float3(sin(radius*210)*.10,.03,cos(radius*210)*.12);
                // 셀 내부는 투명하게 두고 육각선만 올려 실제 게임 배경을 유지합니다.
                c.a=saturate(membrane*hex*.58+residue+fringe)*i.color.a;
                #ifdef UNITY_UI_CLIP_RECT
                    c.a*=UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                    clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
