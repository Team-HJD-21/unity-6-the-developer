using System;
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;

/// <summary>선택 ID를 유지하고 UI 입력을 기존 터렛 API에 전달합니다.</summary>
internal sealed class StageTurretPresenter : IDisposable
{
    private readonly StageTurretSelection _selection;
    private readonly StageTurretControlView _view;
    private readonly TurretLevelUpgradeCatalog _catalog;
    private int _displayedId;
    public StageTurretPresenter(StageTurretSelection selection, StageTurretControlView view, TurretLevelUpgradeCatalog catalog)
    {
        _selection = selection;
        _view = view;
        _catalog = catalog;
        _view.ToggleRequested += Toggle;
        _view.LevelUpRequested += LevelUp;
        _view.LevelDownRequested += LevelDown;
        _view.CloseRequested += Close;
    }

    public void Refresh()
    {
        var turret = _selection.Resolve();
        if (_displayedId != _selection.SelectedId)
        {
            _displayedId = _selection.SelectedId;
            _view.SetFeedback("");
        }
        bool canUp = turret != null && _catalog != null && _catalog.TryGetNext(turret.Definition, out _);
        bool canDown = turret != null && _catalog != null && _catalog.TryGetPrevious(turret.Definition, out _);
        _view.Show(turret, canUp, canDown);
    }

    private void Toggle()
    {
        var turret = _selection.Resolve();
        if (turret == null) return;
        _view.SetFeedback(StageTurretUiText.Activation(turret.RequestActivation(!turret.IsActivated)));
        Refresh();
    }

    private void LevelUp() => ChangeLevel(true);
    private void LevelDown() => ChangeLevel(false);
    private void ChangeLevel(bool upgrade)
    {
        var turret = _selection.Resolve();
        if (turret == null || _catalog == null) return;
        TurretBase prefab;
        bool found = upgrade ? _catalog.TryGetNext(turret.Definition, out prefab) : _catalog.TryGetPrevious(turret.Definition, out prefab);
        if (!found) { _view.SetFeedback("No available level change."); return; }
        TurretLevelUpgradeResult result = upgrade
            ? turret.RequestLevelUpgrade(prefab, out _)
            : turret.RequestLevelDowngrade(prefab, out _);
        _view.SetFeedback(StageTurretUiText.Level(result));
        // The API transfers InstanceId to the replacement; re-resolve instead of retaining the old component.
        Refresh();
    }

    private void Close() { _selection.Clear(); Refresh(); }
    public void Dispose()
    {
        _view.ToggleRequested -= Toggle;
        _view.LevelUpRequested -= LevelUp;
        _view.LevelDownRequested -= LevelDown;
        _view.CloseRequested -= Close;
    }
}
