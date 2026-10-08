using NUnit.Framework;

public sealed class PlayerSkillChargeTests
{
    [Test]
    public void NewInventory_StartsWithThreeCharges() => Assert.That(new PlayerSkillCharges(3, 18).Current, Is.EqualTo(3));
    [Test]
    public void Consumption_CannotExceedInventory()
    {
        var charges = new PlayerSkillCharges(3, 18);
        for (int i = 0; i < 3; i++) Assert.That(charges.TryConsume(), Is.True);
        Assert.That(charges.TryConsume(), Is.False);
    }
    [Test]
    public void SecondConsumption_DoesNotRestartRecharge()
    {
        var charges = new PlayerSkillCharges(3, 18);
        charges.TryConsume(); charges.Tick(5); charges.TryConsume();
        Assert.That(charges.Remaining, Is.EqualTo(13).Within(.001f));
    }
    [Test]
    public void Recharge_RestoresOneChargePerInterval()
    {
        var charges = new PlayerSkillCharges(3, 18);
        charges.TryConsume(); charges.TryConsume(); charges.Tick(18);
        Assert.That(charges.Current, Is.EqualTo(2));
        Assert.That(charges.Remaining, Is.EqualTo(18).Within(.001f));
    }
    [Test]
    public void LongRecharge_IsCappedAtMaximum()
    {
        var charges = new PlayerSkillCharges(3, 18);
        charges.TryConsume(); charges.TryConsume(); charges.Tick(180);
        Assert.That(charges.Current, Is.EqualTo(3));
        Assert.That(charges.Remaining, Is.Zero);
    }
    [TestCase(float.NaN)]
    [TestCase(float.PositiveInfinity)]
    [TestCase(-1)]
    public void InvalidElapsed_DoesNotChangeRecharge(float elapsed)
    {
        var charges = new PlayerSkillCharges(3, 18); charges.TryConsume(); charges.Tick(elapsed);
        Assert.That(charges.Remaining, Is.EqualTo(18).Within(.001f));
    }
}