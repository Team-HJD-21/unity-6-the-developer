// 카메라가 그린 화면을 받아 비 오는 날의 색감과 화면 속 빗줄기를 합성한다.
// StageCameraEffectsFeature가 WeatherShaderApply에서 받은 머티리얼로 렌더링한다.
Shader "Weather/Rain"
{
    // 이 값들은 RainScreen 머티리얼의 Inspector에 표시되며 실행 중에도 변경할 수 있다.
    Properties
    {
        // 비가 올 때 적용할 푸른 색감, 전체 효과 강도, 어두워지는 정도, 채도 감소량.
        _RainTint ("Rain Tint", Color) = (0.72, 0.82, 0.95, 1)
        _RainStrength ("Rain Strength", Range(0, 2)) = 2
        _Darkness ("Darkness", Range(0, 1)) = 0.12
        _Desaturation ("Desaturation", Range(0, 1)) = 0.2
        // 빗줄기의 색, 화면에 나타나는 양, 낙하 속도, 투명도, 기울기.
        _StreakColor ("Streak Color", Color) = (0.8, 0.9, 1, 1)
        _StreakDensity ("Streak Density", Range(0, 1)) = 0.35
        _StreakSpeed ("Streak Speed", Range(0, 2)) = 0.8
        _StreakOpacity ("Streak Opacity", Range(0, 1)) = 0.3
        _WindSlant ("Wind Slant", Range(-0.5, 0.5)) = 0.1
        _RippleOpacity ("Ripple Opacity", Range(0, 1)) = 0.35
        _RippleCount ("Ripple Count Per Cycle", Range(0, 100)) = 40
        _RippleTiltDegrees ("Ripple Rotation (Degrees)", Range(0, 80)) = 30
        _PatternWorldHeight ("Pattern World Height", Range(1, 100)) = 36
        // 화면 모서리를 추가로 어둡게 만들어 흐린 날의 분위기를 낸다.
        _EdgeDarkness ("Edge Darkness", Range(0, 0.5)) = 0.12
    }
    // 현재 렌더 파이프라인에서 사용할 GPU 그리기 설정과 프로그램을 묶는다.
    SubShader
    {
        // 화면 전체를 덮는 효과라 뒷면 제거, 깊이 기록, 깊이 검사를 사용하지 않는다.
        Cull Off ZWrite Off ZTest Always

        // 이 Pass 한 번으로 원본 화면을 읽고 색감·빗줄기·가장자리 음영을 합성한다.
        Pass
        {
            HLSLPROGRAM
            // 정점 단계는 vert, 픽셀 색 계산 단계는 frag 함수에서 시작한다.
            #pragma vertex Vert
            #pragma fragment frag

            // 좌표 변환 함수와 Unity의 _Time, _ScreenParams 같은 공통 값을 사용한다.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            half4 _RainTint;
            half4 _StreakColor;
            half _RainStrength;
            half _Darkness;
            half _Desaturation;
            half _StreakDensity;
            half _StreakSpeed;
            half _StreakOpacity;
            half _WindSlant;
            half _RippleOpacity;
            float _RippleCount;
            float _RippleTiltDegrees;
            float _PatternWorldHeight;
            half _EdgeDarkness;

            // 격자 칸의 좌표를 0~1 사이의 일정한 의사 난수로 바꾼다.
            // 127.1, 311.7, 43758.5453은 무늬가 반복돼 보이지 않게 하는 상수다.
            float HashCell(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }

            // 월드 격자 위의 한 점이 빗줄기에 놓였는지 계산한다.
            // grid는 기준 월드 높이 안의 가로·세로 격자 수다.
            // slant는 바람에 의한 기울기, seed는 두 빗줄기 층의 패턴 차이다.
            half RainStreak(float2 worldGridPoint, float2 grid, float speed,
                float slant, float seed)
            {
                // 월드 좌표로 격자를 나누고 시간에 따라 빗줄기를 아래로 흘린다.
                float2 position = float2(
                    (worldGridPoint.x + worldGridPoint.y * slant) * grid.x,
                    (worldGridPoint.y + _Time.y * speed) * grid.y);
                // floor는 현재 격자 칸, frac은 그 칸 안에서의 위치(0~1)를 구한다.
                float2 cell = floor(position);
                float2 local = frac(position);
                // 각 칸마다 빗줄기의 유무와 가로·세로 시작 위치를 다르게 정한다.
                float random = HashCell(cell + seed);
                float streakX = frac(random * 17.31);
                float streakY = frac(random * 43.27) * 0.5;
                float alongStreak = local.y - streakY;
                // 각 빗줄기의 길이를 기존 길이의 0.25~1배 사이에서 고정해 뽑는다.
                float lengthScale = lerp(0.25, 1.0, frac(random * 29.47));

                // 중심에서는 밝고 좌우로 멀어질수록 사라지는 가는 선을 만든다.
                float thinLine = 1.0 - smoothstep(0.02, 0.08, abs(local.x - streakX));
                // 빗줄기의 시작과 끝을 부드럽게 흐려 딱 잘린 막대처럼 보이지 않게 한다.
                float taperedEnds = smoothstep(0.0, 0.06 * lengthScale, alongStreak)
                    * (1.0 - smoothstep(0.28 * lengthScale,
                        0.48 * lengthScale, alongStreak));
                // 난수가 밀도 기준을 넘는 칸에만 빗줄기를 표시한다.
                return step(1.0 - _StreakDensity, random) * thinLine * taperedEnds;
            }

            // 물웅덩이를 위에서 비스듬히 보는 것처럼 원형 파문을 세로로 압축한다.
            // 각 격자는 서로 다른 시각에 한 번씩 파문을 만들며 위치도 랜덤하다.
            half RainRipple(float2 worldGridPoint, float aspect)
            {
                float2 grid = float2(9.0, 9.0);
                float2 position = worldGridPoint * grid;
                float2 cell = floor(position);
                float cycle = floor(_Time.y * 0.8);
                float random = HashCell(cell + cycle * 13.71);
                float phase = frac(_Time.y * 0.8);
                float start = HashCell(cell + cycle * 7.13 + 3.0) * 0.5;
                float progress = saturate((phase - start) * 2.0);
                float2 center = float2(0.3, 0.3)
                    + float2(HashCell(cell + cycle * 3.19 + 5.0),
                        HashCell(cell + cycle * 5.97 + 11.0)) * 0.4;
                float tilt = cos(radians(_RippleTiltDegrees));
                float2 offset = frac(position) - center;
                float distanceToCenter = length(float2(offset.x, offset.y / max(tilt, 0.1)));
                float radius = progress * 0.22;
                float ring = 1.0 - smoothstep(0.015, 0.04,
                    abs(distanceToCenter - radius));
                float spawnChance = saturate(_RippleCount / (grid.x * grid.y * aspect));
                return step(1.0 - spawnChance, random) * step(start, phase)
                    * ring * (1.0 - progress);
            }

            // 화면의 각 픽셀에 대해 최종 색을 계산한다.
            half4 frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                float2 effectUV = i.texcoord;
                #if UNITY_UV_STARTS_AT_TOP
                    effectUV.y = 1.0 - effectUV.y;
                #endif
                // 카메라가 그린 원래 색을 읽는다. saturate는 강도를 0~1로 제한한다.
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, i.texcoord);
                half strength = saturate(_RainStrength);
                // RGB로 밝기를 구하고 원래 색과 섞어 채도를 낮춘다.
                half luminance = dot(col.rgb, half3(0.2126, 0.7152, 0.0722));
                half3 muted = lerp(col.rgb, luminance.xxx, _Desaturation);
                // 푸른 색을 곱하고 어둡게 만든 뒤 원본과 효과 색을 강도만큼 섞는다.
                half3 rainy = muted * _RainTint.rgb * (1 - _Darkness);
                col.rgb = lerp(col.rgb, rainy, strength);

                // 화면 비율을 적용해 가로로 긴 화면에서도 빗줄기 폭이 과하게 늘지 않게 한다.
                float aspect = _ScreenParams.x / _ScreenParams.y;
                // 직교 카메라의 픽셀을 월드 XY에 대응시켜 격자를 지면에 고정한다.
                // 카메라가 움직여도 같은 월드 위치의 빗줄기·파문은 같은 격자에 남는다.
                float2 worldPosition = _WorldSpaceCameraPos.xy
                    + (effectUV - 0.5) * (2.0 * unity_OrthoParams.y)
                    * float2(aspect, 1.0);
                float2 worldGridPoint = worldPosition / max(_PatternWorldHeight, 0.001);
                // 가까운 빗줄기는 굵고 빠르게, 먼 빗줄기는 촘촘하고 약하게 그린다.
                half nearStreaks = RainStreak(worldGridPoint, float2(38, 10),
                    _StreakSpeed, _WindSlant, 0);
                half farStreaks = RainStreak(worldGridPoint, float2(75, 18),
                    _StreakSpeed * 0.7, _WindSlant * 0.6, 19);
                // 두 층을 합친 마스크로 빗줄기 색을 화면에 섞는다.
                half streaks = saturate(nearStreaks + farStreaks * 0.45);
                col.rgb = lerp(col.rgb, _StreakColor.rgb,
                    streaks * _StreakOpacity * strength);

                half ripple = RainRipple(worldGridPoint, aspect);
                col.rgb = lerp(col.rgb, _StreakColor.rgb,
                    ripple * _RippleOpacity * strength);

                // 화면 중앙에서는 0, 모서리 쪽에서는 1에 가까운 값을 만든다.
                // 이 값으로 모서리만 약하게 어둡게 한다.
                float2 fromCenter = effectUV * 2.0 - 1.0;
                half edge = saturate(dot(fromCenter, fromCenter) * 0.5);
                col.rgb *= 1.0 - edge * _EdgeDarkness * strength;
                // RGB만 바꿨으므로 원본 화면의 알파 값은 유지된다.
                return col;
            }
            ENDHLSL
        }
    }
}
