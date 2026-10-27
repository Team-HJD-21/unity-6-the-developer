using NUnit.Framework;

public sealed class PlayerAttackGestureTests
{
    [Test]
    public void AttackRequiresFreshPressAndStopsOnRelease()
    {
        var gesture = new PlayerAttackGesture();
        Assert.That(gesture.Read(true, false, false), Is.False);
        Assert.That(gesture.Read(true, true, false), Is.True);
        Assert.That(gesture.Read(true, false, false), Is.True);
        Assert.That(gesture.Read(false, false, false), Is.False);
        Assert.That(gesture.Read(true, false, false), Is.False);
    }

    [Test]
    public void BlockedClickCannotBecomeAttackByDraggingAwayOrClosingMenu()
    {
        var gesture = new PlayerAttackGesture();
        Assert.That(gesture.Read(true, true, true), Is.False);
        Assert.That(gesture.Read(true, false, false), Is.False);
        Assert.That(gesture.Read(false, false, false), Is.False);
        Assert.That(gesture.Read(true, true, false), Is.True);
    }

    [Test]
    public void BlockingAnActiveAttackOrDisablingOwnerRequiresAnotherPress()
    {
        var gesture = new PlayerAttackGesture();
        Assert.That(gesture.Read(true, true, false), Is.True);
        Assert.That(gesture.Read(true, false, true), Is.False);
        Assert.That(gesture.Read(true, false, false), Is.False);
        gesture.Read(false, false, false);
        Assert.That(gesture.Read(true, true, false), Is.True);
        gesture.Reset();
        Assert.That(gesture.Read(true, false, false), Is.False);
    }

}
