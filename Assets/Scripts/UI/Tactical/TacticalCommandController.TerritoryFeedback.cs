// 확정 점유 변경을 Field 연출과 기존 완료 Alert에 연결합니다.
// 지도와 게임 판정은 최신 Snapshot을 유지하며 표시 구독만 관리합니다.
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation;
using UnityEngine;

public sealed partial class TacticalCommandController
{
    private IRegionGridSource _territorySource;
    private FieldTerritoryRenderer _territoryRenderer;
    private readonly TerritoryCaptureNotices _territoryNotices = new();

    private void RefreshTerritoryFeedback()
    {
        if (_territoryRenderer == null) return;
        var source = Source.Turrets?.Regions;
        if (!ReferenceEquals(source, _territorySource))
        {
            StopTerritoryFeedback(); _territorySource = source;
            if (_territorySource != null)
            {
                _territorySource.Changed += OnTerritoryChanged;
                OnTerritoryChanged(_territorySource.Snapshot);
            }
        }
        double now = Time.unscaledTimeAsDouble;
        _territoryRenderer.Presentation.Tick(now);
        var snapshot = _territoryRenderer.Presentation.Snapshot;
        _territoryNotices.Align(_territoryRenderer.Presentation.SweepEndsAt, Style.CaptureToastFreshSeconds);
        if (!Source.HasFieldActor || _player == null || _player.curHp <= 0) { _territoryNotices.Clear(); return; }
        if (!_territoryNotices.Ready(snapshot, now) || !IsHudEntryComplete || IsTransitioning || HudPresentationVisibility.IsSuppressed ||
            (_app != null && _app.IsInitialized && _app.LocalInput.IsBlocked)) return;
        if (GameAlerts.TryShowIfIdle("점유 완료", AlertTone.Success, _territoryNotices.Key(snapshot))) _territoryNotices.Clear();
    }

    private void OnTerritoryChanged(RegionGridSnapshot snapshot)
    {
        if (_territoryRenderer == null) return;
        if (snapshot != null && Source.RequiresCommands && snapshot.MatchId != Source.BoundMatchId) return;
        double now = Time.unscaledTimeAsDouble;
        var receipt = _territoryRenderer.Present(snapshot, now);
        if (receipt == TerritoryExpansionState.Receipt.Ignored) return;
        _territoryNotices.Observe(snapshot, receipt, now, _territoryRenderer.Presentation.SweepEndsAt,
            Style.CaptureMergeSeconds, Style.CaptureToastFreshSeconds);
        // 지도·상단 수치는 확정값을 즉시 읽고 바닥의 중간 표시를 판정에 사용하지 않습니다.
        if (View != null && WorldBounds.width > 0 && WorldBounds.height > 0) RefreshRegionRaster();
    }

    private void StopTerritoryFeedback()
    {
        if (_territorySource != null) _territorySource.Changed -= OnTerritoryChanged;
        _territorySource = null; _territoryNotices.Clear();
        _sampledRegions = null;
        if (_territoryRenderer != null) _territoryRenderer.Presentation.Reset(null);
    }
}
