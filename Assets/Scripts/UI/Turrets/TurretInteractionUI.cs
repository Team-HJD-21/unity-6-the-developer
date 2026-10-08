using System.Collections;
using TMPro;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 터렛 선택과 개별 활성화 UI를 제어합니다.
/// 등록·전력·활성화 규칙은 현행 터렛 시스템이 소유합니다.
/// </summary>
public sealed class TurretInteractionUI : MonoBehaviour
{
    // Legacy 제거로 인해 로직 무효화: 구형 알림·대화 Manager 직렬화 의존.
    // [SerializeField] private AlertManager alertManager;
    // [SerializeField] private InGameManager inGameManager;

    [Header("Turret Menu")]
    [SerializeField] private GameObject towerMenu;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private GameObject activateButton;
    [SerializeField] private TMP_Text activateText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text powerText;
    [SerializeField] private TMP_Text damageText;
    [SerializeField] private TMP_Text rpm;

    [Header("Menu Feedback")]
    [SerializeField] private float shakeTime = 0.2f;
    [SerializeField] private float shakeAmount = 1.03f;

    private Animator _animator;
    private TurretBase _selectedTurret;
    private bool _isVisible;
    private Vector3 _menuOriginalPosition;

    private void Awake()
    {
        if (towerMenu == null || activateButton == null)
        {
            Debug.LogError("Turret interaction UI references are missing.", this);
            enabled = false;
            return;
        }

        _animator = towerMenu.GetComponent<Animator>();
        _menuOriginalPosition = towerMenu.GetComponent<RectTransform>().anchoredPosition;
        activateButton.GetComponent<Button>().onClick.AddListener(ToggleSelectedTurret);

        // Legacy 제거로 인해 로직 무효화: 전역 Manager 탐색과 게임 진입 상태 쓰기.
        // alertManager ??= FindFirstObjectByType<AlertManager>();
        // inGameManager ??= FindFirstObjectByType<InGameManager>();
        // GameManager.InGame = true;
        // GameManager.InGameInit = false;
    }

    private void OnDestroy()
    {
        if (activateButton != null && activateButton.TryGetComponent(out Button button))
        {
            button.onClick.RemoveListener(ToggleSelectedTurret);
        }
    }

    private void Update()
    {
        // Legacy 제거로 인해 로직 무효화: 구형 초기화·대화 상태에 따른 입력 잠금.
        // if (GameManager.InGameInit || inGameManager != null && inGameManager.isTalking) return;

        ProcessPointerInput();
    }

    private void ProcessPointerInput()
    {
        if (!GameInput.PrimaryPointerWasPressedThisFrame)
        {
            return;
        }

        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            return;
        }

        Vector2 pointerWorldPosition = Camera.main.ScreenToWorldPoint(GameInput.PointerPosition);
        RaycastHit2D hit = Physics2D.Raycast(pointerWorldPosition, Vector2.zero);
        TurretBase clickedTurret = hit.collider != null
            ? hit.collider.GetComponentInParent<TurretBase>()
            : null;

        if (clickedTurret == null)
        {
            ClearSelection();
            return;
        }

        if (_selectedTurret == clickedTurret)
        {
            return;
        }

        if (_selectedTurret != null)
        {
            _selectedTurret.ShowRange = false;
        }

        _selectedTurret = clickedTurret;
        _selectedTurret.ShowRange = true;
        RefreshTurretInfo();

        if (_isVisible)
        {
            StartCoroutine(ShakeMenuCoroutine());
        }
        else
        {
            _isVisible = true;
            SetMenuVisible();
        }
    }

    private void ClearSelection()
    {
        if (_selectedTurret != null)
        {
            _selectedTurret.ShowRange = false;
        }

        _selectedTurret = null;
        _isVisible = false;
        SetMenuVisible();
    }

    private void SetMenuVisible()
    {
        if (_animator != null)
        {
            _animator.SetBool("isVisible", _isVisible);
        }
    }

    private void RefreshTurretInfo()
    {
        if (_selectedTurret == null)
        {
            return;
        }

        nameText?.SetText(_selectedTurret.DisplayName);
        levelText?.SetText($"Lv {_selectedTurret.Definition?.Level ?? 0}");
        powerText?.SetText($"Power : {_selectedTurret.EffectivePower}");
        damageText?.SetText($"Damage : {_selectedTurret.EffectiveDamage}");

        float shotsPerMinute = (_selectedTurret.Definition?.FireRate ?? 0f) * 60f;
        rpm?.SetText($"RPM : {Mathf.RoundToInt(shotsPerMinute)}");

        if (_selectedTurret.IsDestroyed)
        {
            SetActivationLabel("Destroyed", Color.gray);
        }
        else if (_selectedTurret.IsLocked)
        {
            SetActivationLabel("Locked", Color.gray);
        }
        else if (_selectedTurret.IsActivated)
        {
            SetActivationLabel("Deactivate", Color.red);
        }
        else
        {
            SetActivationLabel("Activate", Color.green);
        }
    }

    private void SetActivationLabel(string text, Color color)
    {
        if (activateText == null)
        {
            return;
        }

        activateText.SetText(text);
        activateText.color = color;
    }

    private void ToggleSelectedTurret()
    {
        if (_selectedTurret == null)
        {
            return;
        }

        TurretActivationResult result = _selectedTurret.RequestActivation(!_selectedTurret.IsActivated);
        switch (result)
        {
            case TurretActivationResult.InsufficientPower:
                // Legacy 제거로 인해 로직 무효화: AlertManager 알림 연결.
                // alertManager?.Show(1);
                Debug.LogWarning("터렛 활성화 실패: 사용할 수 있는 전력이 부족합니다.", _selectedTurret);
                break;
            case TurretActivationResult.PowerSourceUnavailable:
            case TurretActivationResult.InvalidPowerCost:
                Debug.LogError($"Failed to change activation for {_selectedTurret.name}: {result}", _selectedTurret);
                break;
        }

        UpdateMiniMapElement(_selectedTurret);
        RefreshTurretInfo();
    }

    private static void UpdateMiniMapElement(TurretBase turret)
    {
        foreach (Transform child in turret.GetComponentsInChildren<Transform>(true))
        {
            if (!child.CompareTag("MapElement") || !child.TryGetComponent(out SpriteRenderer renderer))
            {
                continue;
            }

            renderer.color = turret.IsActivated ? Color.green : Color.yellow;
            return;
        }
    }

    private IEnumerator ShakeMenuCoroutine()
    {
        float elapsed = 0f;
        const float interval = 0.01f;

        while (elapsed < shakeTime)
        {
            float offsetX = Random.Range(-1f, 1f) * shakeAmount;
            float offsetY = Random.Range(-1f, 1f) * shakeAmount;
            towerMenu.GetComponent<RectTransform>().anchoredPosition =
                _menuOriginalPosition + new Vector3(offsetX, offsetY, 0f);
            elapsed += interval;
            yield return new WaitForSeconds(interval);
        }

        towerMenu.GetComponent<RectTransform>().anchoredPosition = _menuOriginalPosition;
    }
}
