using TeamHJD.Game.Presentation.Scene;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>HUD에 바인딩된 로컬 Player의 실제 피격만 화면 연출로 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class PlayerHitFeedback : MonoBehaviour
{
    [SerializeField] private StagePlayerVitalsView _vitals;
    [SerializeField] private CanvasGroup _vignette;
    [SerializeField, Min(.01f)] private float _flashSeconds = .45f;
    [SerializeField, Min(.01f)] private float _shakeSeconds = .2f;
    [SerializeField, Range(0f, 1f)] private float _maximumAlpha = .65f;
    [SerializeField, Min(0f)] private float _shakeDistance = .14f;
    private PlayerInfo _player;
    private Camera _camera, _renderingCamera;
    private Vector3 _originalCameraPosition;
    private Vector3 _shakeOffset;
    private float _elapsed, _strength;

    private void OnEnable()
    {
        CameraManager.OutputChanged += OnCameraChanged;
        Camera.onPreCull += BeforeBuiltinCamera;
        Camera.onPostRender += AfterBuiltinCamera;
        RenderPipelineManager.beginCameraRendering += BeforePipelineCamera;
        RenderPipelineManager.endCameraRendering += AfterPipelineCamera;
        RefreshBinding();
        ResolveCamera();
        Clear();
    }

    private void Update()
    {
        RefreshBinding();
        ResolveCamera();
        Advance(Time.unscaledDeltaTime);
    }

    private void RefreshBinding()
    {
        PlayerInfo player = _vitals != null && _vitals.isActiveAndEnabled ? _vitals.Player : null;
        if (ReferenceEquals(_player, player)) return;
        if (_player != null) _player.Damaged -= OnDamaged;
        Clear();
        _player = player;
        if (_player != null) _player.Damaged += OnDamaged;
    }

    private void OnDamaged(int amount)
    {
        if (!isActiveAndEnabled || amount <= 0 || _player == null || !_player.isActiveAndEnabled ||
            _vitals == null || _vitals.Player != _player || _vignette == null) return;
        float fraction = (float)amount / Mathf.Max(1, _player.MaxHealth);
        _strength = Mathf.Max(_strength, Mathf.Clamp01(.4f + fraction * 2f));
        _elapsed = 0;
        Advance(0);
    }

    private void Advance(float delta)
    {
        if (_strength <= 0) return;
        _elapsed += Mathf.Max(0, delta);
        float fade = Mathf.Clamp01(1f - _elapsed / Mathf.Max(.01f, _flashSeconds));
        if (_vignette != null) _vignette.alpha = _maximumAlpha * _strength * fade * fade;
        float shake = Mathf.Clamp01(1f - _elapsed / Mathf.Max(.01f, _shakeSeconds));
        // Fixed damped oscillation: no gameplay or presentation random stream is consumed.
        _shakeOffset = new Vector3(Mathf.Cos(_elapsed * 95f), Mathf.Sin(_elapsed * 127f), 0) *
            (_shakeDistance * _strength * shake * shake);
        if (fade <= 0 && shake <= 0) Clear();
    }

    private void ResolveCamera()
    {
        if (_camera != null && _camera.isActiveAndEnabled) return;
        OnCameraChanged(CameraManager.Current != null ? CameraManager.Current.OutputCamera : Camera.main);
    }

    private void OnCameraChanged(Camera camera)
    {
        RestoreCamera();
        _camera = camera;
    }

    // Apply only during rendering, then restore before input/follow/cutscene code runs.
    private void BeforeBuiltinCamera(Camera camera)
    { if (GraphicsSettings.currentRenderPipeline == null) BeginCamera(camera); }
    private void AfterBuiltinCamera(Camera camera)
    { if (GraphicsSettings.currentRenderPipeline == null) EndCamera(camera); }
    private void BeforePipelineCamera(ScriptableRenderContext context, Camera camera) => BeginCamera(camera);
    private void AfterPipelineCamera(ScriptableRenderContext context, Camera camera) => EndCamera(camera);

    private void BeginCamera(Camera camera)
    {
        if (!isActiveAndEnabled || _strength <= 0 || camera == null || camera != _camera ||
            _renderingCamera != null || _shakeOffset.sqrMagnitude <= 0) return;
        _renderingCamera = camera;
        _originalCameraPosition = camera.transform.position;
        camera.transform.position += _shakeOffset;
    }

    private void EndCamera(Camera camera)
    { if (_renderingCamera == camera) RestoreCamera(); }

    private void RestoreCamera()
    {
        if (_renderingCamera != null) _renderingCamera.transform.position = _originalCameraPosition;
        _renderingCamera = null;
    }

    private void Clear()
    {
        RestoreCamera();
        _elapsed = _strength = 0;
        _shakeOffset = Vector3.zero;
        if (_vignette != null) _vignette.alpha = 0;
    }

    private void OnDisable()
    {
        CameraManager.OutputChanged -= OnCameraChanged;
        Camera.onPreCull -= BeforeBuiltinCamera;
        Camera.onPostRender -= AfterBuiltinCamera;
        RenderPipelineManager.beginCameraRendering -= BeforePipelineCamera;
        RenderPipelineManager.endCameraRendering -= AfterPipelineCamera;
        if (_player != null) _player.Damaged -= OnDamaged;
        _player = null; _camera = null;
        Clear();
    }
}
