using System.Linq;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Presentation;
using UnityEngine;
using UnityEngine.EventSystems;

public sealed partial class TacticalCommandController
{
    private int AvailablePower => Source.RequiresCommands ? Source.Commands?.Presentation.Snapshot?.Controller.AvailablePower ?? 0 : Source.Turrets?.GetSnapshot().AvailablePower ?? 0;
    private bool Pending(TurretPresentationSnapshot row) => row != null && Source.Commands?.HasPending(row.EntityId) == true;
    private FieldTurretUpgradeDefinition Branch(bool low, TurretPresentationSnapshot row)
        => (low ? Style.LowPower : Style.Firepower)?.FirstOrDefault(d => d != null && row != null && d.IsCompatibleWith(row.DefinitionId.Value));
    private bool LevelTarget(TurretPresentationSnapshot row, bool increase, out FieldTurretBase prefab)
    {
        prefab = null; var catalog = Source.LevelCatalog;
        if (row == null || catalog == null || !catalog.TryGetDefinition(row.DefinitionId.Value, out var definition)) return false;
        return increase ? catalog.TryGetNext(definition, out prefab) : catalog.TryGetPrevious(definition, out prefab);
    }
    private void RefreshDock()
    {
        if (View == null) return;
        var row = Selected;
        View.Dock.gameObject.SetActive(IsOpen && (row != null || _batch.Count > 1));
        bool atCU = IsOpen && Source.IsAtControlUnit;
        if (!atCU && MultipleSelection)
        { MultipleSelection = false; _batch.Clear(); if (row != null) _batch.Add(row.EntityId); }
        View.SelectMultiple.gameObject.SetActive(atCU);
        View.SelectionLabel.color = MultipleSelection ? Style.Common.Accent : Style.Common.Text;
        View.SelectMultiple.GetComponent<UiButtonFeedback>().Icon.color=View.SelectionLabel.color;
        View.AutoActivate.gameObject.SetActive(atCU); View.BatchActivate.gameObject.SetActive(atCU && _batch.Count > 1);
        View.BatchDeactivate.gameObject.SetActive(atCU && _batch.Count > 1);
        var controller = Source.Commands?.Presentation.Snapshot?.Controller;
        View.AutoActivate.interactable = atCU && Source.Commands?.HasPending(default) != true &&
            (Source.RequiresCommands ? controller != null && !controller.IsAutoActivationRunning && controller.AutoActivationCooldown <= 0
                : Source.Turrets != null && !Source.Turrets.IsAutoActivationRunning && Source.Turrets.AutoActivationCooldownRemaining <= 0);
        View.BatchActivate.interactable = atCU && _batch.Any(id => _byId.TryGetValue(id, out var r) && CanOperate(r) && !r.IsActivated && StageTurretInteractionRules.CanToggle(r) && !Pending(r));
        View.BatchDeactivate.interactable = atCU && _batch.Any(id => _byId.TryGetValue(id, out var r) && CanOperate(r) && r.IsActivated && StageTurretInteractionRules.CanToggle(r) && !Pending(r));
        if (row == null) { View.UpgradeDrawer.gameObject.SetActive(false); return; }
        bool can = CanOperate(row), pending = Pending(row);
        View.Title.text = _batch.Count > 1 ? $"선택 {_batch.Count}" : Caption(row);
        int count = EnemyCount(row);
        View.PowerValue.text=row.Power.ToString();View.ThreatValue.text=count.ToString();
        View.ThreatIcon.gameObject.SetActive(count>0);View.ThreatValue.gameObject.SetActive(count>0);
        View.DockState.Set(TacticalSymbolGraphic.ForState(StateOf(row)),StateColor(StateOf(row)));
        SetHealth(View.DockHealth,row);
        Sprite sprite = SpriteFor(row); View.TurretIcon.sprite = sprite; View.TurretIcon.enabled = sprite != null;
        bool territoryLocked=TerritoryLocked(row);
        View.ToggleLabel.text = Regions==null?"확인 중":TacticalPresentationRules.ToggleCaption(row,AvailablePower,pending,territoryLocked);
        View.ToggleLabel.color = pending || territoryLocked || row.IsLocked ? Style.Inactive : row.IsActivated ? Style.Common.Health : StageTurretInteractionRules.CanToggle(row) && AvailablePower>=row.Power?Style.Active:StateColor(StateOf(row));
        var toggleIcon=View.ToggleTurret.GetComponent<UiButtonFeedback>().Icon as TacticalSymbolGraphic;
        if(toggleIcon!=null)toggleIcon.Set(territoryLocked || row.IsLocked?TacticalSymbolGraphic.Symbol.Locked:TacticalSymbolGraphic.Symbol.Active,View.ToggleLabel.color);
        View.ToggleTurret.interactable = can && !pending && StageTurretInteractionRules.CanToggle(row) && (row.IsActivated || AvailablePower >= row.Power);
        View.Upgrade.interactable = can && !pending && row.CanModifyUpgrades;
        View.Restriction.text = pending ? "처리 중" : row.IsDestroyed ? "파괴됨" : row.IsLocked || territoryLocked ? "잠김" : !can ? "상태 확인 중" : row.IsManualOperationBlocked ? "자동 활성화 중" :
            row.IsCoolingDown ? $"냉각 {row.CoolingRemaining:0.0}s" :
            !row.IsActivated && AvailablePower < row.Power ? "전력 부족" : "";
        View.Restriction.color=StateColor(StateOf(row));
        View.Restriction.gameObject.SetActive(false);
        View.DockStateHint.Caption=territoryLocked && !row.IsLocked && !row.IsDestroyed?TacticalPresentationRules.Caption(StateOf(row))+" · 미점유/원격 잠김":string.IsNullOrEmpty(View.Restriction.text)?TacticalPresentationRules.Caption(StateOf(row)):View.Restriction.text;
        bool modify = can && !pending && row.CanModifyUpgrades;
        View.LevelUp.interactable = modify && LevelTarget(row, true, out _); View.LevelDown.interactable = modify && LevelTarget(row, false, out _);
        var low = Branch(true, row); var fire = Branch(false, row);
        View.LowLabel.text = $"저전력 {(low != null && row.SelectedUpgradeId.Value == low.Id ? row.UpgradeLevel : 0)}/5";
        View.FireLabel.text = $"고화력 {(fire != null && row.SelectedUpgradeId.Value == fire.Id ? row.UpgradeLevel : 0)}/5";
        View.LowPlus.interactable = modify && StageTurretInteractionRules.CanChangeUpgrade(row, low, true);
        View.LowMinus.interactable = modify && StageTurretInteractionRules.CanChangeUpgrade(row, low, false);
        View.FirePlus.interactable = modify && StageTurretInteractionRules.CanChangeUpgrade(row, fire, true);
        View.FireMinus.interactable = modify && StageTurretInteractionRules.CanChangeUpgrade(row, fire, false);
        if (!View.Upgrade.interactable) View.UpgradeDrawer.gameObject.SetActive(false);
        View.Restriction.rectTransform.anchoredPosition = new Vector2(0, View.UpgradeDrawer.gameObject.activeSelf ? 242 : 126);
        View.DockStateHint.Card.anchoredPosition=new Vector2(104,View.UpgradeDrawer.gameObject.activeSelf?242:126);
        bool multiple = _batch.Count > 1;
        View.ToggleTurret.gameObject.SetActive(!multiple); View.Upgrade.gameObject.SetActive(!multiple);
        if (multiple)
        {
            var rows = _batch.Where(id => _byId.ContainsKey(id)).Select(id => _byId[id]).ToArray();
            int active = rows.Count(r => r.IsActivated), eligible = rows.Count(r => CanOperate(r) && StageTurretInteractionRules.CanToggle(r));
            View.PowerValue.text=rows.Where(r=>r.IsActivated).Sum(r=>r.Power).ToString();
            float fraction=rows.Sum(r=>r.CurrentHealth)/(float)System.Math.Max(1,rows.Sum(r=>(long)r.MaxHealth));
            View.DockHealth.Set(fraction,fraction,HealthColor(fraction));
            int threats=_enemies.Count(e=>rows.Any(r=>(e-Point(r)).sqrMagnitude<=r.Range*r.Range));
            View.ThreatIcon.gameObject.SetActive(threats>0);View.ThreatValue.gameObject.SetActive(threats>0);View.ThreatValue.text=threats.ToString();
            View.DockState.Set(active==rows.Length?TacticalSymbolGraphic.Symbol.Active:TacticalSymbolGraphic.Symbol.Busy,Style.Common.Text);
            View.TurretIcon.rectTransform.anchoredPosition=new Vector2(8,-12);View.DockState.rectTransform.anchoredPosition=new Vector2(104,-75);
            View.TurretIcon.sprite = Style.Common.Get("owner_cu"); View.TurretIcon.enabled = View.TurretIcon.sprite != null;
            View.Restriction.text = rows.Any(Pending) ? "처리 중" : eligible < rows.Length ? $"조작 가능 {eligible}/{rows.Length}" : "";
            View.UpgradeDrawer.gameObject.SetActive(false);
        }
    }
    private Sprite SpriteFor(TurretPresentationSnapshot row)
    {
        Sprite authored = Style.DetailFor(row.DefinitionId.Value); if (authored != null) return authored;
        if (!_locals.TryGetValue(row.EntityId, out var local) || local == null)
        {
            if (Source.LevelCatalog == null || !Source.LevelCatalog.TryGetPrefab(row.DefinitionId.Value, out local)) return null;
        }
        var renderers = local.GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var renderer in renderers.OrderByDescending(r => r.sortingOrder))
        {
            string name = (renderer.name + "/" + renderer.sprite?.name).ToLowerInvariant();
            if (renderer.sprite != null && !name.Contains("range") && !name.Contains("minimap") && !name.Contains("flash")) return renderer.sprite;
        }
        return null;
    }
    public void ToggleSelected()
    {
        var row = Selected;
        if (!CanOperate(row) || Pending(row) || !StageTurretInteractionRules.CanToggle(row)) return;
        Submit(row, TurretCommandKind.SetActivation, !row.IsActivated);
    }
    private void ToggleUpgrade()
    {
        if (!IsOpen || !CanOperate(Selected) || !Selected.CanModifyUpgrades || Pending(Selected)) return;
        View.UpgradeDrawer.gameObject.SetActive(!View.UpgradeDrawer.gameObject.activeSelf);
        RefreshDock();
        EventSystem.current?.SetSelectedGameObject(null);
    }
    private void ChangeLevel(bool increase)
    {
        var row = Selected;
        if (!CanOperate(row) || !row.CanModifyUpgrades || Pending(row) || !LevelTarget(row, increase, out _)) return;
        Submit(row, increase ? TurretCommandKind.UpgradeLevel : TurretCommandKind.DowngradeLevel);
    }
    private void ChangeBranch(bool low, bool increase)
    {
        var row = Selected; var definition = Branch(low, row);
        if (!CanOperate(row) || Pending(row) || !StageTurretInteractionRules.CanChangeUpgrade(row, definition, increase)) return;
        Submit(row, increase ? TurretCommandKind.ApplyUpgrade : TurretCommandKind.DowngradeUpgrade, upgrade: definition);
    }
    private void Submit(TurretPresentationSnapshot row, TurretCommandKind kind, bool activate = false, FieldTurretUpgradeDefinition upgrade = null)
    {
        if (row == null || !CanOperate(row)) return;
        if (Source.RequiresCommands)
        {
            if (Source.Commands == null || !Source.Commands.TrySubmit(kind, row.EntityId, activate, upgrade != null ? new DefinitionId(upgrade.Id) : default))
                StageTurretAlertFeedback.Show("Request unavailable or already pending.");
        }
        else if (_locals.TryGetValue(row.EntityId, out var turret) && turret != null)
        {
            string feedback;
            if (kind == TurretCommandKind.SetActivation) feedback = StageTurretUIText.Activation(Source.Turrets.RequestActivation(turret.InstanceId, activate));
            else if (kind == TurretCommandKind.ApplyUpgrade || kind == TurretCommandKind.DowngradeUpgrade)
                feedback = StageTurretUIText.Upgrade(kind == TurretCommandKind.ApplyUpgrade ? turret.ApplyUpgrade(upgrade) : turret.DowngradeUpgrade(upgrade));
            else if (LevelTarget(row, kind == TurretCommandKind.UpgradeLevel, out var prefab))
                feedback = StageTurretUIText.Level(kind == TurretCommandKind.UpgradeLevel ? turret.RequestLevelUpgrade(prefab, out _) : turret.RequestLevelDowngrade(prefab, out _));
            else feedback = "No available level change.";
            StageTurretAlertFeedback.Show(feedback);
        }
        _nextSample = 0; EventSystem.current?.SetSelectedGameObject(null); RefreshDock();
    }
    private void AutoActivate()
    {
        if (IsOpen && Source.IsAtControlUnit) Source.TryRequestAutoActivation();
        EventSystem.current?.SetSelectedGameObject(null); _nextSample = 0;
    }
    private void BatchActivation(bool activate)
    {
        if (!IsOpen || !Source.IsAtControlUnit) return;
        foreach (var id in _batch.ToArray())
            if (_byId.TryGetValue(id, out var row) && row.IsActivated != activate && !Pending(row) && StageTurretInteractionRules.CanToggle(row))
                Submit(row, TurretCommandKind.SetActivation, activate);
    }
}
