using NUnit.Framework;

public sealed class PlayerDashTimerTests
{
    [TestCase(.02f)]
    [TestCase(.016666667f)]
    [TestCase(.033333333f)]
    public void PhysicsStepsConsumeOnlyTheAuthoredDuration(float step)
    {
        var state = new PlayerDashTimer();
        Assert.That(state.TryStart(.16f, 1.2f), Is.True);
        float travelTime = 0;
        int guard = 0;
        while (state.IsDashing && guard++ < 100) travelTime += state.ConsumeStep(step);
        Assert.That(state.IsDashing, Is.False);
        Assert.That(travelTime, Is.EqualTo(.16f).Within(.000001f));
        Assert.That(state.ConsumeStep(step), Is.Zero);
    }

    [Test]
    public void InterruptedDashPreservesCooldownAndDoesNotQueueAnotherDash()
    {
        var state = new PlayerDashTimer();
        Assert.That(state.TryStart(.16f, 1.2f), Is.True);
        Assert.That(state.TryStart(.16f, 1.2f), Is.False);
        state.ConsumeStep(.02f);
        state.Cancel();
        Assert.That(state.IsDashing, Is.False);
        Assert.That(state.TryStart(.16f, 1.2f), Is.False);
        state.TickCooldown(.5f);
        Assert.That(state.CooldownRemaining, Is.EqualTo(.7f).Within(.00001f));
        state.TickCooldown(1);
        Assert.That(state.IsReady, Is.True);
        Assert.That(state.IsDashing, Is.False);
        Assert.That(state.TryStart(.16f, 1.2f), Is.True);
    }

    [Test]
    public void InvalidTimesDoNotStartDashOrCorruptTimers()
    {
        var state = new PlayerDashTimer();
        foreach (float value in new[] { 0, -1, float.NaN, float.PositiveInfinity })
            Assert.That(state.TryStart(value, 1), Is.False);
        Assert.That(state.TryStart(.16f, float.NaN), Is.False);
        Assert.That(state.TryStart(.16f, 1.2f), Is.True);
        state.TickCooldown(float.NaN);
        state.TickCooldown(-1);
        Assert.That(state.CooldownRemaining, Is.EqualTo(1.2f));
        Assert.That(state.ConsumeStep(float.PositiveInfinity), Is.Zero);
        Assert.That(state.Remaining, Is.EqualTo(.16f));
    }
}
