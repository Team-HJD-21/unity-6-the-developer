using System;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Inspector로 연결한 게이지에 체력과 전력을 표시합니다.</summary>
[DisallowMultipleComponent]
public sealed class StagePlayerVitalsView : MonoBehaviour
{
    [Header("Player Health")]
    [SerializeField] private Image[] healthCells;
    [SerializeField] private TMP_Text playerHealthText;
    [SerializeField] private Color filledCellColor = new Color(.4f, 1f, 1f);
    [SerializeField] private Color emptyCellColor = new Color(.25f, .4f, .45f);

    [Header("Control Unit Health")]
    [SerializeField] private Image controlUnitFill;
    [SerializeField] private TMP_Text controlUnitHealthText;

    [Header("Power")]
    [SerializeField] private Image powerFill;
    [SerializeField] private TMP_Text powerPercentText;
    [SerializeField] private TMP_Text powerDetailsText;

    private PlayerInfo _player;
    private ControlUnitHealth _controlUnit;
    private TurretController _controller;
    private int _lastHealth = int.MinValue;
    private int _lastMaxHealth = int.MinValue;
    private int _lastPower = int.MinValue;
    private int _lastMaxPower = int.MinValue;
    private int _lastPendingPower = int.MinValue;

    public PlayerInfo Player => _player;
    public bool HasPlayer => _player != null && _player.isActiveAndEnabled;
    public bool HasControlUnit => _controlUnit != null && _controlUnit.isActiveAndEnabled;
    public ControlUnitHealth ControlUnit => _controlUnit;
    public bool IsConfigured => string.IsNullOrEmpty(MissingReferences);
    public string MissingReferences => StageUIReferenceValidation.Missing(
        (nameof(healthCells), healthCells != null && healthCells.Length > 0 &&
            Array.TrueForAll(healthCells, cell => cell != null)),
        (nameof(playerHealthText), playerHealthText != null),
        (nameof(controlUnitFill), controlUnitFill != null),
        (nameof(controlUnitHealthText), controlUnitHealthText != null),
        (nameof(powerFill), powerFill != null),
        (nameof(powerPercentText), powerPercentText != null),
        (nameof(powerDetailsText), powerDetailsText != null));

    private void OnValidate()
    {
        string missing = MissingReferences;
        if (missing.Length > 0) Debug.LogWarning($"[StagePlayerVitalsView] Missing {missing}.", this);
    }

    public void BindPlayer(PlayerInfo player)
    {
        if (ReferenceEquals(_player, player))
        {
            if (_player != null) ShowPlayerHealth(_player.curHp, _player.maxHp);
            else ShowPlayerHealth(0, 0);
            return;
        }
        if (_player != null) _player.onHpChange.RemoveListener(ShowPlayerHealth);

        _player = player;
        if (_player != null)
        {
            _player.onHpChange.AddListener(ShowPlayerHealth);
            ShowPlayerHealth(_player.curHp, _player.maxHp);
        }
        else
        {
            _lastHealth = _lastMaxHealth = int.MinValue;
            ShowPlayerHealth(0, 0);
        }
    }

    public void BindControlUnit(ControlUnitHealth controlUnit)
    {
        if (ReferenceEquals(_controlUnit, controlUnit))
        {
            if (_controlUnit != null) ShowControlUnitHealth(_controlUnit.CurrentHealth, _controlUnit.MaxHealth);
            else ShowControlUnitUnavailable();
            return;
        }
        if (_controlUnit != null) _controlUnit.HealthChanged -= ShowControlUnitHealth;

        _controlUnit = controlUnit;
        if (_controlUnit != null)
        {
            _controlUnit.HealthChanged += ShowControlUnitHealth;
            ShowControlUnitHealth(_controlUnit.CurrentHealth, _controlUnit.MaxHealth);
        }
        else
        {
            ShowControlUnitUnavailable();
        }
    }

    public void ShowWaitingForSources()
    {
        _lastHealth = _lastMaxHealth = int.MinValue;
        ShowPlayerHealth(0, 0);
        ShowControlUnitUnavailable();
        powerFill.fillAmount = 0;
        powerPercentText.text = "—";
        powerDetailsText.text = "Waiting for power source";
        _lastPower = _lastMaxPower = _lastPendingPower = int.MinValue;
    }

