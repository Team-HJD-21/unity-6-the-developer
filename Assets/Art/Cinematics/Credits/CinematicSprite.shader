Shader "TheDeveloper/Credits/Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer tint", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        _Shine ("Visor reflection sweep", Range(0,1)) = 0
        _ShineStrength ("Visor reflection strength", Range(0,1)) = 0
        _WeldLight ("Reflected welding light", Range(0,1)) = 0
        _SpriteRect ("Sprite UV rectangle", Vector) = (0,0,1,1)
        _VisorRegion ("Visor center and radius", Vector) = (.5,.64,.29,.13)
        _ExhaustMotion ("Engine distortion", Range(0,1)) = 0
        _ExhaustPhase ("Engine phase", Float) = 0
        _ContactGlow ("Local welding reflection", Range(0,1)) = 0
        _ContactRegion ("Contact center and radius", Vector) = (.5,1,.035,.28)
        _UvClamp ("Atlas sampling bounds", Vector) = (0,0,1,1)
    }
    SubShader
    {
        Tags {"Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "CanUseSpriteAtlas"="True"}
        Cull Off Lighting Off ZWrite Off Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnitySprites.cginc"
            float _Shine, _ShineStrength, _WeldLight, _ExhaustMotion, _ExhaustPhase, _ContactGlow;
            float4 _UvClamp, _SpriteRect, _VisorRegion, _ContactRegion;
            fixed4 frag(v2f IN) : SV_Target
            {
                float2 sampleUV=IN.texcoord;
                sampleUV.y+=sin(sampleUV.x*51+_ExhaustPhase)*.004*_ExhaustMotion;
                fixed4 c = SampleSpriteTexture(clamp(sampleUV,_UvClamp.xy,_UvClamp.zw)) * IN.color;
                clip(c.a - .035);
                float2 uv=(IN.texcoord-_SpriteRect.xy)/_SpriteRect.zw;
                float2 visorUV=(uv-_VisorRegion.xy)/_VisorRegion.zw;
                float visor=saturate(1-dot(visorUV,visorUV));
                float diagonal=uv.x + uv.y*.5;
                float stripe=1-smoothstep(.008,.045,abs(diagonal-lerp(.55,1.05,_Shine)));
                c.rgb=lerp(c.rgb,float3(.91,.96,1),stripe*visor*_ShineStrength);
                float reflected=saturate(1-abs(uv.x-.24)*2.3)*saturate((uv.y-.26)*2.5);
                c.rgb+=float3(.14,.23,.3)*_WeldLight*(reflected*.35+visor*.65);
                float2 contact=(uv-_ContactRegion.xy)/_ContactRegion.zw;
                c.rgb+=float3(.16,.32,.5)*exp(-dot(contact,contact)*2)*_ContactGlow;
                c.rgb *= c.a; return c;
            }
            ENDCG
        }
    }
}
