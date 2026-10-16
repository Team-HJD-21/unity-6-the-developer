using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Domain;
using System;
using TeamHJD.Game.Turrets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>프리팹의 상태 View와 터렛 조작 Presenter를 씬 데이터에 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class InitialStagePlayerHUD : MonoBehaviour, ILocalPlayerActorConsumer
{
    private const string StandaloneLocalPlayerId = "local-player";
    private const float SourceRetryInterval = .5f;
    private const float DisplayRefreshInterval = .1f;

    [Header("Prefab Views")]
    [SerializeField] private StagePlayerVitalsView vitalsView;
    [SerializeField] private StageTurretControlView turretView;
    [SerializeField] private StageControlUnitView controlUnitView;
    [Header("Turret Data")]
    [SerializeField] private TurretLevelUpgradeCatalog levelCatalog;
    [SerializeField, Min(0.1f)] private float interactionDistance = 10f;

    private Canvas _canvas;
    private StageTurretSelection _selection;
    private StageTurretPresenter _presenter;
    private TurretController _controller;
    private PlayerInfo _player;
    private PlayerControlState _playerControl;
    private float _nextSourceRetry;
    private float _nextDisplayRefresh;
    private bool _vitalsReady;
    private bool _turretControlsReady;
    private bool _hasLocalActor;
    private MatchId _boundMatchId;
    private PlayerId _boundPlayerId;
    private GameObject _boundActor;
    private bool _hasExplicitBinding;
    private bool _waitingForMatch;
    public MatchId BoundMatchId => _boundMatchId;
    public PlayerId BoundPlayerId => _boundPlayerId;

    public void PrepareLocalActor(MatchId matchId, PlayerId playerId)
    {
        if (matchId.IsEmpty || playerId.IsEmpty) throw new ArgumentException("A valid local Match/Player binding is required.");
        _boundMatchId = matchId; _boundPlayerId = playerId; _boundActor = null;
        _hasExplicitBinding = true; _waitingForMatch = false;
        _player = null; _playerControl = null; _hasLocalActor = false;
        _selection?.Clear(); _presenter?.Refresh(true);
        if (_vitalsReady) { vitalsView.Unbind(); vitalsView.ShowWaitingForSources(); }
    }

    public void BindLocalActor(MatchId matchId, PlayerId playerId, GameObject actor)
    {
        if (matchId.IsEmpty || playerId.IsEmpty) throw new ArgumentException("A valid local Match/Player binding is required.");
        if (actor == null || actor.scene != gameObject.scene || actor.GetComponent<PlayerInfo>() == null)
            throw new ArgumentException("The local actor must contain PlayerInfo in the HUD scene.", nameof(actor));
        var identity = actor.GetComponent<PlayerActorIdentity>();
        if (identity == null || identity.PlayerId != playerId) throw new ArgumentException("Actor identity does not match the local participant.", nameof(actor));
        if (_hasExplicitBinding && _boundMatchId == matchId && _boundPlayerId == playerId && _boundActor == actor) return;
        _boundMatchId = matchId; _boundPlayerId = playerId; _boundActor = actor;
        _hasExplicitBinding = true; _waitingForMatch = false;
        _player = null; _hasLocalActor = false;
        _selection?.Clear(); _presenter?.Refresh(true);
        if (isActiveAndEnabled && (_vitalsReady || _turretControlsReady)) BindSceneSources();
    }

    public void UnbindLocalActor(MatchId matchId)
    {
        if (!_hasExplicitBinding || matchId != _boundMatchId) return;
        _boundMatchId = default; _boundPlayerId = default; _boundActor = null;
        _hasExplicitBinding = false; _waitingForMatch = true;
        _player = null; _playerControl = null; _hasLocalActor = false; _controller = null;
        _selection?.Clear(); _presenter?.Refresh(true);
        if (_vitalsReady) { vitalsView.Unbind(); vitalsView.ShowWaitingForSources(); }
        if (controlUnitView != null) controlUnitView.Refresh(null, false);
    }

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
    }

    private void OnEnable()
    {
        _vitalsReady = TryInitializeVitals();
        _turretControlsReady = TryInitializeTurretControls();
        _canvas.enabled = _vitalsReady || _turretControlsReady;
        _nextSourceRetry = 0;
        _nextDisplayRefresh = 0;
        if (controlUnitView != null) controlUnitView.AutoActivationRequested += RequestAutoActivation;
    }

    // Scene의 체력 초기화가 끝난 뒤 source를 연결합니다. 정식 Match는 조립부가 로컬 actor를 주입합니다.
    private void Start()
    {
        if (_vitalsReady || _turretControlsReady) BindSceneSources();
    }

    private bool TryInitializeVitals()
    {
        if (vitalsView == null)
        {
            Debug.LogError("[InitialStagePlayerHUD] Vitals unavailable: missing vitalsView.", this);
            return false;
        }
        if (!vitalsView.IsConfigured)
        {
            Debug.LogError($"[InitialStagePlayerHUD] Vitals unavailable: missing {vitalsView.MissingReferences}.", vitalsView);
            return false;
        }
        vitalsView.ShowWaitingForSources();
        return true;
    }

    private bool TryInitializeTurretControls()
    {
        if (turretView != null) turretView.Hide();
        if (turretView == null || levelCatalog == null)
        {
            string missing = turretView == null ? nameof(turretView) : nameof(levelCatalog);
            Debug.LogError($"[InitialStagePlayerHUD] Turret controls unavailable: missing {missing}.", this);
            return false;
        }
        if (!turretView.IsConfigured)
        {
            Debug.LogError($"[InitialStagePlayerHUD] Turret controls unavailable: missing {turretView.MissingReferences}.", turretView);
            return false;
        }
        _selection = new StageTurretSelection(gameObject.scene, () => _player, () => interactionDistance);
        _presenter = new StageTurretPresenter(_selection, turretView, levelCatalog,
            CanInteract, () => _controller);
        return true;
    }

    private void OnValidate()
    {
        if (vitalsView == null) Debug.LogWarning("[InitialStagePlayerHUD] Missing vitalsView.", this);
        else if (!vitalsView.IsConfigured)
            Debug.LogWarning($"[InitialStagePlayerHUD] Missing {vitalsView.MissingReferences}.", vitalsView);
        if (turretView == null) Debug.LogWarning("[InitialStagePlayerHUD] Missing turretView.", this);
        else if (!turretView.IsConfigured)
            Debug.LogWarning($"[InitialStagePlayerHUD] Missing {turretView.MissingReferences}.", turretView);
        if (levelCatalog == null) Debug.LogWarning("[InitialStagePlayerHUD] Missing levelCatalog.", this);
    }

    private void Update()
    {
        if (!_vitalsReady && !_turretControlsReady) return;

        if (Time.unscaledTime >= _nextSourceRetry)
        {
            _nextSourceRetry = Time.unscaledTime + SourceRetryInterval;
            BindSceneSources();
        }

        if (_turretControlsReady)
        {
            if (CanInteract()) _selection.ProcessInput(_controller);
            else _selection.Clear();
        }

        bool updateDisplay = Time.unscaledTime >= _nextDisplayRefresh;
        if (updateDisplay)
        {
            _nextDisplayRefresh = Time.unscaledTime + DisplayRefreshInterval;
            if (_vitalsReady) vitalsView.RefreshPower();
            if (controlUnitView != null) controlUnitView.Refresh(_controller, CanInteractAtControlUnit());
        }

        _presenter?.Refresh(updateDisplay);
    }

    private bool CanInteract()
    {
        PlayerInfo player = _player;
        if (_waitingForMatch || !isActiveAndEnabled || Time.timeScale <= 0 ||
            player == null || !player.isActiveAndEnabled || player.curHp <= 0 ||
            _controller == null || !_controller.isActiveAndEnabled)
            return false;

        return _playerControl == null || _playerControl.CanAct;
    }

    private bool CanInteractAtControlUnit()
    {
        ControlUnitHealth cu = _vitalsReady ? vitalsView.ControlUnit : null;
        return CanInteract() && cu != null && cu.isActiveAndEnabled && cu.CurrentHealth > 0 &&
            Vector2.Distance(_player.transform.position, cu.transform.position) <= interactionDistance;
    }

    private void RequestAutoActivation()
    {
        if (!CanInteractAtControlUnit() || _controller == null) return;
        bool started = _controller.TryStartAutoActivation();
        controlUnitView.SetFeedback(started ? "Automatic activation started." : "Automatic activation is unavailable.");
        controlUnitView.Refresh(_controller, CanInteractAtControlUnit());
    }

    private void BindSceneSources()
    {
        if (_waitingForMatch) return;
        if (_controller == null || !_controller.isActiveAndEnabled)
            _controller = TurretController.FindForScene(gameObject.scene);
        if (_vitalsReady) vitalsView.BindPower(_controller);

        if (_player == null || !_player.isActiveAndEnabled || !_hasLocalActor)
        {
            PlayerInfo player = _player = FindLocalPlayer();
            _playerControl = player != null ? player.GetComponent<PlayerControlState>() : null;
            var identity = player != null ? player.GetComponent<PlayerActorIdentity>() : null;
            _hasLocalActor = identity != null && (_hasExplicitBinding ? identity.PlayerId == _boundPlayerId : identity.PlayerId.Value == StandaloneLocalPlayerId);
        }
        if (_vitalsReady) vitalsView.BindPlayer(_player);

        if (_vitalsReady && !vitalsView.HasControlUnit)
            vitalsView.BindControlUnit(FindControlUnit());
    }

    private PlayerInfo FindLocalPlayer()
    {
        if (_hasExplicitBinding)
        {
            if (_boundActor == null || !_boundActor.activeInHierarchy) return null;
            var identity = _boundActor.GetComponent<PlayerActorIdentity>();
            return identity != null && identity.PlayerId == _boundPlayerId ? _boundActor.GetComponent<PlayerInfo>() : null;
        }
        PlayerInfo fallback = _player != null && _player.isActiveAndEnabled &&
            _player.GetComponent<PlayerActorIdentity>() == null ? _player : null;
        foreach (PlayerInfo player in FindObjectsByType<PlayerInfo>(FindObjectsSortMode.None))
        {
            if (player.gameObject.scene != gameObject.scene || !player.isActiveAndEnabled) continue;

            var identity = player.GetComponent<PlayerActorIdentity>();
            if (identity != null && identity.PlayerId.Value == StandaloneLocalPlayerId) return player;
            if (identity == null && fallback == null) fallback = player;
        }

        return fallback;
    }

    private ControlUnitHealth FindControlUnit()
    {
        foreach (var controlUnit in FindObjectsByType<ControlUnitHealth>(FindObjectsSortMode.None))
            if (controlUnit.gameObject.scene == gameObject.scene && controlUnit.isActiveAndEnabled)
                return controlUnit;
        return null;
    }

    private void OnDisable()
    {
        if (controlUnitView != null) controlUnitView.AutoActivationRequested -= RequestAutoActivation;
        _presenter?.Dispose();
        _presenter = null;
        _selection?.Clear();
        if (turretView != null) turretView.Hide();
        if (vitalsView != null) vitalsView.Unbind();
        if (_canvas != null) _canvas.enabled = false;
        _hasLocalActor = false;
        _player = null;
        _playerControl = null;
        _selection = null;
        _vitalsReady = false;
        _turretControlsReady = false;
    }
}
