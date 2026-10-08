using System.Collections;
using TMPro;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Handles direct turret selection and the per-turret activation UI.
/// Turret registration, power, and activation rules remain owned by the turret system.
/// </summary>
public sealed class TurretInteractionUI : MonoBehaviour
{
    [Header("Dependencies")]
    [SerializeField] private AlertManager alertManager;
    [SerializeField] private InGameManager inGameManager;

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

        alertManager ??= FindFirstObjectByType<AlertManager>();
        inGameManager ??= FindFirstObjectByType<InGameManager>();

        GameManager.InGame = true;
        GameManager.InGameInit = false;
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
        if (GameManager.InGameInit || inGameManager != null && inGameManager.isTalking)
        {
            return;
        }

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
                alertManager?.Show(1);
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
