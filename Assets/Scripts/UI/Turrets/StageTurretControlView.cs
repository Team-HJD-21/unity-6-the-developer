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

    [Header("Activation Colors")]
    [SerializeField] private Color activateColor = Color.green;
    [SerializeField] private Color deactivateColor = Color.red;
    [SerializeField] private Color unavailableColor = Color.gray;

    public event Action ToggleRequested;
    public event Action LevelUpRequested;
    public event Action LevelDownRequested;
    public event Action CloseRequested;

    public bool IsConfigured =>
        titleText != null && levelText != null && statsText != null &&
        statusText != null && feedbackText != null && activationText != null &&
        activationButton != null && levelUpButton != null &&
        levelDownButton != null && closeButton != null;

    private void OnEnable()
    {
        if (!IsConfigured)
        {
            Debug.LogError("[StageTurretControlView] Connect all text and button references.", this);
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

    public void Show(TurretBase turret, bool canInteract, bool canLevelUp, bool canLevelDown)
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

        bool editable = canInteract && turret.IsInitialized && !turret.IsDestroyed && !turret.IsLocked;
        activationButton.interactable = editable && !turret.IsManualActivationBlocked &&
                                        (turret.IsActivated || !turret.IsCoolingDown);
        levelUpButton.interactable = editable && canLevelUp;
        levelDownButton.interactable = editable && canLevelDown;

        activationText.text = turret.IsDestroyed ? "Destroyed" :
            turret.IsLocked ? "Locked" : turret.IsActivated ? "Deactivate" : "Activate";
        activationText.color = turret.IsDestroyed || turret.IsLocked ? unavailableColor :
            turret.IsActivated ? deactivateColor : activateColor;
    }

    public void Hide()
    {
        if (gameObject.activeSelf) gameObject.SetActive(false);
    }

    public void SetFeedback(string message) => feedbackText.text = message;

    private static string GetStatus(TurretBase turret)
    {
        if (turret.IsDestroyed) return "Destroyed";
        if (turret.IsLocked) return "Locked";
        if (turret.IsOperational) return "Operational";
        if (turret.IsActivated) return "Activated - Suspended";
        if (turret.IsCoolingDown) return $"Cooling down: {turret.CoolingDownRemaining:0.0}s";
        return "Inactive";
    }

    private void RequestToggle() => ToggleRequested?.Invoke();
    private void RequestLevelUp() => LevelUpRequested?.Invoke();
    private void RequestLevelDown() => LevelDownRequested?.Invoke();
    private void RequestClose() => CloseRequested?.Invoke();
}
