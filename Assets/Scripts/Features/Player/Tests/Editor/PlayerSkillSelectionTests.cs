using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerSkillSelectionTests
{
    [TestCase(PlayerSkillId.Bomb, PlayerSkillId.PlayerTurret)]
    [TestCase(PlayerSkillId.PlayerTurret, PlayerSkillId.Bomb)]
    [TestCase(PlayerSkillId.EnergyBarrier, PlayerSkillId.LureBeacon)]
    public void SelectionOrder_AssignsQThenE(PlayerSkillId first, PlayerSkillId second)
    {
        var loadout = new PlayerSkillLoadout(); loadout.TrySelect(first); loadout.TrySelect(second);
        Assert.That(loadout.Q, Is.EqualTo(first)); Assert.That(loadout.E, Is.EqualTo(second));
    }
    [Test]
    public void Selection_RejectsDuplicate()
    {
        var loadout = new PlayerSkillLoadout(); loadout.TrySelect(PlayerSkillId.Bomb);
        Assert.That(loadout.TrySelect(PlayerSkillId.Bomb), Is.False);
        Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.None));
    }
    [TestCase(PlayerSkillId.None)]
    [TestCase((PlayerSkillId)99)]
    public void Selection_RejectsInvalidSkill(PlayerSkillId skill) => Assert.That(new PlayerSkillLoadout().TrySelect(skill), Is.False);
    [Test]
    public void Clear_ResetsBothSlots()
    {
        var loadout = new PlayerSkillLoadout(); loadout.TrySelect(PlayerSkillId.Bomb); loadout.TrySelect(PlayerSkillId.LureBeacon); loadout.Clear();
        Assert.That(loadout.Q, Is.EqualTo(PlayerSkillId.None)); Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.None));
    }
    [TestCase("_bombIcon")]
    [TestCase("_turretIcon")]
    [TestCase("_barrierIcon")]
    [TestCase("_beaconIcon")]
    public void HudPrefab_AssignsSkillIcon(string field)
    {
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        Assert.That(new SerializedObject(hud.GetComponent<PlayerSkillHud>()).FindProperty(field).objectReferenceValue, Is.Not.Null);
    }
    [Test]
    public void PlayerPrefab_AssignsFieldSkills()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(new SerializedObject(player.GetComponent<PlayerBarrierSkill>()).FindProperty("_prefab").objectReferenceValue, Is.Not.Null);
        Assert.That(new SerializedObject(player.GetComponent<PlayerBeaconSkill>()).FindProperty("_prefab").objectReferenceValue, Is.Not.Null);
    }
}
