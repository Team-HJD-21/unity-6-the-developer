using System;
using TeamHJD.Game.Turrets;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>프리팹에 배치된 터렛 선택창을 표시하고 버튼 입력을 전달합니다.</summary>
[DisallowMultipleComponent]
public sealed class StageTurretControlView : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private TMP_Text statsText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text feedbackText;
    [SerializeField] private TMP_Text activationText;

    [Header("Buttons")]
    [SerializeField] private Button activationButton;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private Button levelDownButton;
    [SerializeField] private Button closeButton;
    [SerializeField] private StageTurretUpgradeView upgradeView;
    [SerializeField] private TMP_Text upgradeRestrictionText;
    public StageTurretUpgradeView Upgrades => upgradeView;

    [Header("Activation Colors")]
    [SerializeField] private Color activateColor = Color.green;
    [SerializeField] private Color deactivateColor = Color.red;
    [SerializeField] private Color unavailableColor = Color.gray;

    private string _lastFeedback = "";
    private bool _withinReach = true;

    public event Action ToggleRequested;
    public event Action LevelUpRequested;
    public event Action LevelDownRequested;
    public event Action CloseRequested;

    public bool IsConfigured => string.IsNullOrEmpty(MissingReferences);
    public string MissingReferences
    {
        get
        {
            string missing = StageUIReferenceValidation.Missing(
                (nameof(titleText), titleText != null), (nameof(levelText), levelText != null),
                (nameof(statsText), statsText != null), (nameof(statusText), statusText != null),
                (nameof(feedbackText), feedbackText != null), (nameof(activationText), activationText != null),
                (nameof(activationButton), activationButton != null), (nameof(levelUpButton), levelUpButton != null),
                (nameof(levelDownButton), levelDownButton != null), (nameof(closeButton), closeButton != null),
                (nameof(upgradeRestrictionText), upgradeRestrictionText != null), (nameof(upgradeView), upgradeView != null));
            if (upgradeView != null && !upgradeView.IsConfigured)
                missing += (missing.Length > 0 ? ", " : "") + $"upgradeView: {upgradeView.MissingReferences}";
            return missing;
        }
    }

    private void OnValidate()
    {
        string missing = MissingReferences;
        if (missing.Length > 0) Debug.LogWarning($"[StageTurretControlView] Missing {missing}.", this);
    }

    private void OnEnable()
    {
        if (!IsConfigured)
        {
            Debug.LogError($"[StageTurretControlView] Missing {MissingReferences}.", this);
            return;
        }

        activationButton.onClick.AddListener(RequestToggle);
        levelUpButton.onClick.AddListener(RequestLevelUp);
        levelDownButton.onClick.AddListener(RequestLevelDown);
        closeButton.onClick.AddListener(RequestClose);
    }

    private void OnDisable()
    {
        if (activationButton != null) activationButton.onClick.RemoveListener(RequestToggle);
        if (levelUpButton != null) levelUpButton.onClick.RemoveListener(RequestLevelUp);
        if (levelDownButton != null) levelDownButton.onClick.RemoveListener(RequestLevelDown);
        if (closeButton != null) closeButton.onClick.RemoveListener(RequestClose);
    }

    public void Show(TurretBase turret, bool canInteract, bool canLevelUp, bool canLevelDown,
        bool dynamicRegions = false, bool withinReach = true)
    {
        if (turret == null)
        {
            Hide();
            return;
        }

        if (!gameObject.activeSelf) gameObject.SetActive(true);

        titleText.text = turret.DisplayName;
        levelText.text = $"Level {turret.Definition?.Level ?? 0}";
        statsText.text = $"HP {turret.CurrentHealth}/{turret.MaxHealth}   Power {turret.EffectivePower}   Damage {turret.EffectiveDamage}   Range {turret.EffectiveRange:0.#}";
        statusText.text = GetStatus(turret);
        if (dynamicRegions) statusText.text += " | Dynamic capture: activate to form territory";
        _withinReach = withinReach;
        RefreshFeedback();

        bool editable = canInteract && turret.CanModifyUpgrades;
        activationButton.interactable = canInteract && StageTurretInteractionRules.CanToggle(turret);
        levelUpButton.interactable = editable && canLevelUp;
        levelDownButton.interactable = editable && canLevelDown;
        upgradeRestrictionText.text = StageTurretUIText.UpgradeRestriction(turret);
        upgradeView.Show(turret, canInteract);

        activationText.text = turret.IsDestroyed ? "Destroyed" :
            turret.IsLocked ? "Locked" : turret.IsActivated ? "Deactivate" : "Activate";
        activationText.color = !activationButton.interactable ? unavailableColor :
            turret.IsActivated ? deactivateColor : activateColor;
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    public void SetFeedback(string message)
    {
        _lastFeedback = message ?? "";
        RefreshFeedback();
    }

    private void RefreshFeedback()
    {
        feedbackText.text = _withinReach ? _lastFeedback : StageTurretUIText.OutOfReach +
            (_lastFeedback.Length > 0 ? $" | {_lastFeedback}" : "");
    }

    private static string GetStatus(TurretBase turret)
    {
        if (!turret.IsInitialized) return "Waiting for turret initialization";
        if (turret.IsManualOperationBlocked)
            return StageTurretUIText.WithCooling("Automatic activation - manual controls disabled", turret);
        if (turret.IsDestroyed) return "Destroyed";
        if (turret.IsLocked) return "Locked";
        if (turret.IsCoolingDown) return $"Cooling down: {turret.CoolingDownRemaining:0.0}s";
        if (turret.IsOperational) return "Operational";
        if (turret.IsActivated) return "Activated - Suspended";
        return "Inactive";
    }

    private void RequestToggle() => ToggleRequested?.Invoke();
    private void RequestLevelUp() => LevelUpRequested?.Invoke();
    private void RequestLevelDown() => LevelDownRequested?.Invoke();
    private void RequestClose() => CloseRequested?.Invoke();
}