    private void ShowControlUnitUnavailable()
    {
        controlUnitFill.fillAmount = 0;
        controlUnitHealthText.text = "—";
    }

    public void BindPower(TurretController controller)
    {
        if (!ReferenceEquals(_controller, controller))
        {
            if (_controller != null) _controller.PowerChanged -= OnPowerChanged;
            _controller = controller;
            if (_controller != null) _controller.PowerChanged += OnPowerChanged;
        }

        RefreshPower();
    }

    // Pending recovery can change without a PowerChanged notification.
    public void RefreshPower()
    {
        if (_controller == null)
        {
            powerFill.fillAmount = 0;
            powerPercentText.text = "—";
            powerDetailsText.text = "Waiting for power source";
            _lastPower = _lastMaxPower = _lastPendingPower = int.MinValue;
            return;
        }

        var budget = _controller.Budget;
        int available = budget.CurrentPower;
        int maximum = budget.MaximumPower;
        int pending = budget.PendingRecovery;
        if (available == _lastPower && maximum == _lastMaxPower && pending == _lastPendingPower) return;

        ShowPower(available, maximum, pending);
    }

    public void UseAuthorityVitals()
    {
        if (_controlUnit != null) _controlUnit.HealthChanged -= ShowControlUnitHealth;
        if (_controller != null) _controller.PowerChanged -= OnPowerChanged;
        _controlUnit = null; _controller = null;
        _lastPower = _lastMaxPower = _lastPendingPower = int.MinValue;
    }
    public void RefreshAuthority(TurretControllerPresentationSnapshot state)
    {
        if (state == null) { ShowControlUnitUnavailable(); RefreshPower(); return; }
        if (state.IsControlUnitAvailable) ShowControlUnitHealth(state.ControlUnitHealth, state.ControlUnitMaxHealth);
        else ShowControlUnitUnavailable();
        ShowPower(state.AvailablePower, state.MaximumPower, state.PendingRecoveryPower);
    }
    private void ShowPower(int available, int maximum, int pending)
    {
        if (available == _lastPower && maximum == _lastMaxPower && pending == _lastPendingPower) return;
        _lastPower = available;
        _lastMaxPower = maximum;
        _lastPendingPower = pending;
        float ratio = maximum > 0 ? Mathf.Clamp01((float)available / maximum) : 0;
        powerFill.fillAmount = ratio;
        powerPercentText.text = $"{Mathf.RoundToInt(ratio * 100)}%";
        powerDetailsText.text = $"Power {available}/{maximum} | Recovering {pending}";
    }

    public void Unbind()
    {
        if (_player != null) _player.onHpChange.RemoveListener(ShowPlayerHealth);
        if (_controlUnit != null) _controlUnit.HealthChanged -= ShowControlUnitHealth;
        if (_controller != null) _controller.PowerChanged -= OnPowerChanged;

        _player = null;
        _controlUnit = null;
        _controller = null;
        _lastHealth = _lastMaxHealth = int.MinValue;
        _lastPower = _lastMaxPower = _lastPendingPower = int.MinValue;
    }

    private void ShowPlayerHealth(int current, int maximum)
    {
        if (_lastHealth == current && _lastMaxHealth == maximum) return;
        _lastHealth = current;
        _lastMaxHealth = maximum;

        float ratio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0;
        for (int i = 0; i < healthCells.Length; i++)
        {
            float fill = Mathf.Clamp01(ratio * healthCells.Length - i);
            healthCells[i].color = Color.Lerp(emptyCellColor, filledCellColor, fill);
        }

        playerHealthText.text = maximum > 0 ?
            $"Player HP {Mathf.Clamp(current, 0, maximum)} / {maximum}" : "Waiting for player";
    }

    private void ShowControlUnitHealth(int current, int maximum)
    {
        float ratio = maximum > 0 ? Mathf.Clamp01((float)current / maximum) : 0;
        controlUnitFill.fillAmount = ratio;
        controlUnitHealthText.text = $"{Mathf.RoundToInt(ratio * 100)}%";
    }

    private void OnPowerChanged(int available, int maximum) => RefreshPower();

    private void OnDisable() => Unbind();
}
