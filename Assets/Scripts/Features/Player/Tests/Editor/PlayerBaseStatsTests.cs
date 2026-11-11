using System.IO;
using NUnit.Framework;

public sealed class PlayerBaseStatsTests
{
    [Test]
    public void ReachingALevelDoesNotGrantPointsUntilItsRewardIsClaimed()
    {
        var state = new PlayerBaseStatsState(100, 50);
        Assert.That(state.AvailablePoints, Is.Zero);
        Assert.That(state.TryClaim(1, 10), Is.False);
        Assert.That(state.TryClaim(3, 2), Is.False);
        Assert.That(state.TryClaim(2, 2), Is.True);
        Assert.That(state.AvailablePoints, Is.EqualTo(1));
        Assert.That(state.TryClaim(2, 3), Is.False);
        Assert.That(state.AvailablePoints, Is.EqualTo(1));
    }

    [Test]
    public void LevelJumpLeavesEachAchievedRewardAvailableOnce()
    {
        var state = new PlayerBaseStatsState(100, 50);
        Assert.That(state.TryClaim(5, 5), Is.True);
        Assert.That(state.TryClaim(2, 5), Is.True);
        Assert.That(state.TryClaim(4, 5), Is.True);
        Assert.That(state.TryClaim(3, 5), Is.True);
        Assert.That(state.AvailablePoints, Is.EqualTo(4));
        Assert.That(state.TryClaim(6, 5), Is.False);
        Assert.That(state.TryClaim(101, 1000), Is.False);
    }

    [Test]
    public void FreeRefundReturnsOnlyTheInvestedPointAndDoesNotResetClaims()
    {
        var state = new PlayerBaseStatsState(100, 50);
        Assert.That(state.TryInvest(PlayerBaseStat.Attack), Is.False);
        state.TryClaim(2, 2);
        Assert.That(state.TryInvest(PlayerBaseStat.Attack), Is.True);
        Assert.That(state.AvailablePoints, Is.Zero);
        Assert.That(state.TryRefund(PlayerBaseStat.Movement), Is.False);
        Assert.That(state.TryRefund(PlayerBaseStat.Attack), Is.True);
        Assert.That(state.TryRefund(PlayerBaseStat.Attack), Is.False);
        Assert.That(state.AvailablePoints, Is.EqualTo(1));
        Assert.That(state.TryClaim(2, 2), Is.False);
    }

    [Test]
    public void CriticalCapStopsInvestmentWithoutSpendingTheExtraPoint()
    {
        var state = new PlayerBaseStatsState(100, 50);
        for (int level = 2; level <= 52; level++) state.TryClaim(level, 52);
        for (int i = 0; i < 50; i++) Assert.That(state.TryInvest(PlayerBaseStat.CriticalChance), Is.True);
        Assert.That(state.TryInvest(PlayerBaseStat.CriticalChance), Is.False);
        Assert.That(state.AvailablePoints, Is.EqualTo(1));
        Assert.That(state.TryInvest(PlayerBaseStat.Attack), Is.True);
    }

    [Test]
    public void ResetReturnsAllInvestmentsAndPreservesEveryRewardClaim()
    {
        var state = new PlayerBaseStatsState(100, 50);
        state.TryClaim(2, 4); state.TryClaim(3, 4); state.TryClaim(4, 4);
        state.TryInvest(PlayerBaseStat.Attack); state.TryInvest(PlayerBaseStat.Movement); state.TryInvest(PlayerBaseStat.CriticalChance);
        Assert.That(state.ResetInvestments(), Is.True);
        Assert.That(state.AvailablePoints, Is.EqualTo(3));
        Assert.That(state.ResetInvestments(), Is.False);
        Assert.That(state.TryClaim(2, 4), Is.False);
    }

    [Test]
    public void SnapshotRestoresInvestmentsAndRejectsDuplicateOrOverspentRewardsAtomically()
    {
        var original = new PlayerBaseStatsState(100, 50);
        original.TryClaim(2, 3); original.TryClaim(3, 3);
        original.TryInvest(PlayerBaseStat.Attack);
        var restored = new PlayerBaseStatsState(100, 50);
        restored.Restore(original.Capture());
        Assert.That(restored.AvailablePoints, Is.EqualTo(1));
        Assert.That(restored.Invested(PlayerBaseStat.Attack), Is.EqualTo(1));
        Assert.That(restored.IsClaimed(3), Is.True);
        Assert.Throws<InvalidDataException>(() => restored.Restore(new(0, 0, 0, new[] { 2, 2 })));
        Assert.Throws<InvalidDataException>(() => restored.Restore(new(3, 0, 0, new[] { 2, 3 })));
        Assert.Throws<InvalidDataException>(() => restored.Restore(new(0, 0, 0, new[] { 1 })));
        Assert.That(restored.AvailablePoints, Is.EqualTo(1));
        Assert.That(restored.Invested(PlayerBaseStat.Attack), Is.EqualTo(1));
    }
}
