// 카메라 출력 픽셀을 실제 월드 XY로 복원합니다.
// Blit 입력 텍스처의 상하 반전과 날씨의 이동 방향을 분리합니다.
#ifndef TEAMHJD_WEATHER_COORDINATES_INCLUDED
#define TEAMHJD_WEATHER_COORDINATES_INCLUDED

float2 StageCameraWorldXY(float4 pixelPosition)
{
    float2 rasterUV = pixelPosition.xy / GetScaledScreenParams().xy;
    return ComputeWorldSpacePosition(rasterUV, UNITY_NEAR_CLIP_VALUE, UNITY_MATRIX_I_VP).xy;
}

float2 StageCameraViewportUV(float4 pixelPosition)
{
    if (unity_OrthoParams.w < 0.5)
        return GetNormalizedScreenSpaceUV(pixelPosition);
    float aspect = GetScaledScreenParams().x / GetScaledScreenParams().y;
    return (StageCameraWorldXY(pixelPosition) - _WorldSpaceCameraPos.xy)
        / (2.0 * unity_OrthoParams.y * float2(aspect, 1.0)) + 0.5;
}
#endif
