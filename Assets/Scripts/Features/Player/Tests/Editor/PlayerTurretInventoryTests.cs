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
    [TestCase(.5f, 15f)]
    [TestCase(0f, 30f)]
    public void Return_RepairsInProportionToLostHealth(float healthRatio, float seconds)
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
        Assert.That(inventory.Return(healthRatio), Is.False);
        Assert.That(inventory.Available, Is.EqualTo(1));
    }

    [Test]
    public void MultipleReturns_RepairIndependently()
    {
        var inventory = new PlayerTurretInventory(3, 30);
        inventory.TryDeploy(); inventory.TryDeploy();
        inventory.Return(.5f); inventory.Return(0);
        Assert.That(inventory.Available, Is.EqualTo(1));
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
