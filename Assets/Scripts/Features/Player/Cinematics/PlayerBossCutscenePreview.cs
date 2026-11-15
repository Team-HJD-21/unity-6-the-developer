using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Presentation;
using TeamHJD.Game.Presentation.Scene;
using TMPro;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>버튼으로 재생하는 로컬 보스 진입 연출 시안. Scene/Match 진행은 변경하지 않습니다.</summary>
[DefaultExecutionOrder(-400)]
public sealed class PlayerBossCutscenePreview : MonoBehaviour, ILocalInputConsumer
{
    [SerializeField] private Button _previewButton;
    [SerializeField] private GameObject _presentation;
    [SerializeField] private RawImage _distortion;
    [SerializeField] private Material _distortionTemplate;
    [SerializeField] private Image _blackout;
    [SerializeField] private CanvasGroup _captionGroup;
    [SerializeField] private TMP_Text _caption;
    [SerializeField] private RectTransform _topBar, _bottomBar;
    [SerializeField] private PlayableDirector _director;
    [SerializeField] private GameObject _cameraRig;
    [SerializeField] private CinemachineCamera _wideCamera, _closeCamera;
    [SerializeField, Range(.2f, 1)] private float _zoomRatio = .52f;
    [SerializeField, Range(.05f, 1)] private float _endingZoomRatio = .22f;
    [Header("Animated by BossEntrance Timeline")]
    [SerializeField, Range(0, 1)] private float _letterbox, _captionOpacity, _suction, _black, _captureFrame;
    [SerializeField, Range(0, 1)] private float _endingZoom;
    [SerializeField] private float _captionCharacters;
    private IInputBlocker _input;
    private IDisposable _inputLease;
    private CameraManager _manager;
    private MatchCameraFollow _follow;
    private CinemachineBrain _brain, _ownedBrain;
    private bool _followWasEnabled, _brainIgnoredTime;
    private CinemachineBrain.UpdateMethods _brainUpdate;
    private float _originalSize;
    private Vector3 _originalPosition;
    private Quaternion _originalRotation;
    private PlayerInfo _player;
    private Material _material;
    private RenderTexture _snapshot;
    private readonly List<Canvas> _hiddenCanvases = new();
    private float _elapsed;
    private bool _scripted, _completedScripted, _heldScriptedFrame;
    public bool IsPlaying { get; private set; }
    public float Elapsed => _elapsed;
    public float Duration => _director != null && _director.playableAsset != null ? (float)_director.duration : 0;
    public bool IsConfigured => _previewButton != null && _presentation != null && _distortion != null &&
        _distortionTemplate != null && _blackout != null && _caption != null && _captionGroup != null &&
        _topBar != null && _bottomBar != null && _director != null && _director.playableAsset is TimelineAsset &&
        _cameraRig != null && _wideCamera != null && _closeCamera != null;

