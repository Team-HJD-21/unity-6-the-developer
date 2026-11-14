using NUnit.Framework;

public sealed class PlayerTurretInventoryTests
{
    [Test]
    public void Capacity_PreventsExtraDeployment()
    {
        var inventory = new PlayerTurretInventory(3, 30);
        for (int i = 0; i < 3; i++) Assert.That(inventory.TryDeploy(), Is.True);
        Assert.That(inventory.TryDeploy(), Is.False);
        Assert.That(inventory.Available, Is.Zero);
    }

    [TestCase(1f, 0f)]
    [TestCase(.5f, 0f)]
    [TestCase(0f, 30f)]
    public void Return_PreservesHealthAndOnlyRebuildsDestroyedSlots(float healthRatio, float seconds)
    {
        var inventory = new PlayerTurretInventory(1, 30);
        inventory.TryDeploy();
        Assert.That(inventory.Return(healthRatio), Is.True);
        Assert.That(inventory.RepairRemaining, Is.EqualTo(seconds).Within(.001f));
        if (seconds > 0)
        {
            Assert.That(inventory.Available, Is.Zero);
            inventory.Tick(seconds - .1f);
            Assert.That(inventory.Available, Is.Zero);
            inventory.Tick(.11f);
        }
        Assert.That(inventory.Available, Is.EqualTo(1));
        Assert.That(inventory.GetSlot(0).HealthRatio, Is.EqualTo(healthRatio > 0 ? healthRatio : 1));
        Assert.That(inventory.Return(healthRatio), Is.False);
        Assert.That(inventory.Available, Is.EqualTo(1));
    }

    [Test]
    public void NumberedSlotsKeepIndependentHealthAcrossRepeatedDeploymentsAndSnapshots()
    {
        var inventory = new PlayerTurretInventory(3, 30);
        Assert.That(inventory.TryDeploy(2), Is.True);
        Assert.That(inventory.TryDeploy(0), Is.True);
        Assert.That(inventory.Return(2, .35f), Is.True);
        Assert.That(inventory.Return(0, .8f), Is.True);
        var saved = inventory.CaptureState();
        inventory.TryDeploy(2); inventory.Return(2, 0); inventory.Tick(30);
        inventory.RestoreState(saved);
        Assert.That(inventory.GetSlot(0).HealthRatio, Is.EqualTo(.8f));
        Assert.That(inventory.GetSlot(1).HealthRatio, Is.EqualTo(1));
        Assert.That(inventory.GetSlot(2).HealthRatio, Is.EqualTo(.35f));
        Assert.That(inventory.TryDeploy(2), Is.True);
        Assert.That(inventory.TryDeploy(2), Is.False);
        Assert.That(inventory.UpdateHealth(2, .65f), Is.True);
        Assert.That(inventory.GetSlot(2).HealthRatio, Is.EqualTo(.65f));
    }

    [Test]
    public void SnapshotRejectsImpossibleSlotStatesAndInvalidReturnsDoNotReleaseSlots()
    {
        var inventory = new PlayerTurretInventory(3, 30);
        inventory.TryDeploy(1);
        Assert.That(inventory.Return(1, float.NaN), Is.False);
        Assert.That(inventory.GetSlot(1).Status, Is.EqualTo(PlayerTurretInventory.SlotStatus.Deployed));
        Assert.Throws<System.ArgumentException>(() => new PlayerTurretInventory.State(1, 30,
            new[] { new PlayerTurretInventory.Slot(PlayerTurretInventory.SlotStatus.Available, 0) }));
        Assert.Throws<System.ArgumentException>(() => new PlayerTurretInventory.State(1, 30,
            new[] { new PlayerTurretInventory.Slot(PlayerTurretInventory.SlotStatus.Rebuilding, .5f, 10) }));
    }

    [Test]
    public void MultipleReturns_RepairIndependently()
    {
        var inventory = new PlayerTurretInventory(3, 30);
        inventory.TryDeploy(); inventory.TryDeploy();
        inventory.Return(.5f); inventory.Return(0);
        Assert.That(inventory.Available, Is.EqualTo(2));
        inventory.Tick(15);
        Assert.That(inventory.Available, Is.EqualTo(2));
        Assert.That(inventory.RepairRemaining, Is.EqualTo(15).Within(.001f));
        inventory.Tick(15);
        Assert.That(inventory.Available, Is.EqualTo(3));
    }

    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1f)]
    public void InvalidTime_DoesNotFinishRepair(float elapsed)
    {
        var inventory = new PlayerTurretInventory(1, 30);
        inventory.TryDeploy(); inventory.Return(0); inventory.Tick(elapsed);
        Assert.That(inventory.Available, Is.Zero);
        Assert.That(inventory.RepairRemaining, Is.EqualTo(30).Within(.001f));
    }
}
