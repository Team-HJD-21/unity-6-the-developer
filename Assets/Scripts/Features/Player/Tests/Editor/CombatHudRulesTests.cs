// 승인된 교체 규칙과 실제 화면 영역/전환의 경계를 검증합니다. 실제 Scene·수동 입력 검수와 별개입니다.
using System;
using NUnit.Framework;
using UnityEngine;

public sealed class CombatHudRulesTests
{
    [Test]
    public void NewSkillReplacesOnlyItsDropSlotAndNotifiesOnce()
    {
        var loadout = new PlayerSkillLoadout(); loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.EnergyBarrier);
        int changes = 0; loadout.Changed += () => changes++;
        Assert.That(loadout.TryEquip(1, PlayerSkillId.PlayerTurret), Is.True);
        Assert.That(loadout.Q, Is.EqualTo(PlayerSkillId.Bomb));
        Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.PlayerTurret));
        Assert.That(changes, Is.EqualTo(1));
    }
    [TestCase(0)] [TestCase(1)]
    public void OtherSlotSkillSwapsRatherThanDuplicating(int slot)
    {
        var loadout = new PlayerSkillLoadout(); loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.EnergyBarrier);
        Assert.That(loadout.TryEquip(slot, slot == 0 ? PlayerSkillId.EnergyBarrier : PlayerSkillId.Bomb), Is.True);
        Assert.That(loadout.Q, Is.EqualTo(PlayerSkillId.EnergyBarrier));
        Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.Bomb));
    }
    [Test]
    public void EmptyQWithEOnlySurvivesCheckpointRestoreAndSwap()
    {
        var original = new PlayerSkillLoadout(); Assert.That(original.TryEquip(1, PlayerSkillId.Bomb), Is.True);
        var restored = new PlayerSkillLoadout(); restored.Restore(original.Q, original.E);
        Assert.That(restored.Q, Is.EqualTo(PlayerSkillId.None)); Assert.That(restored.E, Is.EqualTo(PlayerSkillId.Bomb));
        Assert.That(restored.TryEquip(0, PlayerSkillId.Bomb), Is.True);
        Assert.That(restored.Q, Is.EqualTo(PlayerSkillId.Bomb)); Assert.That(restored.E, Is.EqualTo(PlayerSkillId.None));
    }
    [TestCase(-1, PlayerSkillId.Bomb)] [TestCase(2, PlayerSkillId.Bomb)]
    [TestCase(0, PlayerSkillId.None)] [TestCase(1, (PlayerSkillId)99)]
    public void InvalidTargetNeverMutatesLoadout(int slot, PlayerSkillId skill)
    {
        var loadout = new PlayerSkillLoadout(); loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.EnergyBarrier);
        int changes = 0; loadout.Changed += () => changes++;
        Assert.That(loadout.TryEquip(slot, skill), Is.False);
        Assert.That(loadout.Q, Is.EqualTo(PlayerSkillId.Bomb)); Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.EnergyBarrier));
        Assert.That(changes, Is.Zero);
    }
    [Test]
    public void SameSlotIsUnchangedAndDuplicateCheckpointIsRejected()
    {
        var loadout = new PlayerSkillLoadout(); loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.EnergyBarrier);
        int changes = 0; loadout.Changed += () => changes++;
        Assert.That(loadout.TryEquip(0, PlayerSkillId.Bomb), Is.False); Assert.That(changes, Is.Zero);
        Assert.Throws<ArgumentException>(() => loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.Bomb));
    }
    [TestCase(0, 258, 0)] [TestCase(959, 450, 0)] [TestCase(960, 450, 1)]
    [TestCase(1920, 648, 1)] [TestCase(900, 257, -1)] [TestCase(900, 649, -1)]
    [TestCase(-1, 400, -1)] [TestCase(1921, 400, -1)]
    public void DropHitRegionMatchesDisplayedBand(float x, float topY, int expected)
    {
        Assert.That(CombatHudGeometry.DropSlot(new Vector2(x, 1080 - topY), new Vector2(1920, 1080)), Is.EqualTo(expected));
        Assert.That(CombatHudGeometry.DropSlot(new Vector2(x, 1080 - topY) * .5f, new Vector2(960, 540)), Is.EqualTo(expected));
    }
    [TestCase(150, 0)] [TestCase(112.5f, 1)] [TestCase(70, 2)] [TestCase(22.5f, 3)]
    public void WedgeRaycastAcceptsItsOwnArtAndRejectsCenter(float angle, int index)
    {
        Assert.That(CombatHudGeometry.Wedge(CombatHudGeometry.Radial(262, angle)), Is.EqualTo(index));
        Assert.That(CombatHudGeometry.Wedge(CombatHudGeometry.Radial(100, angle)), Is.EqualTo(-1));
        Assert.That(CombatHudGeometry.Wedge(CombatHudGeometry.Radial(353, angle)), Is.EqualTo(-1));
    }
    [Test]
    public void InterruptedSigmoidContinuesFromCurrentValueAndReachesBothEndpoints()
    {
        Assert.That(CombatHudMotion.Sigmoid(0), Is.EqualTo(0).Within(.00001f));
        Assert.That(CombatHudMotion.Sigmoid(1), Is.EqualTo(1).Within(.00001f));
        var transition = new CombatHudTransition(); transition.Snap(0); transition.Set(1, 1);
        float halfway = transition.Tick(.4f); Assert.That(halfway, Is.GreaterThan(0).And.LessThan(1));
        transition.Set(0, 1); Assert.That(transition.Tick(0), Is.EqualTo(halfway));
        Assert.That(transition.Tick(1), Is.EqualTo(0).Within(.00001f));
    }
}
