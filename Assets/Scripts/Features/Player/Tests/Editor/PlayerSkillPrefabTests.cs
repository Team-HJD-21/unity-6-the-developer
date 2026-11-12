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
        Assert.That(prefab.GetComponent<PlayerSkillHud>().IsFullyConfigured, Is.True);
        Assert.That(prefab.GetComponent<Canvas>().enabled, Is.True);
        Assert.That(prefab.GetComponent<Canvas>().renderMode, Is.EqualTo(RenderMode.ScreenSpaceOverlay));
        var failure = prefab.transform.Find("Placement Failure").GetComponent<TMPro.TMP_Text>();
        Assert.That(failure.color, Is.EqualTo(Color.red));
        Assert.That((failure.fontStyle & TMPro.FontStyles.Bold) != 0, Is.True);
        Assert.That(failure.raycastTarget, Is.False);
    }

    [Test]
    public void SkillHudPrefab_ContainsEditableViewsAndFontsBeforePlay()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        Assert.That(prefab.GetComponent<PlayerSkillHudSceneBinding>(), Is.Not.Null);
        Assert.That(prefab.GetComponentsInChildren<PlayerSkillSlotView>(true).Length, Is.EqualTo(4));
        Assert.That(prefab.GetComponentsInChildren<PlayerSkillCardView>(true).Length, Is.EqualTo(4));
        Assert.That(prefab.GetComponentsInChildren<PlayerSkillTooltipView>(true).Length, Is.EqualTo(1));
        Assert.That(prefab.GetComponentsInChildren<PlayerTurretRecoveryPromptView>(true).Length, Is.EqualTo(1));
        foreach (var text in prefab.GetComponentsInChildren<TMPro.TMP_Text>(true))
            Assert.That(text.font, Is.Not.Null, text.name);
        var key = prefab.transform.Find("Turret Recovery Prompt/F Key Icon").GetComponent<UnityEngine.UI.Image>();
        Assert.That(key.sprite, Is.Not.Null);
        Assert.That(key.raycastTarget, Is.False);
    }

    [TestCase("EnergyBarrier")]
    [TestCase("LureBeacon")]
    public void DevicePrefab_AssignsEmitterSprite(string name)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/" + name + ".prefab");
        Assert.That(prefab.GetComponent<PlayerFieldDevice>().EmitterSprite, Is.Not.Null);
    }
    [TestCase("PlayerBombPreview", typeof(PlayerBomb))]
    [TestCase("PlayerBeaconPreview", typeof(PlayerBeaconSkill))]
    [TestCase("PlayerBarrierPreview", typeof(PlayerBarrierSkill))]
    public void PlacementPreview_IsAuthoredVisualOnlyAndAssigned(string name, System.Type skillType)
    {
        var preview = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/" + name + ".prefab");
        var view = preview.GetComponent<PlayerSkillPlacementPreview>();
        Assert.That(view.IsConfigured, Is.True);
        Assert.That(preview.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(preview.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        Assert.That(preview.GetComponentsInChildren<MonoBehaviour>(true).Length, Is.EqualTo(1));
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var data = new SerializedObject(player.GetComponent(skillType));
        Assert.That(data.FindProperty("_previewPrefab").objectReferenceValue, Is.EqualTo(view));
        Assert.That(data.FindProperty("_placementRadius").floatValue, Is.EqualTo(12));
        var range = preview.transform.Find("Placement Range").GetComponent<LineRenderer>();
        Assert.That(range.loop, Is.True);
        Assert.That(range.useWorldSpace, Is.False);
        Assert.That(range.positionCount, Is.EqualTo(64));
        for (int i = 0; i < range.positionCount; i++)
            Assert.That(range.GetPosition(i).magnitude, Is.EqualTo(1).Within(.0001f));
        var skill = (PlayerPlacementSkillBase)player.GetComponent(skillType);
        Vector2 pointer = (Vector2)player.transform.position + new Vector2(2.25f, 1.75f);
        Assert.That(skill.GetPlacementPosition(pointer), Is.EqualTo(new Vector3(pointer.x, pointer.y, player.transform.position.z)));
        if (skillType == typeof(PlayerBarrierSkill))
        {
            Assert.That(data.FindProperty("_maxDistance").floatValue, Is.EqualTo(13.5f));
            Assert.That(preview.GetComponentsInChildren<SpriteRenderer>(true).Length, Is.EqualTo(2));
            Assert.That(preview.GetComponentInChildren<LineRenderer>(true).useWorldSpace, Is.True);
        }
    }

    [Test]
    public void PlayerTurret_DamageIsHalfOfLevelOneCannon()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurret.prefab");
        var definition = AssetDatabase.LoadAssetAtPath<TeamHJD.Game.Content.FieldTurretDefinition>(
            "Assets/Data/FieldTurrets/CanonDefinition/Canon_Stage1_LV1.asset");
        Assert.That(new SerializedObject(prefab.GetComponent<PlayerTurret>()).FindProperty("damagePerShot").floatValue,
            Is.EqualTo(definition.Damage * .5f).Within(.001f));
    }
    [Test]
    public void PlayerTurret_HasSerializedHealth()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurret.prefab");
        Assert.That(prefab.GetComponent<PlayerTurretHealth>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerTurretHealth>().MaxHealth, Is.EqualTo(100).Within(.001f));
    }

    [Test]
    public void SupportTurretPreview_IsVisualOnlyAndConnectedToPlayer()
    {
        var preview = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurretPreview.prefab");
        Assert.That(preview.GetComponent<PlayerTurretPlacementPreview>().IsConfigured, Is.True);
        Assert.That(preview.GetComponentsInChildren<Collider2D>(true), Is.Empty);
        Assert.That(preview.GetComponentsInChildren<Rigidbody2D>(true), Is.Empty);
        Assert.That(preview.GetComponent<PlayerTurret>(), Is.Null);
        Assert.That(preview.GetComponent<PlayerTurretHealth>(), Is.Null);
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        Assert.That(player.GetComponent<PlayerTurretSkill>().HasPrefab, Is.True);
        var data = new SerializedObject(player.GetComponent<PlayerTurretSkill>());
        Assert.That(data.FindProperty("_previewPrefab").objectReferenceValue,
            Is.EqualTo(preview.GetComponent<PlayerTurretPlacementPreview>()));
    }

    [Test]
    public void PlayerTurret_AuthoredBodyCoversBaseAndBlocksBothActorLayers()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurret.prefab");
        var body = prefab.GetComponent<BoxCollider2D>();
        Assert.That(body, Is.Not.Null);
        Assert.That(body.isTrigger, Is.False);
        Bounds visual = prefab.GetComponent<SpriteRenderer>().localBounds;
        Assert.That(body.offset.x - body.size.x * .5f, Is.LessThanOrEqualTo(visual.min.x + .001f));
        Assert.That(body.offset.x + body.size.x * .5f, Is.GreaterThanOrEqualTo(visual.max.x - .001f));
        Assert.That(body.offset.y - body.size.y * .5f, Is.LessThanOrEqualTo(visual.min.y + .001f));
        Assert.That(body.offset.y + body.size.y * .5f, Is.GreaterThanOrEqualTo(visual.max.y - .001f));
        foreach (string layer in new[] { "PlayerFeetBox", "EnemyFeetBox" })
        {
            int bit = 1 << LayerMask.NameToLayer(layer);
            Assert.That(body.includeLayers.value & bit, Is.Not.Zero);
            Assert.That(body.excludeLayers.value & bit, Is.Zero);
        }
        Assert.That(body.excludeLayers.value & (1 << LayerMask.NameToLayer("Bullet")), Is.Not.Zero);
    }
    [Test]
    public void PlayerTurret_MuzzleFollowsRotatingHead()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurret.prefab");
        var data = new SerializedObject(prefab.GetComponent<PlayerTurret>());
        var head = (Transform)data.FindProperty("turretHead").objectReferenceValue;
        var muzzle = (Transform)data.FindProperty("firePoint").objectReferenceValue;
        Assert.That(muzzle.IsChildOf(head), Is.True);
    }
    [Test]
    public void PlayerTurret_UsesIndependentCannonProjectile()
    {
        var turret = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurret.prefab");
        var projectile = (GameObject)new SerializedObject(turret.GetComponent<PlayerTurret>()).FindProperty("bulletPrefab").objectReferenceValue;
        Assert.That(projectile.GetComponent<PlayerTurretProjectile>(), Is.Not.Null);
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
