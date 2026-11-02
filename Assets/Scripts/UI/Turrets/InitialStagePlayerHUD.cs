// 로컬 actor와 View를 연결하고 정식 Match 입력은 Host 명령/확정 표시 상태를 사용합니다.
// 포트 대기·해제 상태를 로컬 D 변경이나 다른 참가자 actor 검색으로 우회하지 않습니다.
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;
using System;
using TeamHJD.Game.FieldTurrets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>프리팹의 상태 View와 터렛 조작 Presenter를 씬 데이터에 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
// PlayerSkillController(100)가 설치 상태를 정한 뒤 포탑 선택 클릭을 처리합니다.
[DefaultExecutionOrder(150)]
public sealed class InitialStagePlayerHUD : MonoBehaviour, ILocalPlayerActorConsumer, ITurretCommandConsumer
{
    private const string StandaloneLocalPlayerId = "local-player";
    private const float SourceRetryInterval = .5f;
    private const float DisplayRefreshInterval = .1f;

    [Header("Prefab Views")]
    [SerializeField] private StagePlayerVitalsView vitalsView;
    [SerializeField] private StageTurretControlView turretView;
    [SerializeField] private StageControlUnitView controlUnitView;
    [Header("Turret Data")]
    [SerializeField] private FieldTurretLevelUpgradeCatalog levelCatalog;
    [SerializeField, Min(0.1f), Tooltip("포탑·CU 조작에 필요한 XY 거리입니다. 포탑 선택 거리에는 영향을 주지 않습니다.")]
    private float interactionDistance = 10f;

    private Canvas _canvas;
    private StageTurretSelection _selection;
    private StageTurretPresenter _presenter;
    private FieldTurretController _controller;
    private PlayerInfo _player;
    private PlayerControlState _playerControl;
    private PlayerSkillController _playerSkills;
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
    private StageTurretCommandBinding _commands;
    private bool _requiresCommands;
    public MatchId BoundMatchId => _boundMatchId;
    public PlayerId BoundPlayerId => _boundPlayerId;
    public bool HasTurretCommandBinding => _commands != null;

    public void BindTurretCommands(MatchId matchId, PlayerId playerId, ITurretCommandRequester requester, ITurretPresentationSource presentation)
    {
        if (!_hasExplicitBinding || _boundMatchId != matchId || _boundPlayerId != playerId)
            throw new ArgumentException("Bind commands to the HUD's current local participant.");
        if (_commands != null)
        {
            if (_commands.Matches(matchId, playerId, requester, presentation)) return;
            throw new InvalidOperationException("Unbind the previous command ports before replacing them.");
        }
        var next = new StageTurretCommandBinding(matchId, playerId, requester, presentation);
        ClearCommands(); _commands = next; _requiresCommands = true;
        _commands.ResultReceived += OnCommandResult;
        if (_vitalsReady) vitalsView.UseAuthorityVitals();
        _presenter?.Refresh(true);
    }
    public void UnbindTurretCommands(MatchId matchId)
    {
        if (_commands == null || _commands.MatchId != matchId) return;
        ClearCommands(); _presenter?.Refresh(true);
        if (controlUnitView != null) controlUnitView.RefreshAuthority(null, null, false, false);
    }
    public bool RetryPendingTurretCommand(TeamHJD.Game.Domain.EntityId targetId) => _commands != null && _commands.TryRetry(targetId);
    /// <summary>튜토리얼/명시적 UI 입력도 클릭과 같은 Scene·거리 검증으로 대상을 선택합니다.</summary>
    public bool TrySelectTurret(TeamHJD.Game.Domain.EntityId targetId)
    {
        if (!CanInteract() || _selection == null || targetId.IsEmpty) return false;
        foreach (var turret in FieldTurretInstanceRegistry.GetAll())
        {
            if (!_controller.OwnsTurret(turret) || !turret.TryGetEntityId(out var id) || id != targetId) continue;
            bool selected = _selection.TrySelect(turret); _presenter?.Refresh(true); return selected;
        }
        return false;
    }
    private void ClearCommands()
    {
        StageTurretCommandBinding previous = _commands; _commands = null;
        if (previous == null) return;
        previous.ResultReceived -= OnCommandResult; previous.Dispose();
    }
    private void OnCommandResult(TurretCommandResult result)
    {
        if (!isActiveAndEnabled || result.MatchId != _boundMatchId) return;
        if (result.Kind == TurretCommandKind.StartAutoActivation)
        {
            if (controlUnitView != null) controlUnitView.SetFeedback(StageTurretUIText.Command(result));
        }
        else _presenter?.OnCommandResult(result);
    }

    public void PrepareLocalActor(MatchId matchId, PlayerId playerId)
    {
        if (matchId.IsEmpty || playerId.IsEmpty) throw new ArgumentException("A valid local Match/Player binding is required.");
        if (_commands != null && (_commands.MatchId != matchId || _commands.PlayerId != playerId)) ClearCommands();
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
        if (_commands != null && (_commands.MatchId != matchId || _commands.PlayerId != playerId)) ClearCommands();
        _boundMatchId = matchId; _boundPlayerId = playerId; _boundActor = actor;
        _hasExplicitBinding = true; _waitingForMatch = false;
        _player = null; _hasLocalActor = false;
        _selection?.Clear(); _presenter?.Refresh(true);
        if (isActiveAndEnabled && (_vitalsReady || _turretControlsReady)) BindSceneSources();
    }

