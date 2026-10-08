using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerWeaponMigrationTests
{
    [Test]
    public void PlayerPrefab_UsesOwnedGunMuzzlesAndKeepsLegacySources()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
        Assert.That(rig, Is.Not.Null);
        Assert.That(rig.transform.parent, Is.EqualTo(player.transform));
        Assert.That(rig.transform.localPosition, Is.EqualTo(Vector3.zero));
        var rigData = new SerializedObject(rig);
        Assert.That(rigData.FindProperty("_playerRoot").objectReferenceValue, Is.EqualTo(player.transform));
        foreach (var direction in new[] { "left", "right", "up", "down" })
        {
            var gun = (Transform)rigData.FindProperty("_" + direction + "Gun").objectReferenceValue;
            var muzzle = (Transform)rigData.FindProperty("_" + direction + "Muzzle").objectReferenceValue;
            Assert.That(gun, Is.Not.Null, direction);
            Assert.That(muzzle, Is.Not.Null, direction);
            Assert.That(muzzle.IsChildOf(gun), Is.True, direction);
            Assert.That(muzzle.localPosition.sqrMagnitude, Is.GreaterThan(0f));
            Assert.That(gun.gameObject.activeSelf, Is.EqualTo(direction == "down"));
            Assert.That(gun.GetComponent<PlayerMuzzleFlash>(), Is.Not.Null);
        }
        var anchor = player.GetComponentInChildren<PlayerMuzzleAnchor>(true);
        Assert.That(anchor.WeaponRig, Is.EqualTo(rig));
        var attack = new SerializedObject(player.GetComponent<PlayerAttack>());
        Assert.That(attack.FindProperty("_muzzleAnchor").objectReferenceValue, Is.EqualTo(anchor));
        Assert.That(attack.FindProperty("_weaponRig").objectReferenceValue, Is.EqualTo(rig));
        Assert.That(player.GetComponent<Player>(), Is.Null);
        Assert.That(player.GetComponentsInChildren<PlayerGunControl>(true), Is.Empty);
        Assert.That(player.GetComponentsInChildren<FirePoint>(true), Is.Empty);
        foreach (var source in new[] { "Player.cs", "playerguncontrol.cs", "FirePoint.cs", "gunflash.cs", "bombscript.cs", "bombEffect.cs" })
            Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Player/" + source), Is.Not.Null, source);
    }

    [Test]
    public void Facing_RejectsInvalidAimAndUsesCardinalDirections()
    {
        Assert.That(PlayerAimUtility.GetFacing(Vector2.zero), Is.EqualTo(PlayerAimFacing.Down));
        Assert.That(PlayerAimUtility.GetFacing(new Vector2(float.NaN, 1)), Is.EqualTo(PlayerAimFacing.Down));
        Assert.That(PlayerAimUtility.GetFacing(new Vector2(float.PositiveInfinity, 1)), Is.EqualTo(PlayerAimFacing.Down));
        Assert.That(PlayerAimUtility.GetFacing(Vector2.right), Is.EqualTo(PlayerAimFacing.Right));
        Assert.That(PlayerAimUtility.GetFacing(Vector2.left), Is.EqualTo(PlayerAimFacing.Left));
        Assert.That(PlayerAimUtility.GetFacing(Vector2.up), Is.EqualTo(PlayerAimFacing.Up));
        Assert.That(PlayerAimUtility.GetFacing(Vector2.down), Is.EqualTo(PlayerAimFacing.Down));
    }

    [Test]
    public void Bomb_MissingOptionalVisualComponentsStillExplodesOnlyOnce()
    {
        var bomb = new GameObject("Bomb without visuals");
        bomb.transform.position = new Vector3(10000, 10000, 0);
        try
        {
            var explosion = bomb.AddComponent<PlayerBombExplosion>();
            Assert.That(explosion.Explode(), Is.True);
            Assert.That(explosion.Explode(), Is.False);
        }
        finally { Object.DestroyImmediate(bomb); }
    }

    [Test]
    public void TimeBombPrefab_UsesNewVisualAndHidesBodyOnDetonation()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/bomb.prefab");
        Assert.That(prefab.GetComponent<Animator>(), Is.Null, "The body keeps the time bomb sprite");
        var legacyAnimator = prefab.GetComponentInChildren<Animator>(true);
        Assert.That(legacyAnimator, Is.Not.Null);
        Assert.That(legacyAnimator.runtimeAnimatorController, Is.Not.Null);
        Assert.That(legacyAnimator.gameObject.activeSelf, Is.False);
        Assert.That(prefab.transform.localScale.x, Is.EqualTo(2.5f));
        Assert.That(prefab.GetComponent<PlayerTimeBombVisual>(), Is.Not.Null);
        var instance = Object.Instantiate(prefab, new Vector3(12000, 12000, 0), Quaternion.identity);
        try
        {
            var presentation = instance.GetComponent<PlayerTimeBombVisual>();
            var explosion = instance.GetComponent<PlayerBombExplosion>();
            explosion.SendMessage("Awake");
            explosion.SendMessage("OnEnable");
            Assert.That(instance.GetComponent<SpriteRenderer>().enabled, Is.True);
            Assert.That(instance.transform.Find("Live Timer").gameObject.activeSelf, Is.True);
            presentation.UpdateFuse(1, 2.1f, 3);
            Assert.That(explosion.Explode(), Is.True);
            Assert.That(instance.GetComponent<SpriteRenderer>().enabled, Is.False);
            Assert.That(instance.transform.Find("Live Timer").gameObject.activeSelf, Is.False);
            Assert.That(instance.transform.Find("Legacy Explosion").gameObject.activeSelf, Is.True);
            Assert.That(instance.GetComponentsInChildren<LineRenderer>(true), Is.Empty, "Bomb has no range indicator or shockwave ring");
            Assert.That(explosion.Explode(), Is.False);
        }
        finally { Object.DestroyImmediate(instance); }
    }
}
