using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerSkillPrefabTests
{
    [Test]
    public void SkillHudPrefab_HasConnectedCanvasAndRedBoldFailureText()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        Assert.That(prefab.GetComponent<PlayerSkillHud>().IsConfigured, Is.True);
        Assert.That(prefab.GetComponent<Canvas>().enabled, Is.True);
        Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        var failure = prefab.transform.Find("Placement Failure").GetComponent<TMPro.TMP_Text>();
        Assert.That(failure.color, Is.EqualTo(Color.red));
        Assert.That((failure.fontStyle & TMPro.FontStyles.Bold) != 0, Is.True);
        Assert.That(failure.raycastTarget, Is.False);
    }

    [TestCase("EnergyBarrier")]
    [TestCase("LureBeacon")]
    public void DevicePrefab_AssignsEmitterSprite(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/" + name + ".prefab");
        Assert.That(prefab.GetComponent<PlayerFieldDevice>().EmitterSprite, Is.Not.Null);
    }
    [Test]
    public void SupportTurret_DamageIsHalfOfLevelOneCannon()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/TemporaryTurret.prefab");
        var definition = AssetDatabase.LoadAssetAtPath<TeamHJD.Game.Content.FieldTurretDefinition>(
            "Assets/Data/FieldTurrets/CanonDefinition/Canon_Stage1_LV1.asset");
        Assert.That(new SerializedObject(prefab.GetComponent<TemporaryTurret>()).FindProperty("damagePerShot").floatValue,
            Is.EqualTo(definition.Damage * .5f).Within(.001f));
    }
    [Test]
    public void SupportTurret_HasSerializedHealth()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/TemporaryTurret.prefab");
        Assert.That(prefab.GetComponent<TemporaryTurretHealth>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<TemporaryTurretHealth>().MaxHealth, Is.EqualTo(100).Within(.001f));
    }
    [Test]
    public void SupportTurret_MuzzleFollowsRotatingHead()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/TemporaryTurret.prefab");
        var data = new SerializedObject(prefab.GetComponent<TemporaryTurret>());
        var head = (Transform)data.FindProperty("turretHead").objectReferenceValue;
        var muzzle = (Transform)data.FindProperty("firePoint").objectReferenceValue;
        Assert.That(muzzle.IsChildOf(head), Is.True);
    }
    [Test]
    public void SupportTurret_UsesIndependentCannonProjectile()
    {
        var turret = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/TemporaryTurret.prefab");
        var projectile = (GameObject)new SerializedObject(turret.GetComponent<TemporaryTurret>()).FindProperty("bulletPrefab").objectReferenceValue;
        Assert.That(projectile.GetComponent<TemporaryTurretProjectile>(), Is.Not.Null);
        Assert.That(projectile.GetComponent<PlayerBullet>(), Is.Null);
        var cannon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/FieldTurrets/Weapons/bullet.prefab");
        Assert.That(projectile.GetComponent<SpriteRenderer>().sprite, Is.EqualTo(cannon.GetComponent<SpriteRenderer>().sprite));
    }
    [Test]
    public void BombPrefab_KeepsLegacyExplosionSeparateFromBody()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/bomb.prefab");
        Assert.That(prefab.GetComponent<Animator>(), Is.Null);
        var explosion = prefab.transform.Find("Legacy Explosion");
        Assert.That(explosion.GetComponent<Animator>().runtimeAnimatorController, Is.Not.Null);
        Assert.That(explosion.gameObject.activeSelf, Is.False);
    }
}
