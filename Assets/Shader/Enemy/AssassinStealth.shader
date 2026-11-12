Shader "TheDeveloper/Enemy/AssassinStealth"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel Snap", Float) = 0
        _StealthAmount ("Stealth Amount", Range(0, 1)) = 0
        _StealthAlpha ("Stealth Alpha", Range(0, 1)) = 0.25
        _StealthTint ("Stealth Tint", Color) = (0.75, 0.85, 1, 1)
        _DistortionStrength ("Distortion Strength", Range(0, 0.02)) = 0.004
        _DistortionScale ("Distortion Scale", Range(1, 50)) = 18
        _DistortionRadiusPixels ("Distortion Radius (Pixels)", Range(0, 12)) = 4
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
        }

        GrabPass
        {
            "_AssassinBackground"
        }

        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex AssassinVert
            #pragma fragment AssassinStealthFrag
            #pragma target 2.0
            #pragma multi_compile_instancing
            #pragma multi_compile _ PIXELSNAP_ON
            #include "UnitySprites.cginc"

            sampler2D _AssassinBackground;
            float _DistortionStrength;
            float _DistortionScale;
            float _DistortionRadiusPixels;

            struct assassin_v2f
            {
                float4 vertex : SV_POSITION;
                fixed4 color : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 grabPosition : TEXCOORD1;
                float2 worldPosition : TEXCOORD2;
            };

            assassin_v2f AssassinVert(appdata_t IN)
            {
                v2f sprite = SpriteVert(IN);

                assassin_v2f OUT;
                OUT.vertex = sprite.vertex;
                OUT.color = sprite.color;
                OUT.texcoord = sprite.texcoord;
                OUT.grabPosition = ComputeGrabScreenPos(sprite.vertex);
                OUT.worldPosition = mul(
                    unity_ObjectToWorld,
                    UnityFlipSprite(IN.vertex, _Flip)).xy;

                return OUT;
            }

            fixed _StealthAmount;
            fixed _StealthAlpha;
            fixed4 _StealthTint;

            fixed4 AssassinStealthFrag(assassin_v2f IN) : SV_Target
            {
                fixed4 spriteColor =
                    SampleSpriteTexture(IN.texcoord) * IN.color;

                // 월드 위치에 고정된 패턴을 사용한다.
                float2 position = IN.worldPosition * _DistortionScale;

                // 넓은 굴곡과 세밀한 굴곡을 섞는다.
                float2 broadPattern = float2(
                    sin(position.y + sin(position.x * 0.7)),
                    cos(position.x + cos(position.y * 0.8)));
                float2 finePattern = float2(
                    sin(position.x * 2.3 + position.y * 1.7),
                    cos(position.y * 2.1 - position.x * 1.9));
                float2 pattern = broadPattern * 0.65 + finePattern * 0.35;

                float2 offsetPixels =
                    pattern * _DistortionStrength * _ScreenParams.xy;

                // 먼 배경을 끌어오지 않도록 샘플 이동 거리를 제한한다.
                float distancePixels = length(offsetPixels);
                offsetPixels *= min(
                    1.0,
                    _DistortionRadiusPixels / max(distancePixels, 0.0001));
                float2 offset = offsetPixels / _ScreenParams.xy;

                float4 grabPosition = IN.grabPosition;
                grabPosition.xy += offset * grabPosition.w;

                fixed4 backgroundColor =
                    tex2Dproj(
                        _AssassinBackground,
                        UNITY_PROJ_COORD(grabPosition));

                fixed amount = saturate(_StealthAmount);
                fixed4 color = lerp(spriteColor, backgroundColor, amount);

                // 스프라이트 모양의 알파 영역은 유지한다.
                color.a = spriteColor.a;

                // 현재 Blend 설정에 맞춰 premultiplied alpha로 반환한다.
                color.rgb *= color.a;

                return color;
            }
            ENDCG
        }
    }
}
