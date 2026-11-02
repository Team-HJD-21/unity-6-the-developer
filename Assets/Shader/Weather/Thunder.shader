// Rain과 같은 차가운 색감 위에 월드 좌표에 고정된 노란 번개를 그린다.
// 번개는 1~3초 간격으로 발생하며, 한 번에 1~3개를 각기 다른 위치에 그린다.
Shader "Weather/Thunder"
{
    Properties
    {
        [NoScaleOffset] _MainTex ("Screen", 2D) = "white" {}
        _RainTint ("Rain Tint", Color) = (0.72, 0.82, 0.95, 1)
        _RainStrength ("Rain Strength", Range(0, 1)) = 1
        _Darkness ("Darkness", Range(0, 1)) = 0.12
        _Desaturation ("Desaturation", Range(0, 1)) = 0.2
        _EdgeDarkness ("Edge Darkness", Range(0, 0.5)) = 0.12

        _LightningColor ("Lightning Color", Color) = (1, 0.82, 0.12, 1)
        // 현재 Player 프리팹의 스프라이트 높이 5 × 루트 배율 0.8 = 4 월드 단위.
        _PlayerWorldHeight ("Player World Height", Float) = 4
        _FlashDuration ("Flash Duration", Range(0.08, 0.5)) = 0.22
        _LightningSize ("Lightning Size", Range(0.25, 3)) = 1
        [HideInInspector] _SpawnAboveViewport ("Spawn Above Viewport", Float) = 1
    }

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert(appdata input)
            {
                v2f output;
                output.vertex = UnityObjectToClipPos(input.vertex);
                output.uv = input.uv;
                return output;
            }

            sampler2D _MainTex;
            half4 _RainTint;
            half4 _LightningColor;
            half _RainStrength;
            half _Darkness;
            half _Desaturation;
            half _EdgeDarkness;
            float _PlayerWorldHeight;
            float _FlashDuration;
            float _LightningSize;
            float4 _CurrentStrikeRoots[3];

            float HashNumber(float number)
            {
                return frac(sin(number * 127.1) * 43758.5453);
            }

            float SegmentDistance(float2 pixelPosition, float2 segmentStart, float2 segmentEnd)
            {
                float2 segment = segmentEnd - segmentStart;
                float part = saturate(dot(pixelPosition - segmentStart, segment)
                    / max(dot(segment, segment), 0.000001));
                return length(pixelPosition - (segmentStart + part * segment));
            }

            float2 BoltPoint(float2 root, float height, float seed, int segment)
            {
                if (segment == 0)
                    return root;
                if (segment == 9)
                    return root + float2((HashNumber(seed + 97.0) - 0.5) * height * 0.22,
                        -height);

                // 양쪽 흔들림과 각 선분의 높이를 따로 흔들어 규칙적인 지그재그를 피한다.
                float bend = (HashNumber(seed * 13.17 + segment * 5.63) - 0.5)
                    * height * lerp(0.12, 0.32, HashNumber(seed + segment * 3.41));
                float verticalJitter = (HashNumber(seed * 7.91 + segment * 11.3) - 0.5)
                    * height * 0.055;
                return root + float2(bend, -height * segment / 9.0 + verticalJitter);
            }

            float BoltDistance(float2 pixelPosition, float2 root, float height, float eventIndex)
            {
                float nearest = 10.0;
                // 길이와 방향이 다른 아홉 선분으로 번개 본체를 만든다.
                [unroll] for (int segment = 0; segment < 9; segment++)
                {
                    float2 segmentStart = BoltPoint(root, height, eventIndex, segment);
                    float2 segmentEnd = BoltPoint(root, height, eventIndex, segment + 1);
                    nearest = min(nearest, SegmentDistance(pixelPosition, segmentStart, segmentEnd));
                }

                // 사건마다 가지 수·시작점·방향·길이를 달리한다.
                int branchCount = 1 + (int)floor(HashNumber(eventIndex + 19.31) * 3.0);
                [unroll] for (int branch = 0; branch < 3; branch++)
                {
                    if (branch < branchCount)
                    {
                        float branchSeed = eventIndex * 17.13 + branch * 31.7;
                        int startSegment = 2 + (int)floor(HashNumber(branchSeed + 4.0) * 5.0);
                        float2 branchStart = BoltPoint(root, height, eventIndex, startSegment);
                        float direction = HashNumber(branchSeed + 8.0) < 0.5 ? -1.0 : 1.0;
                        float2 branchEnd = branchStart + float2(direction * height
                            * lerp(0.12, 0.28, HashNumber(branchSeed + 11.0)),
                            -height * lerp(0.10, 0.23, HashNumber(branchSeed + 15.0)));
                        nearest = min(nearest, SegmentDistance(pixelPosition, branchStart, branchEnd));
                    }
                }
                return nearest;
            }

            half4 frag(v2f input) : SV_Target
            {
                half4 color = tex2D(_MainTex, input.uv);
                half strength = saturate(_RainStrength);

                // Rain.shader와 같은 차가운 색감과 채도 감소만 적용한다.
                half luminance = dot(color.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 muted = lerp(color.rgb, luminance.xxx, _Desaturation);
                half3 rainy = muted * _RainTint.rgb * (1.0 - _Darkness);
                color.rgb = lerp(color.rgb, rainy, strength);

                float aspect = _ScreenParams.x / _ScreenParams.y;
                float2 fromCenter = input.uv * 2.0 - 1.0;
                half edge = saturate(dot(fromCenter, fromCenter) * 0.5);
                color.rgb *= 1.0 - edge * _EdgeDarkness * strength;

                // t[n] = 2n + 난수[n]. 연속 발생 간격은
                // t[n+1] - t[n] = 2 + 난수[n+1] - 난수[n]이므로 1~3초다.
                float now = _Time.y;
                float eventIndex = floor(now * 0.5);
                float strikeTime = eventIndex * 2.0 + HashNumber(eventIndex + 41.0);
                if (now < strikeTime)
                {
                    eventIndex -= 1.0;
                    strikeTime = eventIndex * 2.0 + HashNumber(eventIndex + 41.0);
                }

                float age = now - strikeTime;
                half fade = 1.0 - smoothstep(0.0, _FlashDuration, age);
                half flicker = 0.75 + 0.25 * sin(age * 110.0);
                half flash = fade * flicker * strength;
                if (flash <= 0.0)
                    return color;

                // 플레이어 높이를 화면 비율로 바꾸고 번개 크기 배율을 적용한다.
                float playerScreenHeight = _PlayerWorldHeight
                    / max(2.0 * unity_OrthoParams.y, 0.0001);
                float2 screenPoint = float2(input.uv.x * aspect, input.uv.y);

                // ShaderTests가 번개 발생 시 월드 좌표로 정한 위치를 사용한다.
                int boltCount = 1 + (int)floor(HashNumber(eventIndex * 23.17
                    + 37.0) * 3.0);
                [unroll] for (int boltIndex = 0; boltIndex < 3; boltIndex++)
                {
                    if (boltIndex < boltCount)
                    {
                        float boltSeed = eventIndex * 31.7 + boltIndex * 53.1;
                        float boltHeight = clamp(playerScreenHeight
                            * lerp(2.0, 2.5, HashNumber(boltSeed * 3.73 + 5.0))
                            * _LightningSize, 0.005, 2.4);
                        float2 worldRoot = _CurrentStrikeRoots[boltIndex].xy;
                        float2 root = (worldRoot - _WorldSpaceCameraPos.xy)
                            / (2.0 * unity_OrthoParams.y);
                        root += float2(aspect * 0.5, 0.5);

                        float distanceToBolt = BoltDistance(screenPoint, root,
                            boltHeight, boltSeed);

                        // 각 번개에 좁은 본체와 넓은 노란 후광을 더한다.
                        float coreRadius = boltHeight * 0.007;
                        float glowRadius = boltHeight * 0.07;
                        half core = 1.0 - smoothstep(coreRadius * 0.35,
                            coreRadius, distanceToBolt);
                        half glow = 1.0 - smoothstep(coreRadius, glowRadius,
                            distanceToBolt);
                        // 화면 밖의 시작점부터 아래로 번개를 펼쳐 화면 안으로 들어오게 한다.
                        float reveal = saturate(age / max(_FlashDuration * 0.35, 0.0001));
                        half revealed = step(root.y - boltHeight * reveal,
                            screenPoint.y);
                        color.rgb += _LightningColor.rgb * flash
                            * (core * 1.5 + glow * 0.4) * revealed;
                    }
                }

                color.rgb += _LightningColor.rgb * flash * 0.04;
                return color;
            }
            ENDCG
        }
    }
}