    public void UnbindLocalActor(MatchId matchId)
    {
        if (!_hasExplicitBinding || matchId != _boundMatchId) return;
        UnbindTurretCommands(matchId);
        _boundMatchId = default; _boundPlayerId = default; _boundActor = null;
        _hasExplicitBinding = false; _waitingForMatch = true;
        _player = null; _playerControl = null; _hasLocalActor = false; _controller = null;
        _selection?.Clear(); _presenter?.Refresh(true);
        if (_vitalsReady) { vitalsView.Unbind(); vitalsView.ShowWaitingForSources(); }
        if (controlUnitView != null) controlUnitView.Refresh(null, false);
    }

    public bool IsTurretPanelOpen => isActiveAndEnabled && turretView != null && turretView.isActiveAndEnabled;

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
        if (_requiresCommands && _vitalsReady) vitalsView.UseAuthorityVitals();
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
            CanInteract, () => _controller, () => _commands, () => _requiresCommands);
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
            if (_requiresCommands)
            {
                var state = _commands?.Presentation.Snapshot?.Controller;
                if (_vitalsReady) vitalsView.RefreshAuthority(state);
                if (controlUnitView != null) controlUnitView.RefreshAuthority(state, _controller?.Regions?.Snapshot,
                    CanInteractAtControlUnit(), _commands != null && _commands.HasPending(default));
            }
            else
            {
                if (_vitalsReady) vitalsView.RefreshPower();
                if (controlUnitView != null) controlUnitView.Refresh(_controller, CanInteractAtControlUnit());
            }
        }

        _presenter?.Refresh(updateDisplay);
    }

    private bool CanInteract()
    {
        PlayerInfo player = _player;
        if (_waitingForMatch || !isActiveAndEnabled || Time.timeScale <= 0 ||
            player == null || !player.isActiveAndEnabled || player.curHp <= 0 ||
            _controller == null || !_controller.isActiveAndEnabled ||
            GameInput.PrimaryPointerReservedForPlacement ||
            (_playerSkills != null && (!_playerSkills.CanUse || _playerSkills.IsPlacingSkill)))
            return false;

        return _playerControl == null || _playerControl.CanAct;
    }

    private bool CanInteractAtControlUnit()
    {
        if (_requiresCommands)
        {
            var state = _commands?.Presentation.Snapshot?.Controller;
            if (!CanInteract() || state == null || !state.IsControlUnitAvailable || state.ControlUnitHealth <= 0) return false;
            Vector3 position = _player.transform.position;
            double dx = position.x - state.ControlUnitPosition.X, dy = position.y - state.ControlUnitPosition.Y;
            return dx * dx + dy * dy <= interactionDistance * interactionDistance;
        }
        ControlUnitHealth cu = _vitalsReady ? vitalsView.ControlUnit : null;
        return CanInteract() && cu != null && cu.isActiveAndEnabled && cu.CurrentHealth > 0 &&
            Vector2.Distance(_player.transform.position, cu.transform.position) <= interactionDistance;
    }

    private void RequestAutoActivation()
    {
        if (!CanInteractAtControlUnit() || _controller == null) return;
        if (_requiresCommands)
        {
            controlUnitView.SetFeedback("Waiting for Host confirmation/state...");
            if (_commands == null || !_commands.TrySubmit(TurretCommandKind.StartAutoActivation))
                controlUnitView.SetFeedback("Request unavailable or already pending.");
            controlUnitView.RefreshAuthority(_commands?.Presentation.Snapshot?.Controller, _controller.Regions?.Snapshot,
                CanInteractAtControlUnit(), _commands != null && _commands.HasPending(default));
            return;
        }
        bool started = _controller.TryStartAutoActivation();
        controlUnitView.SetFeedback(started ? "Automatic activation started." : "Automatic activation is unavailable.");
        controlUnitView.Refresh(_controller, CanInteractAtControlUnit());
    }

    private void BindSceneSources()
    {
        if (_waitingForMatch) return;
        if (_controller == null || !_controller.isActiveAndEnabled)
            _controller = FieldTurretController.FindForScene(gameObject.scene);
        if (_vitalsReady) { if (_requiresCommands) vitalsView.UseAuthorityVitals(); else vitalsView.BindPower(_controller); }

        if (_player == null || !_player.isActiveAndEnabled || !_hasLocalActor)
        {
            PlayerInfo player = _player = FindLocalPlayer();
            _playerControl = player != null ? player.GetComponent<PlayerControlState>() : null;
            _playerSkills = player != null ? player.GetComponent<PlayerSkillController>() : null;
            var identity = player != null ? player.GetComponent<PlayerActorIdentity>() : null;
            _hasLocalActor = identity != null && (_hasExplicitBinding ? identity.PlayerId == _boundPlayerId : identity.PlayerId.Value == StandaloneLocalPlayerId);
        }
        if (_vitalsReady) vitalsView.BindPlayer(_player);

        if (_vitalsReady && !_requiresCommands && !vitalsView.HasControlUnit)
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
        _playerSkills = null;
        _selection = null;
        _vitalsReady = false;
        _turretControlsReady = false;
    }
    private void OnDestroy() => ClearCommands();
}
