using TeamHJD.Game.Turrets;
using TeamHJD.Game.Presentation.Scene;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

/// <summary>Initial 씬의 상태 표시와 터렛 선택 UI를 조립합니다.</summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster))]
public sealed class InitialStagePlayerUi : MonoBehaviour
{
    [SerializeField] private RectTransform playerHealthRoot;
    [SerializeField] private RectTransform controlUnitHealthRoot;
    [SerializeField] private RectTransform powerRoot;
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private Sprite menuSprite;
    [SerializeField] private Sprite buttonSprite;
    [SerializeField] private TurretLevelUpgradeCatalog levelCatalog;
    private StagePlayerVitalsView _vitals;
    private StageTurretControlView _view;
    private StageTurretSelection _selection;
    private StageTurretPresenter _presenter;
    private TurretController _controller;
    private float _nextBindTime;

    private void Awake()
    {
        if (playerHealthRoot == null || controlUnitHealthRoot == null || powerRoot == null || font == null)
        {
            Debug.LogError("[InitialStagePlayerUi] 상태 UI와 폰트 참조가 필요합니다.", this);
            enabled = false;
            return;
        }
        var canvas = GetComponent<Canvas>();
        canvas.enabled = true;
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10;
        var scaler = GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = .5f;
        if (EventSystem.current == null)
        {
            var events = new GameObject("Gameplay UI EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(transform, false);
        }
        _vitals = new StagePlayerVitalsView(playerHealthRoot, controlUnitHealthRoot, powerRoot, font);
        _view = new StageTurretControlView((RectTransform)transform, font, menuSprite, buttonSprite);
        _selection = new StageTurretSelection(gameObject.scene);
        _presenter = new StageTurretPresenter(_selection, _view, levelCatalog);
    }

    // Other scene sources initialize in Awake; read their initial values afterwards.
    private void Start()
    {
        if (_presenter != null) BindSceneSources();
    }

    private void Update()
    {
        if (_presenter == null) return;
        if (Time.unscaledTime >= _nextBindTime)
        {
            _nextBindTime = Time.unscaledTime + .5f;
            BindSceneSources();
        }
        _selection.ProcessInput(_controller);
        _presenter.Refresh();
    }

    private void BindSceneSources()
    {
        if (_controller == null) _controller = TurretController.FindForScene(gameObject.scene);
        _vitals.BindPower(_controller);
        if (!_vitals.HasPlayer)
        {
            PlayerInfo fallback = null;
            foreach (var player in FindObjectsByType<PlayerInfo>(FindObjectsSortMode.None))
            {
                if (player.gameObject.scene != gameObject.scene || !player.isActiveAndEnabled) continue;
                var identity = player.GetComponent<PlayerActorIdentity>();
                if (identity != null && identity.PlayerId.Value == "local-player")
                { _vitals.BindPlayer(player); break; }
                if (identity == null && fallback == null) fallback = player;
            }
            if (!_vitals.HasPlayer && fallback != null) _vitals.BindPlayer(fallback);
        }
        if (!_vitals.HasControlUnit)
            foreach (var cu in FindObjectsByType<ControlUnitHealth>(FindObjectsSortMode.None))
                if (cu.gameObject.scene == gameObject.scene) { _vitals.BindControlUnit(cu); break; }
    }

    private void OnDisable() => _selection?.Clear();
    private void OnDestroy()
    {
        _presenter?.Dispose();
        _selection?.Clear();
        _vitals?.Dispose();
    }
}
