using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerWeaponMigrationTests
{
    private GameObject PlayerPrefab => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
    [Test]
    public void WeaponRig_IsOwnedByPlayer()
    {
        var rig = PlayerPrefab.GetComponentInChildren<PlayerWeaponRig>(true);
        Assert.That(rig.transform.parent, Is.EqualTo(PlayerPrefab.transform));
        Assert.That(Vector3.Distance(rig.transform.localPosition, Vector3.zero), Is.LessThan(.001f));
        Assert.That(new SerializedObject(rig).FindProperty("_playerRoot").objectReferenceValue, Is.EqualTo(PlayerPrefab.transform));
    }
    [TestCase("left")]
    [TestCase("right")]
    [TestCase("up")]
    [TestCase("down")]
    public void Muzzle_IsChildOfItsDirectionalGun(string direction)
    {
        var data = new SerializedObject(PlayerPrefab.GetComponentInChildren<PlayerWeaponRig>(true));
        var gun = (Transform)data.FindProperty("_" + direction + "Gun").objectReferenceValue;
        var muzzle = (Transform)data.FindProperty("_" + direction + "Muzzle").objectReferenceValue;
        Assert.That(muzzle.IsChildOf(gun), Is.True);
        Assert.That(muzzle.localPosition.sqrMagnitude, Is.GreaterThan(0f));
    }
    [Test]
    public void Attack_UsesOwnedMuzzleAnchor()
    {
        var rig = PlayerPrefab.GetComponentInChildren<PlayerWeaponRig>(true);
        var anchor = PlayerPrefab.GetComponentInChildren<PlayerMuzzleAnchor>(true);
        var data = new SerializedObject(PlayerPrefab.GetComponent<PlayerAttack>());
        Assert.That(anchor.WeaponRig, Is.EqualTo(rig));
        Assert.That(data.FindProperty("_muzzleAnchor").objectReferenceValue, Is.EqualTo(anchor));
        Assert.That(data.FindProperty("_weaponRig").objectReferenceValue, Is.EqualTo(rig));
    }
    [Test]
    public void PlayerPrefab_DoesNotAttachLegacyControllers()
    {
        Assert.That(PlayerPrefab.GetComponent<Player>(), Is.Null);
        Assert.That(PlayerPrefab.GetComponentsInChildren<PlayerGunControl>(true), Is.Empty);
        Assert.That(PlayerPrefab.GetComponentsInChildren<FirePoint>(true), Is.Empty);
    }
    [TestCase("Player.cs")]
    [TestCase("playerguncontrol.cs")]
    [TestCase("FirePoint.cs")]
    [TestCase("gunflash.cs")]
    [TestCase("bombscript.cs")]
    [TestCase("bombEffect.cs")]
    public void LegacySource_IsPreserved(string source) => Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Player/" + source), Is.Not.Null);
    [TestCase(1, 0, PlayerAimFacing.Right)]
    [TestCase(-1, 0, PlayerAimFacing.Left)]
    [TestCase(0, 1, PlayerAimFacing.Up)]
    [TestCase(0, -1, PlayerAimFacing.Down)]
    [TestCase(0, 0, PlayerAimFacing.Down)]
    [TestCase(float.NaN, 1, PlayerAimFacing.Down)]
    [TestCase(float.PositiveInfinity, 1, PlayerAimFacing.Down)]
    public void Facing_UsesValidCardinalDirection(float x, float y, PlayerAimFacing expected) =>
        Assert.That(PlayerAimUtility.GetFacing(new Vector2(x, y)), Is.EqualTo(expected));
}