// 실제 충전·임시 포탑 수리 모델이 저장된 진행부터 같은 후속 상태로 이어지는지 확인합니다.
// Scene 설치물 복원이나 전체 스킬 writer 완료를 대신하는 검사는 아닙니다.
using System;
using System.Collections.Generic;
using NUnit.Framework;

public sealed class PlayerSkillCheckpointStateTests
{
    [Test]
    public void ChargeProgressRestoresRemainingTimeAndMatchesTheSameFutureTicks()
    {
        var source = new PlayerSkillCharges(3, 18);
        source.TryConsume(); source.TryConsume(); source.Tick(5);
        var state = source.CaptureState();
        var restored = new PlayerSkillCharges(3, 18); restored.RestoreState(state);
        foreach (float elapsed in new[] { 12f, 1f, 17f, 1f, 50f })
        {
            source.Tick(elapsed); restored.Tick(elapsed);
            Assert.That(restored.Current, Is.EqualTo(source.Current));
            Assert.That(restored.Remaining, Is.EqualTo(source.Remaining));
        }
        var before = restored.CaptureState();
        Assert.Throws<ArgumentException>(() => restored.RestoreState(default));
        Assert.Throws<ArgumentException>(() => restored.RestoreState(new PlayerSkillCharges.State(4, 18, 2, 10)));
        Assert.That(restored.Current, Is.EqualTo(before.Current));
    }

    [Test]
    public void EveryParallelRepairSurvivesRestoreInsteadOfOnlyTheNearestDeadline()
    {
        var source = new PlayerTurretInventory(4, 30);
        source.TryDeploy(); source.TryDeploy(); source.TryDeploy();
        source.Return(0.5f); source.Return(0.1f); source.Tick(4);
        var saved = source.CaptureState();
        Assert.That(saved.Repairs, Is.EqualTo(new[] { 11f, 23f }));
        var target = new PlayerTurretInventory(4, 30); target.RestoreState(saved);
        foreach (float elapsed in new[] { 10f, 2f, 10f, 2f })
        {
            source.Tick(elapsed); target.Tick(elapsed);
            Assert.That(target.Available, Is.EqualTo(source.Available));
            Assert.That(target.Deployed, Is.EqualTo(source.Deployed));
            Assert.That(target.CaptureState().Repairs, Is.EqualTo(source.CaptureState().Repairs));
        }
        Assert.That(saved.Repairs, Is.EqualTo(new[] { 11f, 23f }));
    }

    [Test]
    public void InvalidOrMutableRepairInputCannotCorruptTheRestoredInventory()
    {
        var values = new List<float> { 12, 20 };
        var state = new PlayerTurretInventory.State(3, 30, 1, values); values[0] = -1;
        Assert.That(state.Repairs[0], Is.EqualTo(12));
        Assert.Throws<NotSupportedException>(() => ((IList<float>)state.Repairs)[0] = -1);
        Assert.Throws<ArgumentException>(() => new PlayerTurretInventory.State(3, 30, 2, new[] { 1f, 2f }));
        Assert.Throws<ArgumentException>(() => new PlayerTurretInventory.State(3, 30, 1, new[] { float.NaN }));
        var inventory = new PlayerTurretInventory(3, 30); inventory.TryDeploy();
        Assert.Throws<ArgumentException>(() => inventory.RestoreState(new PlayerTurretInventory.State(4, 30, 0, Array.Empty<float>())));
        Assert.That(inventory.Deployed, Is.EqualTo(1));
    }

    [Test]
    public void LoadoutRestoresAtomicallyAndRejectsImpossibleSlotStates()
    {
        var loadout = new PlayerSkillLoadout(); int changes = 0; loadout.Changed += () => changes++;
        loadout.Restore(PlayerSkillId.EnergyBarrier, PlayerSkillId.Bomb);
        Assert.That(changes, Is.EqualTo(1)); Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.Bomb));
        Assert.Throws<ArgumentException>(() => loadout.Restore(PlayerSkillId.None, PlayerSkillId.Bomb));
        Assert.Throws<ArgumentException>(() => loadout.Restore(PlayerSkillId.Bomb, PlayerSkillId.Bomb));
        Assert.Throws<ArgumentException>(() => loadout.Restore((PlayerSkillId)99, PlayerSkillId.None));
        Assert.That(changes, Is.EqualTo(1)); Assert.That(loadout.Q, Is.EqualTo(PlayerSkillId.EnergyBarrier));
    }
}
