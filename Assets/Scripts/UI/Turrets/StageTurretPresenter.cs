// 선택한 논리 대상의 Host 확정 상태를 표시하고 입력을 명령 Port에 전달합니다.
// 정식 Match는 Port 미연결/수신 대기를 로컬 D 호출로 우회하지 않습니다.
using System;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Contracts;

/// <summary>선택 ID를 유지하고 UI 입력을 기존 터렛 API에 전달합니다.</summary>
internal sealed class StageTurretPresenter : IDisposable
{
    private readonly StageTurretSelection _selection;
    private readonly StageTurretControlView _view;
    private readonly FieldTurretLevelUpgradeCatalog _catalog;
    private readonly Func<bool> _canInteract;
    private readonly Func<FieldTurretController> _getController;
    private readonly Func<StageTurretCommandBinding> _getCommands;
    private readonly Func<bool> _requiresCommands;
    private long _displayedRevision = -1;
    private bool _displayedPending;
    private int _displayedId;
    private bool _displayedPermission;
    private bool _dirty = true;
    private bool _disposed;

    public StageTurretPresenter(StageTurretSelection selection, StageTurretControlView view,
        FieldTurretLevelUpgradeCatalog catalog, Func<bool> canInteract, Func<FieldTurretController> getController,
        Func<StageTurretCommandBinding> getCommands = null, Func<bool> requiresCommands = null)
    {
        _selection = selection;
        _view = view;
        _catalog = catalog;
        _canInteract = canInteract;
        _getController = getController;
        _getCommands = getCommands ?? (() => null); _requiresCommands = requiresCommands ?? (() => false);

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
        bool withinReach = _selection.CanInteractWith(turret);
        bool canInteract = _canInteract() && withinReach;
        if (_requiresCommands()) { RefreshAuthority(turret, canInteract, refreshDisplay); return; }
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
            _getController()?.Regions?.Snapshot.Mode == RegionPolicyMode.Dynamic, withinReach);
    }

    private void Toggle()
    {
        if (!CanRequest(out FieldTurretBase turret)) return;
        if (_requiresCommands())
        {
            var state = ResolveSnapshot(turret);
            if (StageTurretInteractionRules.CanToggle(state)) Submit(TurretCommandKind.SetActivation, state, !state.IsActivated);
            return;
        }
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
        if (_requiresCommands())
        {
            var state = ResolveSnapshot(turret);
            if (StageTurretInteractionRules.CanChangeUpgrade(state, upgrade, increase))
                Submit(increase ? TurretCommandKind.ApplyUpgrade : TurretCommandKind.DowngradeUpgrade, state, upgradeId: new DefinitionId(upgrade.Id));
            return;
        }
        if (!StageTurretInteractionRules.CanChangeUpgrade(turret, upgrade, increase)) return;
        var result = increase ? turret.ApplyUpgrade(upgrade) : turret.DowngradeUpgrade(upgrade);
        _view.SetFeedback(StageTurretUIText.Upgrade(result));
        Refresh(true);
    }

    private void ChangeLevel(bool upgrade)
    {
        if (!CanRequest(out FieldTurretBase turret)) return;
        if (_requiresCommands())
        {
            var state = ResolveSnapshot(turret);
            if (state != null && state.CanModifyUpgrades) Submit(upgrade ? TurretCommandKind.UpgradeLevel : TurretCommandKind.DowngradeLevel, state);
            return;
        }
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
        if (turret == null) return false;
        if (_selection.CanInteractWith(turret)) return true;

        Refresh(true);
        return false;
    }

    private TurretPresentationSnapshot ResolveSnapshot(FieldTurretBase turret)
    {
        var frame = _getCommands()?.Presentation.Snapshot;
        return turret != null && frame != null && turret.TryGetEntityId(out var id) && frame.TryGet(id, out var state) ? state : null;
    }
    private void RefreshAuthority(FieldTurretBase turret, bool canInteract, bool refreshDisplay)
    {
        if (turret == null) { _view.Hide(); _displayedId = 0; return; }
        var commands = _getCommands(); var frame = commands?.Presentation.Snapshot;
        var state = ResolveSnapshot(turret); long revision = frame?.Revision ?? 0;
        bool pending = state != null && commands.HasPending(state.EntityId);
        bool selectionChanged = _displayedId != _selection.SelectedId;
        if (!selectionChanged && !_dirty && !refreshDisplay && _displayedRevision == revision &&
            _displayedPermission == canInteract && _displayedPending == pending) return;
        if (selectionChanged) _view.SetFeedback("");
        _displayedId = _selection.SelectedId; _displayedRevision = revision; _displayedPermission = canInteract; _displayedPending = pending; _dirty = false;
        if (state == null) { _view.ShowWaitingForHost(); return; }
        bool known = _catalog.TryGetDefinition(state.DefinitionId.Value, out var definition);
        bool up = known && _catalog.TryGetNext(definition, out _), down = known && _catalog.TryGetPrevious(definition, out _);
        _view.ShowSnapshot(state, canInteract, up, down, frame.Controller.RegionMode == RegionPolicyMode.Dynamic,
            pending, _selection.CanInteractWith(turret));
    }
    private void Submit(TurretCommandKind kind, TurretPresentationSnapshot state, bool activate = false, DefinitionId upgradeId = default)
    {
        var commands = _getCommands();
        _view.SetFeedback("Waiting for Host confirmation/state...");
        if (commands == null || !commands.TrySubmit(kind, state.EntityId, activate, upgradeId)) _view.SetFeedback("Request unavailable or already pending.");
        Refresh(true);
    }
    public void OnCommandResult(TurretCommandResult result)
    {
        FieldTurretBase turret = _selection.Resolve();
        if (turret != null && turret.TryGetEntityId(out var id) && id == result.TargetId) _view.SetFeedback(StageTurretUIText.Command(result));
        Refresh(true);
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