    public void BindInputBlocker(IInputBlocker input) => _input = input ?? throw new ArgumentNullException(nameof(input));
    private void OnEnable()
    {
        if (_previewButton != null) _previewButton.onClick.AddListener(PreviewFromButton);
        CameraManager.OutputChanged += OnOutputChanged;
        if (_presentation != null) _presentation.SetActive(false);
        if (_cameraRig != null) _cameraRig.SetActive(false);
    }
    private void PreviewFromButton() { TryPlay(); }
    private void ResolveInput()
    {
        if (_input != null) return;
        var app = FindFirstObjectByType<AppRoot>();
        if (app != null && app.IsInitialized) _input = app.LocalInput;
    }
    private PlayerInfo AvailablePlayer(bool scripted = false)
    {
        var manager = CameraManager.Current;
        if (manager == null || manager.HasOverride || manager.OutputCamera == null) return null;
        var brain = manager.OutputCamera.GetComponent<CinemachineBrain>();
        if (brain != null && brain.enabled) return null;
        var follow = manager.GetComponent<MatchCameraFollow>();
        var player = follow != null && follow.Target != null ? follow.Target.GetComponent<PlayerInfo>() : null;
        var control = player != null ? player.GetComponent<PlayerControlState>() : null;
        return player != null && !player.IsDestroyed && control != null && (scripted || control.CanMove) &&
            !control.IsInSpaceship && player.gameObject.scene == gameObject.scene ? player : null;
    }
    public bool TryPlay()
        => TryPlay(false);
    /// <summary>진행 소유자의 기존 입력 잠금을 허용합니다. 마지막 화면은 Fade 인계 뒤 StopPreview/Reset에서 해제합니다.</summary>
    public async Task PlayScriptedAsync(CancellationToken cancellation)
    {
        cancellation.ThrowIfCancellationRequested();
        if (!TryPlay(true)) throw new InvalidOperationException("The authored boss entrance is unavailable.");
        try
        {
            while (IsPlaying) { cancellation.ThrowIfCancellationRequested(); await Task.Yield(); }
            cancellation.ThrowIfCancellationRequested();
            if (!_completedScripted) throw new OperationCanceledException("Boss entrance was interrupted.", cancellation);
        }
        catch { StopPreview(); throw; }
    }
    private bool TryPlay(bool scripted)
    {
        ResolveInput();
        var player = AvailablePlayer(scripted);
        if (!isActiveAndEnabled || !IsConfigured || IsPlaying || _heldScriptedFrame || player == null || _input == null || (!scripted && _input.IsBlocked)) return false;
        GameInput.TryConsumePrimaryPointerPress();
        try
        {
            _player = player; _manager = CameraManager.Current;
            _inputLease = _input.Acquire("PlayerBossCutscenePreview");
            _player.Damaged += OnPlayerDamaged;
            var camera = _manager.OutputCamera;
            _originalSize = camera.orthographicSize; _originalPosition = camera.transform.position; _originalRotation = camera.transform.rotation;
            _follow = _manager.GetComponent<MatchCameraFollow>(); _followWasEnabled = _follow.enabled; _follow.enabled = false;
            _brain = camera.GetComponent<CinemachineBrain>();
            if (_brain == null) _brain = _ownedBrain = camera.gameObject.AddComponent<CinemachineBrain>();
            _brainUpdate = _brain.UpdateMethod; _brainIgnoredTime = _brain.IgnoreTimeScale;
            _brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate; _brain.IgnoreTimeScale = true;
            _brain.enabled = true;
            _wideCamera.Lens.OrthographicSize = _originalSize;
            _closeCamera.Lens.OrthographicSize = Mathf.Max(1, _originalSize * _zoomRatio);
            _cameraRig.SetActive(true); PositionCameras();
            foreach (var track in ((TimelineAsset)_director.playableAsset).GetOutputTracks())
                if (track is CinemachineTrack) _director.SetGenericBinding(track, _brain);
            // 바인딩된 해당 플레이어 HUD만 숨깁니다. 다른 참가자/공용 메뉴의 상태는 소유하지 않습니다.
            foreach (var vitals in FindObjectsByType<StagePlayerVitalsView>(FindObjectsSortMode.None))
                if (vitals.Player == player)
                    foreach (var canvas in vitals.GetComponentsInChildren<Canvas>(true))
                        if (canvas.enabled) { _hiddenCanvases.Add(canvas); canvas.enabled = false; }
            _material = new Material(_distortionTemplate);
            _distortion.material = _material; _distortion.enabled = false;
            _scripted = scripted; _completedScripted = false; _elapsed = 0; IsPlaying = true;
            _presentation.SetActive(true); _previewButton.gameObject.SetActive(false);
            _director.timeUpdateMode = DirectorUpdateMode.Manual;
            _director.Play(); _director.time = 0; _director.Evaluate(); ApplyCameraZoom(); _brain.ManualUpdate();
            ApplyPresentation();
            return true;
        }
        catch (Exception error) { StopPreview(); Debug.LogException(error, this); return false; }
    }
    private static Vector3 FocusPoint(PlayerInfo player)
    {
        var sprite = player.GetComponent<SpriteRenderer>();
        return sprite != null ? sprite.bounds.center : player.transform.position;
    }
    private void Update()
    {
        if (!IsPlaying)
        {
            if (_heldScriptedFrame) return;
            ResolveInput();
            if (_previewButton != null)
            {
                _previewButton.gameObject.SetActive(AvailablePlayer() != null);
                _previewButton.interactable = IsConfigured && _input != null && !_input.IsBlocked;
            }
            return;
        }
        if ((!_scripted && UIEscapeInput.TryConsume()) || _player == null || _player.IsDestroyed ||
            _manager != CameraManager.Current || _manager.OutputCamera == null ||
            _player.GetComponent<PlayerControlState>().IsInSpaceship || _follow == null || _follow.Target != _player.transform || _manager.HasOverride)
        { StopPreview(); return; }
        _elapsed += Time.unscaledDeltaTime;
        try
        {
            PositionCameras();
            _director.time = Math.Min(_elapsed, Duration); _director.Evaluate();
            ApplyCameraZoom();
            _brain.ManualUpdate();
            if (_captureFrame > .5f && _snapshot == null) CaptureWorld();
            ApplyPresentation();
            if (_elapsed >= Duration) { _completedScripted = _scripted; StopPreview(_scripted); }
        }
        catch (Exception error) { StopPreview(); Debug.LogException(error, this); }
    }
    private void PositionCameras()
    {
        Vector3 focus = FocusPoint(_player); focus.z = _originalPosition.z;
        _wideCamera.transform.SetPositionAndRotation(focus, _originalRotation);
        _closeCamera.transform.SetPositionAndRotation(focus, _originalRotation);
    }
    private void ApplyCameraZoom()
    {
        float approach = Mathf.Max(1, _originalSize * _zoomRatio);
        float ending = Mathf.Min(approach, Mathf.Max(.5f, _originalSize * _endingZoomRatio));
        _closeCamera.Lens.OrthographicSize = Mathf.Lerp(approach, ending, _endingZoom);
    }
    private void CaptureWorld()
    {
        var camera = _manager.OutputCamera;
        int width = Mathf.Clamp(camera.pixelWidth, 64, 1920);
        int height = Mathf.Max(64, Mathf.RoundToInt(width / Mathf.Max(.1f, camera.aspect)));
        _snapshot = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32)
        { name = "Boss preview frame", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        _snapshot.Create();
        var request = new UniversalRenderPipeline.SingleCameraRequest { destination = _snapshot };
        if (RenderPipeline.SupportsRenderRequest(camera, request)) RenderPipeline.SubmitRenderRequest(camera, request);
        else if (GraphicsSettings.currentRenderPipeline == null)
        {
            var previous = camera.targetTexture;
            try { camera.targetTexture = _snapshot; camera.Render(); }
            finally { camera.targetTexture = previous; }
        }
        else throw new InvalidOperationException("Current renderer does not support the boss preview capture.");
        _distortion.texture = _snapshot; _distortion.enabled = true;
    }
    private void ApplyPresentation()
    {
        _topBar.sizeDelta = new Vector2(0, 105 * _letterbox); _bottomBar.sizeDelta = new Vector2(0, 105 * _letterbox);
        _captionGroup.alpha = _captionOpacity;
        _caption.maxVisibleCharacters = Mathf.FloorToInt(_captionCharacters);
        if (_material != null)
        { _material.SetFloat("_Amount", _suction); _material.SetFloat("_Clock", _elapsed); _material.SetFloat("_Aspect", (float)Screen.width / Mathf.Max(1, Screen.height)); }
        _blackout.color = new Color(0, 0, 0, _black);
        _distortion.enabled = _snapshot != null && _captureFrame > .5f;
    }
    public void StopPreview()
        => StopPreview(false);
    private void StopPreview(bool preserveFinalFrame)
    {
        bool wasScripted = _scripted || _heldScriptedFrame;
        IsPlaying = false;
        _heldScriptedFrame = preserveFinalFrame;
        if (_player != null) _player.Damaged -= OnPlayerDamaged;
        if (_director != null) _director.Stop();
        if (_cameraRig != null) _cameraRig.SetActive(false);
        if (_brain != null)
        {
            _brain.enabled = false;
            _brain.UpdateMethod = _brainUpdate; _brain.IgnoreTimeScale = _brainIgnoredTime;
            _brain = null;
        }
        if (_manager != null && _manager.OutputCamera != null)
        {
            _manager.OutputCamera.orthographicSize = _originalSize;
            _manager.OutputCamera.transform.SetPositionAndRotation(_originalPosition, _originalRotation);
        }
        if (_follow != null) { _follow.enabled = _followWasEnabled; if (_followWasEnabled) _follow.SnapToTarget(); _follow = null; }
        _inputLease?.Dispose(); _inputLease = null;
        foreach (var canvas in _hiddenCanvases) if (canvas != null) canvas.enabled = true;
        _hiddenCanvases.Clear();
        if (_presentation != null) _presentation.SetActive(preserveFinalFrame);
        if (!preserveFinalFrame)
        {
            if (_distortion != null) { _distortion.texture = null; _distortion.material = _distortionTemplate; }
            if (_snapshot != null) { _snapshot.Release(); Destroy(_snapshot); _snapshot = null; }
            if (_material != null) { Destroy(_material); _material = null; }
        }
        _player = null; _manager = null;
        _scripted = false;
        if (_previewButton != null) _previewButton.gameObject.SetActive(!preserveFinalFrame && !wasScripted);
    }
    private void OnPlayerDamaged(int damage) => StopPreview();
    private void OnOutputChanged(Camera camera) { if (IsPlaying && (_manager == null || _manager.OutputCamera != camera)) StopPreview(); }
    private void OnDisable()
    {
        if (_previewButton != null) _previewButton.onClick.RemoveListener(PreviewFromButton);
        CameraManager.OutputChanged -= OnOutputChanged; StopPreview();
    }
    private void OnDestroy() { if (_ownedBrain != null) Destroy(_ownedBrain); }
}
