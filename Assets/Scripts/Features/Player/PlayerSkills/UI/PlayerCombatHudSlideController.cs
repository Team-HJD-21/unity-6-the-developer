using UnityEngine;

/// <summary>포탑 조작창 또는 플레이어 상태창이 열리면 미리 배치된 무기·대시 UI를 화면 아래로 접습니다.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(250)]
[UnityEngine.Scripting.APIUpdating.MovedFrom(true, sourceNamespace: "", sourceAssembly: "Assembly-CSharp", sourceClassName: "PlayerCombatHudLayout")]
public sealed class PlayerCombatHudSlideController : MonoBehaviour
{
    [SerializeField] private RectTransform _weapons;
    [SerializeField] private RectTransform _dash;
    [SerializeField] private CanvasGroup _weaponInput;
    [SerializeField, Min(0)] private float _visibleEdge = 10f;
    [SerializeField, Min(.01f)] private float _slideSeconds = .16f;
    private InitialStagePlayerHUD _stageHud;
    private PlayerStatusPanel _statusPanel;
    private Vector2 _weaponHome;
    private Vector2 _dashHome;
    private float _fold;
    private float _nextSearch;
    private bool _initialized;

    private void Awake()
    {
        if (_weapons == null || _dash == null || _weaponInput == null)
        {
            Debug.LogError("[PlayerCombatHudSlideController] Connect the authored weapon/dash RectTransforms and weapon CanvasGroup.", this);
            enabled = false;
            return;
        }
        _weaponHome = _weapons.anchoredPosition;
        _dashHome = _dash.anchoredPosition;
        _initialized = true;
    }

    private void LateUpdate()
    {
        if (!_initialized) return;
        if (_stageHud == null && Time.unscaledTime >= _nextSearch)
        {
            _nextSearch = Time.unscaledTime + .5f;
            foreach (var hud in FindObjectsByType<InitialStagePlayerHUD>(FindObjectsSortMode.None))
                if (hud.gameObject.scene == gameObject.scene)
                {
                    _stageHud = hud;
                    _statusPanel = hud.GetComponent<PlayerStatusPanel>();
                    break;
                }
        }
        if (_statusPanel == null && _stageHud != null)
            _statusPanel = _stageHud.GetComponent<PlayerStatusPanel>();
        bool panelOpen = (_stageHud != null && _stageHud.IsTurretPanelOpen) ||
            (_statusPanel != null && _statusPanel.isActiveAndEnabled && _statusPanel.IsOpen);
        Refresh(panelOpen, Time.unscaledDeltaTime);
    }

    public void Refresh(bool panelOpen, float elapsed)
    {
        if (!_initialized) return;
        _fold = Mathf.MoveTowards(_fold, panelOpen ? 1f : 0f, Mathf.Max(0f, elapsed) / Mathf.Max(.01f, _slideSeconds));
        float progress = Mathf.SmoothStep(0f, 1f, _fold);
        Slide(_weapons, _weaponHome, progress);
        Slide(_dash, _dashHome, progress);
        // PlayerWeaponHud가 매 프레임 원래 선택 가능 여부를 갱신한 뒤, 접힌 슬롯의 클릭만 차단합니다.
        if (panelOpen || _fold > 0f)
        {
            _weaponInput.interactable = false;
            _weaponInput.blocksRaycasts = false;
        }
    }

    private void Slide(RectTransform rect, Vector2 home, float progress)
    {
        float hiddenY = _visibleEdge - rect.rect.height * (1f - rect.pivot.y);
        rect.anchoredPosition = Vector2.Lerp(home, new Vector2(home.x, hiddenY), progress);
    }

    private void OnDisable()
    {
        if (!_initialized) return;
        _weapons.anchoredPosition = _weaponHome;
        _dash.anchoredPosition = _dashHome;
        _fold = 0f;
        _stageHud = null;
        _statusPanel = null;
        _nextSearch = 0f;
    }
}
