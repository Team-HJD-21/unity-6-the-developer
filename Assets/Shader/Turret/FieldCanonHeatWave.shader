Shader "FieldTurrets/CanonHeatWave"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        [HideInInspector] _HeatWaveTex ("Silhouette", 2D) = "white" {}
        [HideInInspector] _HeatWaveAlphaTex ("External Silhouette Alpha", 2D) = "white" {}
        [HideInInspector] _HeatWaveUseSplitAlpha ("Use External Silhouette Alpha", Float) = 0
        [HideInInspector] _HeatWaveUvRect ("Silhouette UV Bounds", Vector) = (0, 0, 1, 1)
        [HideInInspector] _HeatWaveUvScale ("Silhouette Scale", Vector) = (1, 1, 0, 0)
        [HideInInspector] _HeatWaveLocalized ("Localized Heat Wave", Float) = 0
        [HideInInspector] _HeatWaveGunTexture ("Gun Texture", 2D) = "white" {}
        _PartRippleStrength ("Hot Part Refraction Strength", Range(0, 1)) = 0.9
        _DistortionPixels ("Distortion (pixels)", Range(0, 12)) = 8
        [HideInInspector] _Color ("Tint", Color) = (1, 1, 1, 1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1, 1, 1, 1)
        [HideInInspector] _Flip ("Flip", Vector) = (1, 1, 1, 1)
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
            #include "UnitySprites.cginc"

            sampler2D _CameraSortingLayerTexture;
            sampler2D _HeatWaveTex;
            sampler2D _HeatWaveAlphaTex;
            float _HeatWaveUseSplitAlpha;
            float4 _HeatWaveUvRect;
            float4 _HeatWaveUvScale;
            float4 _HeatWaveTex_TexelSize;
            float _HeatWaveLocalized;
            float _PartRippleStrength;
            float _DistortionPixels;
            sampler2D _HeatWaveGunTexture;
            float4 _HeatWaveGunU, _HeatWaveGunV, _HeatWaveGunTransform, _HeatWaveGunBounds;
            fixed4 _HeatWaveGunTint;
            float _HeatWavePartOpacity;

            struct heat_v2f
            {
                float4 vertex : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                float2 gunPosition : TEXCOORD1;
                fixed4 color : COLOR;
            };

            heat_v2f HeatVert(appdata_t input)
            {
                v2f sprite = SpriteVert(input);
                heat_v2f output;
                output.vertex = sprite.vertex;
                output.texcoord = sprite.texcoord;
                output.gunPosition = input.vertex.xy * _HeatWaveGunTransform.xy + _HeatWaveGunTransform.zw;
                output.color = sprite.color;
                return output;
            }

            fixed SilhouetteAlpha(float2 uv)
            {
                return lerp(tex2D(_HeatWaveTex, uv).a, tex2D(_HeatWaveAlphaTex, uv).r, _HeatWaveUseSplitAlpha);
            }

            fixed4 SampleHotPart(float2 uv)
            {
                float2 minUv = _HeatWaveUvRect.xy, maxUv = _HeatWaveUvRect.zw;
                float2 inRect = step(minUv, uv) * step(uv, maxUv);
                float2 inset = _HeatWaveTex_TexelSize.xy * 0.5;
                fixed4 part = tex2D(_HeatWaveTex, clamp(uv, minUv + inset, maxUv - inset));
                part.a *= inRect.x * inRect.y;
                return part;
            }

            float HeatHalo(float2 uv)
            {
                float2 texel = _HeatWaveTex_TexelSize.xy;
                float coverage = SampleHotPart(uv).a;
                for (int y = -1; y <= 1; y++)
                for (int x = -1; x <= 1; x++)
                {
                    float2 direction = float2(x, y);
                    coverage = max(coverage, SampleHotPart(uv + direction * texel * 2.0).a * 0.84);
                    coverage = max(coverage, SampleHotPart(uv + direction * texel * 4.0).a * 0.42);
                }

                return coverage;
            }

            float2 WeatherHeatOffset(float2 uv, float localHaze)
            {
                // Use the distortion waveform and lengths from Weather/HeatWave and HeatWaveScreen.
                const float screenLength = 0.15;
                const float screenStrength = 0.347;
                const float lightLength = 0.442;
                const float lightStrength = 0.467;
                const float tau = 6.2831853;
                float yDirection = 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    uv.y = 1.0 - uv.y;
                    yDirection = -1.0;
                #endif
                float time = _Time.y;
                float globalWave = sin(uv.y * tau / screenLength + time * 3.4
                    + sin(uv.x * 17.0 + time * 1.2) * 0.7);
                float globalDetail = sin(uv.y * tau / (screenLength * 0.47) - time * 4.7 + uv.x * 9.0);
                float lightWave = sin(uv.y * tau / (lightLength * 0.22) + time * 7.0 + uv.x * 24.0);
                return float2(
                    (globalWave + globalDetail * 0.35) * screenStrength * 0.003
                        + lightWave * localHaze * lightStrength * 0.009,
                    (globalDetail * screenStrength * 0.0015
                        + globalWave * localHaze * lightStrength * 0.002) * yDirection);
            }

            fixed4 HeatFrag(heat_v2f input) : SV_Target
            {
                float2 minUv = _HeatWaveUvRect.xy, maxUv = _HeatWaveUvRect.zw;
                float2 centerUv = (minUv + maxUv) * 0.5;
                float2 gunUv = centerUv + (input.texcoord - centerUv) * _HeatWaveUvScale.xy;

                float2 screenUv = input.vertex.xy / _ScreenParams.xy;
                float heat = input.color.a;
                if (_HeatWaveLocalized > 0.5)
                {
                    float coverage = HeatHalo(gunUv);
                    float2 offset = WeatherHeatOffset(screenUv, coverage) * heat * _PartRippleStrength;
                    float2 pixelOffset = offset * _ScreenParams.xy;
                    float2 warpedPartUv = gunUv + ddx(gunUv) * pixelOffset.x + ddy(gunUv) * pixelOffset.y;
                    float2 gunPosition = input.gunPosition;
                    gunPosition += ddx(gunPosition) * pixelOffset.x + ddy(gunPosition) * pixelOffset.y;
                    clip(coverage - 0.01);

                    // The 2D sorting texture contains the map, so include the current gun and glow sprites in the sample.
                    float2 warpedGunUv = float2(dot(_HeatWaveGunU.xyz, float3(gunPosition, 1.0)),
                        dot(_HeatWaveGunV.xyz, float3(gunPosition, 1.0)));
                    fixed4 gun = tex2D(_HeatWaveGunTexture, warpedGunUv) * _HeatWaveGunTint;
                    float2 inGun = step(_HeatWaveGunBounds.xy, gunPosition) * step(gunPosition, _HeatWaveGunBounds.zw);
                    gun.a *= inGun.x * inGun.y;
                    fixed4 part = SampleHotPart(warpedPartUv);
                    fixed3 map = tex2D(_CameraSortingLayerTexture, saturate(screenUv + offset)).rgb;
                    fixed3 refracted = lerp(lerp(map, gun.rgb, gun.a), part.rgb, part.a * _HeatWavePartOpacity);
                    return fixed4(refracted, coverage * heat * _PartRippleStrength);
                }

                float2 inRect = step(minUv, gunUv) * step(gunUv, maxUv);
                float gunAlpha = SilhouetteAlpha(clamp(gunUv, minUv, maxUv)) * inRect.x * inRect.y;
                float outer = SilhouetteAlpha(input.texcoord) * (1.0 - gunAlpha);
                clip(outer - 0.01);
                float2 offset = WeatherHeatOffset(screenUv, 0.0) * (_DistortionPixels / 8.0) * heat;
                fixed3 refracted = tex2D(_CameraSortingLayerTexture, saturate(screenUv + offset)).rgb;
                return fixed4(refracted, outer * heat * 0.8);
            }
            ENDCG
        }
    }
}
