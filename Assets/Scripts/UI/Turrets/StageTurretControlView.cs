using System;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Contracts;
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
    private float _feedbackUntil;
    private Image[] _levelUpGlyphs, _levelDownGlyphs;

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
        _levelUpGlyphs = Array.FindAll(levelUpButton.GetComponentsInChildren<Image>(true), i => i.name.EndsWith("Stroke", StringComparison.Ordinal));
        _levelDownGlyphs = Array.FindAll(levelDownButton.GetComponentsInChildren<Image>(true), i => i.name.EndsWith("Stroke", StringComparison.Ordinal));
        var feedback = activationButton.GetComponent<TeamHJD.Game.Presentation.UiButtonFeedback>();
        if (feedback != null) feedback.PreserveLabelColor = true;
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

    public void Show(FieldTurretBase turret, bool canInteract, bool canLevelUp, bool canLevelDown,
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
        SetText(statusText, GetStatus(turret));
        _withinReach = withinReach;
        RefreshFeedback();

        bool editable = canInteract && turret.CanModifyUpgrades;
        activationButton.interactable = canInteract && StageTurretInteractionRules.CanToggle(turret);
        levelUpButton.interactable = editable && canLevelUp;
        levelDownButton.interactable = editable && canLevelDown;
        RefreshLevelGlyphs();
        SetText(upgradeRestrictionText, StageTurretUIText.UpgradeRestriction(turret));
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
        // 대기/거리 상태는 패널에 남기고, 실제 조작 결과는 공통 Alert 한 곳에서 표시합니다.
        _lastFeedback = StageTurretAlertFeedback.IsPending(message) ? message : "";
        _feedbackUntil = _lastFeedback.Length > 0 ? float.PositiveInfinity : 0;
        StageTurretAlertFeedback.Show(message);
        RefreshFeedback();
    }

    private void RefreshFeedback()
    {
        string recent = Time.unscaledTime < _feedbackUntil ? _lastFeedback : "";
        SetText(feedbackText, _withinReach ? recent : StageTurretUIText.OutOfReach +
            (recent.Length > 0 ? $" | {recent}" : ""));
    }
    private void LateUpdate() { if (feedbackText != null) RefreshFeedback(); }
    private static void SetText(TMP_Text text, string value) { text.text = value; text.enabled = value.Length > 0; }
    public void ShowWaitingForHost()
    {
        _withinReach = true;
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        titleText.text = "Turret"; levelText.text = "Level —"; statsText.text = "—";
        SetText(statusText, "Waiting for state"); SetText(upgradeRestrictionText, "");
        activationButton.interactable = levelUpButton.interactable = levelDownButton.interactable = false;
        RefreshLevelGlyphs();
        activationText.text = "Waiting"; activationText.color = unavailableColor; upgradeView.ShowSnapshot(null, false);
    }
    public void ShowSnapshot(TurretPresentationSnapshot turret, bool canInteract, bool canLevelUp, bool canLevelDown,
        bool dynamicRegions, bool pending, bool withinReach = true)
    {
        if (turret == null) { ShowWaitingForHost(); return; }
        if (!gameObject.activeSelf) gameObject.SetActive(true);
        titleText.text = turret.DisplayName; levelText.text = $"Level {turret.Level}";
        statsText.text = $"HP {turret.CurrentHealth}/{turret.MaxHealth}   Power {turret.Power}   Damage {turret.Damage}   Range {turret.Range:0.#}";
        SetText(statusText, pending ? "Processing" : turret.IsManualOperationBlocked ? "Automatic activation - manual controls disabled" :
            turret.IsDestroyed ? "Destroyed" : turret.IsLocked ? "Locked" : turret.IsCoolingDown ? $"Cooling down: {turret.CoolingRemaining:0.0}s" :
            turret.IsOperational ? "" : turret.IsActivated ? "Activated - Suspended" : "");
        _withinReach = withinReach;
        RefreshFeedback();
        bool editable = canInteract && !pending && turret.CanModifyUpgrades;
        activationButton.interactable = canInteract && !pending && StageTurretInteractionRules.CanToggle(turret);
        levelUpButton.interactable = editable && canLevelUp; levelDownButton.interactable = editable && canLevelDown;
        RefreshLevelGlyphs();
        SetText(upgradeRestrictionText, StageTurretUIText.UpgradeRestriction(turret)); upgradeView.ShowSnapshot(turret, canInteract && !pending);
        activationText.text = turret.IsDestroyed ? "Destroyed" : turret.IsLocked ? "Locked" : turret.IsActivated ? "Deactivate" : "Activate";
        activationText.color = !activationButton.interactable ? unavailableColor : turret.IsActivated ? deactivateColor : activateColor;
    }


    private static string GetStatus(FieldTurretBase turret)
    {
        if (!turret.IsInitialized) return "Waiting for turret initialization";
        if (turret.IsManualOperationBlocked)
            return StageTurretUIText.WithCooling("Automatic activation - manual controls disabled", turret);
        if (turret.IsDestroyed) return "Destroyed";
        if (turret.IsLocked) return "Locked";
        if (turret.IsCoolingDown) return $"Cooling down: {turret.CoolingDownRemaining:0.0}s";
        if (turret.IsOperational) return "";
        if (turret.IsActivated) return "Activated - Suspended";
        return "";
    }
    private void RefreshLevelGlyphs()
    {
        if (_levelUpGlyphs != null) foreach (var image in _levelUpGlyphs) image.color = levelUpButton.interactable ? Color.white : unavailableColor;
        if (_levelDownGlyphs != null) foreach (var image in _levelDownGlyphs) image.color = levelDownButton.interactable ? Color.white : unavailableColor;
    }

    private void RequestToggle() => ToggleRequested?.Invoke();
    private void RequestLevelUp() => LevelUpRequested?.Invoke();
    private void RequestLevelDown() => LevelDownRequested?.Invoke();
    private void RequestClose() => CloseRequested?.Invoke();
}
