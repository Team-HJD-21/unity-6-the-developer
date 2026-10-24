using System;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Domain;

/// <summary>선택 ID를 유지하고 UI 입력을 기존 터렛 API에 전달합니다.</summary>
internal sealed class StageTurretPresenter : IDisposable
{
    private readonly StageTurretSelection _selection;
    private readonly StageTurretControlView _view;
    private readonly FieldTurretLevelUpgradeCatalog _catalog;
    private readonly Func<bool> _canInteract;
    private readonly Func<FieldTurretController> _getController;
    private int _displayedId;
    private bool _displayedPermission;
    private bool _dirty = true;
    private bool _disposed;

    public StageTurretPresenter(StageTurretSelection selection, StageTurretControlView view,
        FieldTurretLevelUpgradeCatalog catalog, Func<bool> canInteract, Func<FieldTurretController> getController)
    {
        _selection = selection;
        _view = view;
        _catalog = catalog;
        _canInteract = canInteract;
        _getController = getController;

        _view.ToggleRequested += Toggle;
        _view.LevelUpRequested += LevelUp;
        _view.LevelDownRequested += LevelDown;
        _view.CloseRequested += Close;
        _view.Upgrades.ChangeRequested += ChangeUpgrade;
        FieldTurretInstanceRegistry.SnapshotChanged += OnSnapshotChanged;
    }

    public void Refresh(bool refreshDisplay = false)
    {
        if (_disposed) return;

        FieldTurretBase turret = _selection.Resolve();
        bool canInteract = _canInteract();
        bool selectionChanged = _displayedId != _selection.SelectedId;
        bool permissionChanged = _displayedPermission != canInteract;
        if (!selectionChanged && !permissionChanged && !_dirty && !refreshDisplay) return;

        if (selectionChanged) _view.SetFeedback("");
        _displayedId = _selection.SelectedId;
        _displayedPermission = canInteract;
        _dirty = false;

        bool canLevelUp = turret != null && _catalog.TryGetNext(turret.Definition, out _);
        bool canLevelDown = turret != null && _catalog.TryGetPrevious(turret.Definition, out _);
        _view.Show(turret, canInteract, canLevelUp, canLevelDown,
            _getController()?.Regions?.Snapshot.Mode == RegionPolicyMode.Dynamic);
    }

    private void Toggle()
    {
        if (!CanRequest(out FieldTurretBase turret)) return;
        if (!StageTurretInteractionRules.CanToggle(turret)) return;
        FieldTurretController controller = _getController();
        if (controller == null) return;

        // The ID-based controller API also rejects a turret using a different power source.
        var result = controller.RequestActivation(turret.InstanceId, !turret.IsActivated);
        _view.SetFeedback(StageTurretUIText.Activation(result));
        Refresh(true);
    }

    private void LevelUp() => ChangeLevel(true);
    private void LevelDown() => ChangeLevel(false);

    private void ChangeUpgrade(FieldTurretUpgradeDefinition upgrade, bool increase)
    {
        if (!CanRequest(out FieldTurretBase turret)) return;
        if (!StageTurretInteractionRules.CanChangeUpgrade(turret, upgrade, increase)) return;
        var result = increase ? turret.ApplyUpgrade(upgrade) : turret.DowngradeUpgrade(upgrade);
        _view.SetFeedback(StageTurretUIText.Upgrade(result));
        Refresh(true);
    }

    private void ChangeLevel(bool upgrade)
    {
        if (!CanRequest(out FieldTurretBase turret)) return;
        if (!turret.CanModifyUpgrades) return;

        FieldTurretBase targetPrefab;
        bool found = upgrade
            ? _catalog.TryGetNext(turret.Definition, out targetPrefab)
            : _catalog.TryGetPrevious(turret.Definition, out targetPrefab);

        if (!found)
        {
            _view.SetFeedback("No available level change.");
            return;
        }

        var result = upgrade
            ? turret.RequestLevelUpgrade(targetPrefab, out _)
            : turret.RequestLevelDowngrade(targetPrefab, out _);
        _view.SetFeedback(StageTurretUIText.Level(result));

        // The replacement keeps InstanceId; resolve the new component instead of the old reference.
        Refresh(true);
    }

    private bool CanRequest(out FieldTurretBase turret)
    {
        turret = null;
        if (_disposed || !_canInteract()) return false;
        turret = _selection.Resolve();
        return turret != null;
    }

    private void OnSnapshotChanged(int instanceId)
    {
        if (instanceId == _selection.SelectedId) _dirty = true;
    }

    private void Close()
    {
        _selection.Clear();
        Refresh(true);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _view.ToggleRequested -= Toggle;
        _view.LevelUpRequested -= LevelUp;
        _view.LevelDownRequested -= LevelDown;
        _view.CloseRequested -= Close;
        _view.Upgrades.ChangeRequested -= ChangeUpgrade;
        FieldTurretInstanceRegistry.SnapshotChanged -= OnSnapshotChanged;
    }
}
