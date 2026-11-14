using UnityEngine;

public sealed partial class TacticalCommandController
{
    private Vector2 _cameraFrom, _cameraGoal;
    private float _cameraMoveAt, _zoomFrom, _zoomGoal, _zoomAt;
    private bool _axisPanning;

    /// <summary>Scene에 명시 연결한 marker만 읽습니다. 지역 점유/명령 권한의 geometry는 바꾸지 않습니다.</summary>
    public bool TryGetAuthoredCameraBounds(out Rect bounds)
    {
        bounds = default;
        return CameraLeftTop != null && CameraRightBottom != null &&
            CameraLeftTop.gameObject.scene == gameObject.scene && CameraRightBottom.gameObject.scene == gameObject.scene &&
            TacticalMapMath.TryBounds(CameraLeftTop.position, CameraRightBottom.position, out bounds);
    }

    private void ResetCameraMotion()
    {
        _cameraFrom = _cameraGoal = CameraPosition;
        _zoomFrom = _zoomGoal = _size;
        _cameraMoveAt = _zoomAt = Time.unscaledTime - Mathf.Max(1, Style.CameraMoveSeconds, Style.CameraZoomSeconds);
        _axisPanning = false;
    }

    private Vector2 ClampCameraGoal(Vector2 position)
    {
        var frame = TacticalMapMath.FrameCamera(WorldBounds, _zoomGoal, FullAspect, _defaultViewport);
        return TacticalMapMath.ClampViewCenter(position, WorldBounds, frame.Size, frame.Aspect);
    }

    private void BeginCameraPan()
    {
        _cameraFrom = _cameraGoal = CameraPosition;
        _cameraMoveAt = Time.unscaledTime;
    }

    private void MoveCameraTo(Vector2 position)
    {
        _cameraFrom = CameraPosition; _cameraGoal = ClampCameraGoal(position);
        _cameraMoveAt = Time.unscaledTime; _axisPanning = false;
    }
}
