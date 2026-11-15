// 게임 확정값과 표시 전환·완료 통지의 경계 검사입니다. Scene/Prefab을 저장하지 않습니다.
using System;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Domain;
using UnityEngine;

public sealed class TerritoryExpansionTests
{
    private readonly MatchId _match = new("territory-feedback-test");
    private readonly Guid _grid = Guid.NewGuid();
    private static RegionGridDefinition Definition(int columns = 4, int rows = 1, double cellWidth = 10, double cellHeight = 10)
        => new(new BattlefieldGridConfiguration(new BattlefieldPoint(0, 0), 0, columns * cellWidth, rows * cellHeight, columns, rows),
            Enumerable.Range(1, columns * rows));
    private RegionGridSnapshot Frame(RegionGridDefinition definition, long revision, int[] cells,
        RegionChangeKind kind = RegionChangeKind.Baseline, long previous = -1, int secured = 2,
        long? changeRevision = null, Guid? grid = null, RegionPolicyMode mode = RegionPolicyMode.Static)
        => RegionGridSnapshot.FromCapturedCells(_match, grid ?? _grid, revision, mode, definition, cells, null,
            kind == RegionChangeKind.Baseline ? RegionGridChange.Baseline(revision) :
            new RegionGridChange(kind, previous, changeRevision ?? revision, kind == RegionChangeKind.StaticSecured ? new[] { secured } : null));
    private static int Pixel(TerritoryExpansionState state, Vector2 world)
    {
        int x = Mathf.Clamp((int)((world.x - state.Bounds.xMin) / state.Bounds.width * state.Width), 0, state.Width - 1);
        int y = Mathf.Clamp((int)((world.y - state.Bounds.yMin) / state.Bounds.height * state.Height), 0, state.Height - 1);
        return y * state.Width + x;
    }

    [Test]
    public void ConfirmedGridIsImmediateButTheGroundFrontTravelsBeforeSettling()
    {
        var d = Definition(); var state = new TerritoryExpansionState();
        var before = Frame(d, 0, new[] { 0 }); state.Accept(before, 0);
        var after = Frame(d, 1, new[] { 0, 1, 2, 3 }, RegionChangeKind.StaticSecured, 0);
        Assert.That(state.Accept(after, 0), Is.EqualTo(TerritoryExpansionState.Receipt.Capture));
        Assert.That(state.Snapshot, Is.SameAs(after)); Assert.That(after.CapturedCellCount, Is.EqualTo(4));
        int near = Pixel(state, new Vector2(-5, 0)), far = Pixel(state, new Vector2(15, 0));
        Assert.That(state.DisplayedAtPixel(near, 0), Is.LessThan(0));
        Assert.That(state.DisplayedAtPixel(near, .4), Is.GreaterThan(0));
        Assert.That(state.DisplayedAtPixel(far, .4), Is.LessThan(0));
        Assert.That(state.DisplayedAtPixel(far, .7), Is.GreaterThan(0));
        Assert.That(state.Blend(.7), Is.Zero); Assert.That(state.Glow(.7), Is.EqualTo(1));
        Assert.That(state.Blend(1.06), Is.EqualTo(1)); Assert.That(state.Animating(1.06), Is.False);
        Assert.That(before.CapturedCellCount, Is.EqualTo(1), "표시 진행은 과거/최신 Snapshot을 변경하지 않습니다.");
    }

