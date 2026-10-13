using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Turrets;
using UnityEngine;
using UnityEngine.UI;

/// <summary>프리팹의 상태 View와 터렛 조작 Presenter를 씬 데이터에 연결합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class InitialStagePlayerUi : MonoBehaviour
{
    private const string LocalPlayerId = "local-player";
    private const float SourceRetryInterval = .5f;
    private const float DisplayRefreshInterval = .1f;

    [Header("Prefab Views")]
    [SerializeField] private StagePlayerVitalsView vitalsView;
    [SerializeField] private StageTurretControlView turretView;
    [Header("Turret Data")]
    [SerializeField] private TurretLevelUpgradeCatalog levelCatalog;

    private Canvas _canvas;
    private StageTurretSelection _selection;
    private StageTurretPresenter _presenter;
    private TurretController _controller;
    private PlayerControlState _playerControl;
    private float _nextSourceRetry;
    private float _nextDisplayRefresh;
    private bool _initialized;

    private void Awake()
    {
        _canvas = GetComponent<Canvas>();
        if (vitalsView == null || turretView == null ||
            !vitalsView.IsConfigured || !turretView.IsConfigured || levelCatalog == null)
        {
            Debug.LogError("[InitialStagePlayerUi] Connect the prefab views and level catalog.", this);
            enabled = false;
            return;
        }

        _selection = new StageTurretSelection(gameObject.scene);
        _initialized = true;
    }

    private void OnEnable()
    {
        if (!_initialized) return;

        _canvas.enabled = true;
        turretView.Hide();
        _presenter = new StageTurretPresenter(_selection, turretView, levelCatalog,
            CanInteract, () => _controller);
        _nextSourceRetry = 0;
        _nextDisplayRefresh = 0;
    }

    // Start runs after the scene's health sources finish Awake.
    private void Start()
    {
        if (_initialized) BindSceneSources();
    }

    private void Update()
    {
        if (_presenter == null) return;

        if (Time.unscaledTime >= _nextSourceRetry)
        {
            _nextSourceRetry = Time.unscaledTime + SourceRetryInterval;
            BindSceneSources();
        }

        if (CanInteract()) _selection.ProcessInput(_controller);
        else _selection.Clear();

        bool updateDisplay = Time.unscaledTime >= _nextDisplayRefresh;
        if (updateDisplay)
        {
            _nextDisplayRefresh = Time.unscaledTime + DisplayRefreshInterval;
            vitalsView.RefreshPower();
        }

        _presenter.Refresh(updateDisplay);
    }

    private bool CanInteract()
    {
        PlayerInfo player = vitalsView.Player;
        if (!isActiveAndEnabled || Time.timeScale <= 0 ||
            player == null || !player.isActiveAndEnabled || player.curHp <= 0 ||
            _controller == null || !_controller.isActiveAndEnabled)
            return false;

        return _playerControl == null || _playerControl.CanAct;
    }

    private void BindSceneSources()
    {
        if (_controller == null || !_controller.isActiveAndEnabled)
            _controller = TurretController.FindForScene(gameObject.scene);
        vitalsView.BindPower(_controller);

        if (!vitalsView.HasPlayer)
        {
            PlayerInfo player = FindLocalPlayer();
            vitalsView.BindPlayer(player);
            _playerControl = player != null ? player.GetComponent<PlayerControlState>() : null;
        }

        if (!vitalsView.HasControlUnit)
            vitalsView.BindControlUnit(FindControlUnit());
    }

    private PlayerInfo FindLocalPlayer()
    {
        PlayerInfo fallback = null;
        foreach (PlayerInfo player in FindObjectsByType<PlayerInfo>(FindObjectsSortMode.None))
        {
            if (player.gameObject.scene != gameObject.scene || !player.isActiveAndEnabled) continue;

            var identity = player.GetComponent<PlayerActorIdentity>();
            if (identity != null && identity.PlayerId.Value == LocalPlayerId) return player;
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
        _presenter?.Dispose();
        _presenter = null;
        _selection?.Clear();
        if (turretView != null) turretView.Hide();
        if (vitalsView != null) vitalsView.Unbind();
        if (_canvas != null) _canvas.enabled = false;
    }
}
