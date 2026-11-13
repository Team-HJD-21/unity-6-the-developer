Shader "TheDeveloper/Credits/SoftBillboard"
{
    Properties
    {
        [PerRendererData] _MainTex ("Particle",2D)="white" {}
        _Color ("Tint",Color)=(1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer tint",Color)=(1,1,1,1)
        [HideInInspector] _Flip ("Flip",Vector)=(1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnitySprites.cginc"
            fixed4 frag(v2f IN):SV_Target
            {
                float2 p=(IN.texcoord-.5)*2;
                float falloff=pow(saturate(1-dot(p,p)),2);
                return fixed4(IN.color.rgb,SampleSpriteTexture(IN.texcoord).a*IN.color.a*falloff);
            }
            ENDCG
        }
    }
}
