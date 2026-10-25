using System;
using TeamHJD.Game.Content;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Contracts;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>호환되는 강화 분기의 표시와 입력만 담당합니다. 명령은 Presenter가 실행합니다.</summary>
[DisallowMultipleComponent]
public sealed class StageTurretUpgradeView : MonoBehaviour
{
    [SerializeField] private TurretUpgradeDefinition[] lowPowerUpgrades;
    [SerializeField] private TurretUpgradeDefinition[] highFirepowerUpgrades;
    [SerializeField] private TMP_Text lowPowerText;
    [SerializeField] private TMP_Text highFirepowerText;
    [SerializeField] private Button lowPowerPlus;
    [SerializeField] private Button lowPowerMinus;
    [SerializeField] private Button highFirepowerPlus;
    [SerializeField] private Button highFirepowerMinus;
    private TurretUpgradeDefinition _lowPower;
    private TurretUpgradeDefinition _highFirepower;

    public event Action<TurretUpgradeDefinition, bool> ChangeRequested;
    public bool IsConfigured => string.IsNullOrEmpty(MissingReferences);
    public string MissingReferences => StageUIReferenceValidation.Missing(
        (nameof(lowPowerText), lowPowerText != null), (nameof(highFirepowerText), highFirepowerText != null),
        (nameof(lowPowerPlus), lowPowerPlus != null), (nameof(lowPowerMinus), lowPowerMinus != null),
        (nameof(highFirepowerPlus), highFirepowerPlus != null), (nameof(highFirepowerMinus), highFirepowerMinus != null),
        (nameof(lowPowerUpgrades), HasDefinitions(lowPowerUpgrades)),
        (nameof(highFirepowerUpgrades), HasDefinitions(highFirepowerUpgrades)));

    private static bool HasDefinitions(TurretUpgradeDefinition[] definitions) =>
        definitions != null && definitions.Length > 0 && Array.TrueForAll(definitions, value => value != null);

    private void OnValidate()
    {
        string missing = MissingReferences;
        if (missing.Length > 0) Debug.LogWarning($"[StageTurretUpgradeView] Missing {missing}.", this);
    }

    private void OnEnable()
    {
        if (!IsConfigured) return;
        lowPowerPlus.onClick.AddListener(IncreaseLowPower);
        lowPowerMinus.onClick.AddListener(DecreaseLowPower);
        highFirepowerPlus.onClick.AddListener(IncreaseHighFirepower);
        highFirepowerMinus.onClick.AddListener(DecreaseHighFirepower);
    }

    private void OnDisable()
    {
        if (lowPowerPlus != null) lowPowerPlus.onClick.RemoveListener(IncreaseLowPower);
        if (lowPowerMinus != null) lowPowerMinus.onClick.RemoveListener(DecreaseLowPower);
        if (highFirepowerPlus != null) highFirepowerPlus.onClick.RemoveListener(IncreaseHighFirepower);
        if (highFirepowerMinus != null) highFirepowerMinus.onClick.RemoveListener(DecreaseHighFirepower);
        _lowPower = null;
        _highFirepower = null;
    }

    public void Show(TurretBase turret, bool canInteract)
    {
        _lowPower = FindCompatible(lowPowerUpgrades, turret);
        _highFirepower = FindCompatible(highFirepowerUpgrades, turret);
        ShowBranch(turret, _lowPower, "Low Power", lowPowerText, lowPowerPlus, lowPowerMinus, canInteract);
        ShowBranch(turret, _highFirepower, "High Firepower", highFirepowerText,
            highFirepowerPlus, highFirepowerMinus, canInteract);
    }

    public void ShowSnapshot(TurretPresentationSnapshot turret, bool canInteract)
    {
        _lowPower = turret == null ? null : FindCompatible(lowPowerUpgrades, turret.DefinitionId.Value);
        _highFirepower = turret == null ? null : FindCompatible(highFirepowerUpgrades, turret.DefinitionId.Value);
        ShowSnapshotBranch(turret, _lowPower, "Low Power", lowPowerText, lowPowerPlus, lowPowerMinus, canInteract);
        ShowSnapshotBranch(turret, _highFirepower, "High Firepower", highFirepowerText, highFirepowerPlus, highFirepowerMinus, canInteract);
    }
    private static TurretUpgradeDefinition FindCompatible(TurretUpgradeDefinition[] definitions, string definitionId)
    {
        foreach (var upgrade in definitions) if (upgrade != null && upgrade.IsCompatibleWith(definitionId)) return upgrade;
        return null;
    }
    private static void ShowSnapshotBranch(TurretPresentationSnapshot turret, TurretUpgradeDefinition upgrade, string name,
        TMP_Text text, Button plus, Button minus, bool canInteract)
    {
        bool selected = turret != null && upgrade != null && turret.SelectedUpgradeId.Value == upgrade.Id;
        text.text = upgrade == null ? $"{name}: unavailable" : $"{name} {(selected ? turret.UpgradeLevel : 0)}/{TurretRuntimeState.MaximumUpgradeLevel}";
        text.color = selected ? Color.cyan : Color.white;
        plus.interactable = canInteract && StageTurretInteractionRules.CanChangeUpgrade(turret, upgrade, true);
        minus.interactable = canInteract && StageTurretInteractionRules.CanChangeUpgrade(turret, upgrade, false);
    }

    private static TurretUpgradeDefinition FindCompatible(TurretUpgradeDefinition[] upgrades, TurretBase turret)
    {
        if (turret.Definition == null) return null;
        foreach (var upgrade in upgrades)
            if (upgrade != null && upgrade.IsCompatibleWith(turret.Definition.Id)) return upgrade;
        return null;
    }

    private static void ShowBranch(TurretBase turret, TurretUpgradeDefinition upgrade, string name,
        TMP_Text text, Button plus, Button minus, bool canInteract)
    {
        bool selected = upgrade != null && turret.RuntimeState.HasAppliedUpgrade(upgrade.Id);
        int level = selected ? turret.RuntimeState.UpgradeLevel : 0;
        text.text = upgrade == null ? $"{name}: unavailable" :
            $"{name} {level}/{TurretRuntimeState.MaximumUpgradeLevel}";
        text.color = selected ? Color.cyan : Color.white;
        plus.interactable = canInteract && StageTurretInteractionRules.CanChangeUpgrade(turret, upgrade, true);
        minus.interactable = canInteract && StageTurretInteractionRules.CanChangeUpgrade(turret, upgrade, false);
    }

    private void IncreaseLowPower() => ChangeRequested?.Invoke(_lowPower, true);
    private void DecreaseLowPower() => ChangeRequested?.Invoke(_lowPower, false);
    private void IncreaseHighFirepower() => ChangeRequested?.Invoke(_highFirepower, true);
    private void DecreaseHighFirepower() => ChangeRequested?.Invoke(_highFirepower, false);
}