    [Test]
    public void CaptureThenUnlockKeepsTheSameNoticeAndDoesNotRestartTheWave()
    {
        var d = Definition(); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0 }), 0);
        state.Accept(Frame(d, 1, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0), 1);
        int version = state.Version; double end = state.EndsAt;
        var retained = Frame(d, 2, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0, changeRevision: 1);
        Assert.That(state.Accept(retained, 1.1), Is.EqualTo(TerritoryExpansionState.Receipt.Metadata));
        Assert.That(state.Snapshot.Revision, Is.EqualTo(2)); Assert.That(state.Version, Is.EqualTo(version));
        Assert.That(state.EndsAt, Is.EqualTo(end));
        Assert.That(state.Accept(retained, 1.2), Is.EqualTo(TerritoryExpansionState.Receipt.Ignored));
    }

    [Test]
    public void RetargetingDoesNotHideGroundAlreadyRevealedByThePreviousWave()
    {
        var d = Definition(6); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0 }), 0);
        state.Accept(Frame(d, 1, new[] { 0, 1, 2 }, RegionChangeKind.StaticSecured, 0), 0);
        var displayed = Enumerable.Range(0, state.Pixels.Length).Select(i => state.DisplayedAtPixel(i, .31)).ToArray();
        state.Accept(Frame(d, 2, new[] { 0, 1, 2, 3, 4 }, RegionChangeKind.StaticSecured, 1, secured: 4), .31);
        Assert.That(state.Pixels.Length, Is.EqualTo(displayed.Length));
        for (int i = 0; i < displayed.Length; i++)
            Assert.That(state.DisplayedAtPixel(i, .31), Is.GreaterThanOrEqualTo(displayed[i] - .0001f), "pixel=" + i);
    }

    [Test]
    public void BurstCapturesCoalesceWithoutDelayingTheConfirmedSnapshot()
    {
        var d = Definition(); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0 }), 0);
        state.Accept(Frame(d, 1, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0), 0);
        int version = state.Version;
        state.Accept(Frame(d, 2, new[] { 0, 1, 2 }, RegionChangeKind.StaticSecured, 1, secured: 3), .08);
        Assert.That(state.Snapshot.CapturedCellCount, Is.EqualTo(3)); Assert.That(state.HasPending, Is.True);
        Assert.That(state.Version, Is.EqualTo(version)); state.Tick(.2);
        Assert.That(state.HasPending, Is.False); Assert.That(state.Version, Is.GreaterThan(version));
        Assert.That(state.SweepEndsAt, Is.EqualTo(.85).Within(.00001));
    }

    [Test]
    public void RetargetDuringSettleDoesNotFlashPreviouslyCapturedGroundAgain()
    {
        var d = Definition(); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0 }), 0);
        state.Accept(Frame(d, 1, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0), 0);
        float previousGlow = state.Glow(.9);
        state.Accept(Frame(d, 2, new[] { 0, 1, 2 }, RegionChangeKind.StaticSecured, 1, secured: 3), .9);
        float carried = state.Pixels[Pixel(state, new Vector2(-5, 0))].a;
        Assert.That(carried, Is.LessThan(0)); Assert.That(-carried, Is.EqualTo(previousGlow).Within(.0001));
        Assert.That(state.Pixels[Pixel(state, new Vector2(5, 0))].a, Is.EqualTo(1));
    }

    [Test]
    public void TheFarOuterRimDoesNotAppearBeforeTheTravellingFrontArrives()
    {
        var d = Definition(6); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0 }), 0);
        state.Accept(Frame(d, 1, new[] { 0, 1, 2 }, RegionChangeKind.StaticSecured, 0), 0);
        int outside = Pixel(state, new Vector2(.625f, 0));
        Assert.That(state.DisplayedAtPixel(outside, .3), Is.LessThan(-state.BandWidth));
        Assert.That(state.DisplayedAtPixel(outside, .7), Is.GreaterThan(-state.BandWidth));
    }

    [Test]
    public void LossCancelsTheGlowAndSettlesOnTheRemainingConfirmedCells()
    {
        var d = Definition(); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0 }), 0);
        state.Accept(Frame(d, 1, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0), 0);
        state.Accept(Frame(d, 2, new[] { 0 }, RegionChangeKind.StaticLost, 1), .4);
        Assert.That(state.IsLoss, Is.True); Assert.That(state.Glow(.41), Is.Zero);
        Assert.That(state.Animating(.57), Is.False);
        Assert.That(state.DisplayedAtPixel(Pixel(state, new Vector2(-5, 0)), .57), Is.LessThan(0));
        Assert.That(state.Snapshot.CapturedCellCount, Is.EqualTo(1));
    }

    [Test]
    public void EqualCountsStillHandleRemovedAndAddedCellsSeparately()
    {
        var d = Definition(); var state = new TerritoryExpansionState();
        state.Accept(Frame(d, 0, new[] { 0, 1 }, mode: RegionPolicyMode.Dynamic), 0);
        state.Accept(Frame(d, 1, new[] { 1, 2 }, RegionChangeKind.DynamicCapture, 0, mode: RegionPolicyMode.Dynamic), 0);
        Assert.That(state.Animating(.1), Is.True); Assert.That(state.IsLoss, Is.False);
        Assert.That(state.DisplayedAtPixel(Pixel(state, new Vector2(-15, 0)), .01), Is.LessThan(0));
        Assert.That(state.Snapshot.CapturedCellCount, Is.EqualTo(2));
    }

    [Test]
    public void BaselineRestoreNewGridAndLateJoinNeverReplayHistoricalCapture()
    {
        var d = Definition(); var state = new TerritoryExpansionState();
        var late = Frame(d, 9, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 7, changeRevision: 8);
        Assert.That(state.Accept(late, 0), Is.EqualTo(TerritoryExpansionState.Receipt.Baseline));
        Assert.That(state.Pixels, Is.Null);
        state.Accept(Frame(d, 10, new[] { 0, 1, 2 }, RegionChangeKind.StaticSecured, 9, secured: 3), 0);
        Assert.That(state.Animating(.1), Is.True);
        Assert.That(state.Accept(Frame(d, 11, new[] { 0, 1 }), .1), Is.EqualTo(TerritoryExpansionState.Receipt.Baseline));
        Assert.That(state.Pixels, Is.Null);
        Assert.That(state.Accept(Frame(d, 1, new[] { 0 }, grid: Guid.NewGuid()), .2), Is.EqualTo(TerritoryExpansionState.Receipt.Baseline));
        Assert.That(state.Pixels, Is.Null);
    }

    [Test]
    public void CoalescedLiveRevisionsAnimateButOnlyTheReportedCompletionCanNotify()
    {
        var d = Definition(); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0 }), 0);
        var gap = Frame(d, 6, new[] { 0, 1, 2 }, RegionChangeKind.StaticSecured, 5);
        var receipt = state.Accept(gap, 0);
        Assert.That(receipt, Is.EqualTo(TerritoryExpansionState.Receipt.Capture));
        Assert.That(state.Snapshot, Is.SameAs(gap)); Assert.That(state.Pixels, Is.Not.Null);
        var notices = new TerritoryCaptureNotices(); notices.Observe(gap, receipt, 0, state.SweepEndsAt, .18f, 1);
        Assert.That(notices.Count, Is.EqualTo(1), "셀 증가를 보고 다른 구역의 확보 완료를 추측하지 않습니다.");
    }

    [Test]
    public void EquivalentReplicatedDefinitionsDoNotTurnLiveUpdatesIntoBaselines()
    {
        var state = new TerritoryExpansionState(); state.Accept(Frame(Definition(), 0, new[] { 0 }), 0);
        Assert.That(state.Accept(Frame(Definition(), 1, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0), 0),
            Is.EqualTo(TerritoryExpansionState.Receipt.Capture));
        Assert.That(state.Pixels, Is.Not.Null);
    }

    [Test]
    public void ConfirmedCompletionCanNotifyWithoutInventingANewGroundArea()
    {
        var d = Definition(); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0, 1 }), 0);
        var confirmed = Frame(d, 1, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0);
        var receipt = state.Accept(confirmed, 0); var notices = new TerritoryCaptureNotices();
        notices.Observe(confirmed, receipt, 0, state.SweepEndsAt, .18f, 1);
        Assert.That(receipt, Is.EqualTo(TerritoryExpansionState.Receipt.Capture)); Assert.That(state.Pixels, Is.Null);
        Assert.That(notices.Ready(confirmed, .2), Is.True);
    }

    [Test]
    public void ExtremelyThinLargeGridsKeepExactConfirmedStateWithoutOversizedTextures()
    {
        var d = Definition(513); var state = new TerritoryExpansionState(); state.Accept(Frame(d, 0, new[] { 0 }), 0);
        var confirmed = Frame(d, 1, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0);
        Assert.That(state.Accept(confirmed, 0), Is.EqualTo(TerritoryExpansionState.Receipt.Capture));
        Assert.That(state.Snapshot, Is.SameAs(confirmed)); Assert.That(state.Pixels, Is.Null);
    }

    [Test]
    public void SignedDistancesMatchIndependentBruteForceForRectangularPixelsAndHoles()
    {
        const int width = 4, height = 3; const float dx = 3, dy = .75f;
        for (int pattern = 0; pattern < 256; pattern++)
        {
            var mask = Enumerable.Range(0, width * height).Select(i => (pattern & (1 << (i % 8))) != 0).ToArray();
            var distance = TerritoryExpansionState.SignedDistance(mask, width, height, dx, dy);
            for (int i = 0; i < mask.Length; i++)
            {
                float best = (width * dx + height * dy) * 2;
                for (int j = 0; j < mask.Length; j++)
                    if (mask[i] != mask[j]) best = Mathf.Min(best, new Vector2((i % width - j % width) * dx, (i / width - j / width) * dy).magnitude);
                float expected = (best - Math.Min(dx, dy) * .5f) * (mask[i] ? 1 : -1);
                Assert.That(distance[i], Is.EqualTo(expected).Within(.0001), $"mask={pattern},pixel={i}");
            }
        }
    }

    [Test]
    public void ArrivalUsesWorldDistanceAndDoesNotCrossUncapturedHoles()
    {
        var target = new[] { true, true, true, false, false, true };
        var visible = new[] { true, false, false, false, false, false };
        var values = TerritoryExpansionState.ArrivalDistances(target, visible, 3, 2, 20, 5);
        Assert.That(values[1], Is.EqualTo(20)); Assert.That(values[2], Is.EqualTo(40)); Assert.That(values[5], Is.EqualTo(45));
        Assert.That(values[4], Is.GreaterThan(0), "외부 보간값을 0으로 리셋하여 먼 붉은 외곽이 먼저 뜨면 안 됩니다.");
        values = TerritoryExpansionState.ArrivalDistances(new[] { true, false, false, false, false, true }, visible, 3, 2, 20, 5);
        Assert.That(values[5], Is.Zero, "분리된 섬에는 별도 시작점이 있으며 미점유 셀을 통과하지 않습니다.");
    }

    [Test]
    public void SuccessNoticesUseTheCaptureRevisionAndExpireWithoutEnteringAnAlertQueue()
    {
        var d = Definition(); var notices = new TerritoryCaptureNotices();
        var capture = Frame(d, 2, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0, changeRevision: 1);
        notices.Observe(capture, TerritoryExpansionState.Receipt.Capture, 0, .65, .18f, 1);
        Assert.That(notices.Key(capture), Does.EndWith("/1")); Assert.That(notices.Ready(capture, .5), Is.False);
        Assert.That(notices.Ready(capture, .7), Is.True);
        notices.Observe(Frame(d, 3, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0, changeRevision: 1),
            TerritoryExpansionState.Receipt.Metadata, .8, .65, .18f, 1);
        Assert.That(notices.Count, Is.EqualTo(1)); Assert.That(notices.Ready(capture, 1.7), Is.False);
        Assert.That(notices.Count, Is.Zero, "사용할 수 없었던 성공 메시지를 늦게 큐에 재등록하지 않습니다.");
    }

    [Test]
    public void LossAndBaselineCancelPendingSuccessWhileReacquisitionHasANewKey()
    {
        var d = Definition(); var notices = new TerritoryCaptureNotices();
        var capture = Frame(d, 1, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 0);
        notices.Observe(capture, TerritoryExpansionState.Receipt.Capture, 0, .65, .18f, 1);
        notices.Observe(Frame(d, 2, new[] { 0 }, RegionChangeKind.StaticLost, 1), TerritoryExpansionState.Receipt.Capture, .2, .36, .18f, 1);
        Assert.That(notices.Count, Is.Zero);
        var again = Frame(d, 3, new[] { 0, 1 }, RegionChangeKind.StaticSecured, 2);
        notices.Observe(again, TerritoryExpansionState.Receipt.Capture, .3, .95, .18f, 1);
        Assert.That(notices.Key(again), Does.EndWith("/3")); Assert.That(notices.Ready(again, 1), Is.True);
        notices.Observe(Frame(d, 4, new[] { 0, 1 }), TerritoryExpansionState.Receipt.Baseline, 1, 0, .18f, 1);
        Assert.That(notices.Count, Is.Zero);
    }
}
