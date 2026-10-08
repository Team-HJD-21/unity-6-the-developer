// 실제 선택 순서/중복/전투 잠금과 Player 프리팹의 스킬·아이콘 연결을 검증합니다.
using NUnit.Framework;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class PlayerSkillSelectionTests
{
    [TestCase(PlayerSkillId.Bomb, PlayerSkillId.TemporaryTurret)]
    [TestCase(PlayerSkillId.TemporaryTurret, PlayerSkillId.Bomb)]
    public void SelectionOrder_AssignsQThenE(PlayerSkillId first, PlayerSkillId second)
    {
        var loadout = new PlayerSkillLoadout();
        Assert.That(loadout.Q, Is.EqualTo(PlayerSkillId.None));
        Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.None));
        Assert.That(loadout.TrySelect(first), Is.True);
        Assert.That(loadout.Q, Is.EqualTo(first));
        Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.None));
        Assert.That(loadout.TrySelect(first), Is.False);
        Assert.That(loadout.TrySelect(PlayerSkillId.None), Is.False);
        Assert.That(loadout.TrySelect(second), Is.True);
        Assert.That(loadout.E, Is.EqualTo(second));
        Assert.That(loadout.TrySelect(first), Is.False);
        loadout.Clear();
        Assert.That(loadout.Q, Is.EqualTo(PlayerSkillId.None));
        Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.None));
        Assert.That(loadout.TrySelect(second), Is.True);
        Assert.That(loadout.Q, Is.EqualTo(second));
    }

    [Test]
    public void PlayerPrefab_HasReadySkillsAndAssignedVisualAssets()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var controller = prefab.GetComponent<PlayerSkillController>();
        Assert.That(controller, Is.Not.Null);
        foreach (string field in new[] { "bombIcon", "turretIcon", "_barrierIcon", "_beaconIcon", "uiFont" })
            Assert.That(new SerializedObject(controller).FindProperty(field).objectReferenceValue, Is.Not.Null, field);
        Assert.That(new SerializedObject(prefab.GetComponent<PlayerBomb>()).FindProperty("bombPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(new SerializedObject(prefab.GetComponent<PlayerTurretSkill>()).FindProperty("turretPrefab").objectReferenceValue, Is.Not.Null);
        Assert.That(prefab.GetComponent<PlayerTurretSkill>().MaxTurretCount, Is.EqualTo(3));
        Assert.That(prefab.GetComponent<PlayerTurretSkill>().SkillCooldown, Is.EqualTo(3f));
        var turret = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/TemporaryTurret.prefab");
        foreach (var renderer in turret.GetComponentsInChildren<SpriteRenderer>())
            Assert.That(renderer.sprite, Is.Not.Null, renderer.name);
        var turretData = new SerializedObject(turret.GetComponent<TemporaryTurret>());
        var head = (Transform)turretData.FindProperty("turretHead").objectReferenceValue;
        var firePoint = (Transform)turretData.FindProperty("firePoint").objectReferenceValue;
        Assert.That(firePoint.IsChildOf(head), Is.True, "Muzzle follows rotating head");
        var projectile = (GameObject)turretData.FindProperty("bulletPrefab").objectReferenceValue;
        Assert.That(projectile.GetComponent<TemporaryTurretProjectile>(), Is.Not.Null);
        Assert.That(projectile.GetComponent<PlayerBullet>(), Is.Null, "Support turret is independent of player weapons");
        var definition = AssetDatabase.LoadAssetAtPath<TeamHJD.Game.Content.TurretDefinition>(
            "Assets/Scripts/Tower/TurretDefinitions/CanonDefinition/Canon_Stage1_LV1.asset");
        Assert.That(turretData.FindProperty("damagePerShot").floatValue, Is.EqualTo(definition.Damage * .5f));
        var cannonBullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Tower/Weapons/bullet.prefab");
        Assert.That(projectile.GetComponent<SpriteRenderer>().sprite, Is.EqualTo(cannonBullet.GetComponent<SpriteRenderer>().sprite));
    }

    [Test]
    public void SupportProjectile_PassesSupportDevicesAndAppliesOneSixDamageHit()
    {
        var template = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/TemporaryTurretProjectile.prefab");
        var projectileObject = Object.Instantiate(template);
        var deviceObject = new GameObject("Friendly device", typeof(BoxCollider2D), typeof(PlayerFieldDevice));
        var enemyObject = new GameObject("Enemy", typeof(BoxCollider2D), typeof(WalkAndAttackMonster));
        try
        {
            var projectile = projectileObject.GetComponent<TemporaryTurretProjectile>();
            Assert.That(projectile.Initialize(Vector2.zero, 6), Is.False);
            Assert.That(projectile.Initialize(Vector2.right, 6), Is.True);
            Assert.That(projectile.Initialize(Vector2.right, 6), Is.False, "Cannot initialize twice");
            projectile.SendMessage("OnTriggerEnter2D", deviceObject.GetComponent<Collider2D>());
            Assert.That(projectileObject != null, Is.True, "Energy barriers do not consume allied bullets");
            var enemy = enemyObject.GetComponent<WalkAndAttackMonster>();
            enemy.maxHealth = enemy.currentHealth = 100;
            projectile.SendMessage("OnTriggerEnter2D", enemyObject.GetComponent<Collider2D>());
            Assert.That(enemy.currentHealth, Is.EqualTo(94));
            Assert.That(projectileObject == null, Is.True, "Bullet is removed immediately after one hit");
        }
        finally
        {
            if (projectileObject != null) Object.DestroyImmediate(projectileObject);
            Object.DestroyImmediate(deviceObject);
            Object.DestroyImmediate(enemyObject);
        }
    }

    [Test]
    public void TurretInventory_LimitsDeploymentAndRecoversOnlyNearbyOwnedTurrets()
    {
        var player = new GameObject("Inventory Test Player");
        var template = new GameObject("Turret Template", typeof(TemporaryTurret));
        var otherPlayer = new GameObject("Other Player");
        try
        {
            var controller = player.AddComponent<PlayerSkillController>();
            controller.SendMessage("Awake");
            var skill = player.GetComponent<PlayerTurretSkill>();
            skill.SendMessage("Awake");
            var data = new SerializedObject(skill);
            data.FindProperty("turretPrefab").objectReferenceValue = template;
            data.ApplyModifiedPropertiesWithoutUndo();
            controller.BindSceneState(() => true, () => true);
            Assert.That(controller.TrySelect(PlayerSkillId.TemporaryTurret), Is.True);
            Assert.That(skill.CurrentTurretCount, Is.EqualTo(3));
            Assert.That(controller.TryUse(PlayerSkillId.TemporaryTurret), Is.True);
            var first = skill.RecoverableTurret;
            Assert.That(first, Is.Not.Null);
            Assert.That(skill.CurrentTurretCount, Is.EqualTo(2));
            Assert.That(controller.TryUse(PlayerSkillId.TemporaryTurret), Is.False, "Immediate reinstallation is blocked");
            skill.SendMessage("TickCooldown", 2.9f);
            Assert.That(controller.TryUse(PlayerSkillId.TemporaryTurret), Is.False);
            skill.SendMessage("TickCooldown", .11f);
            player.transform.position = Vector3.right * 10;
            Assert.That(controller.TryUse(PlayerSkillId.TemporaryTurret), Is.True);
            skill.SendMessage("TickCooldown", 3f);
            player.transform.position = Vector3.right * 20;
            Assert.That(controller.TryUse(PlayerSkillId.TemporaryTurret), Is.True);
            skill.SendMessage("TickCooldown", 3f);
            Assert.That(skill.CurrentTurretCount, Is.Zero);
            Assert.That(controller.TryUse(PlayerSkillId.TemporaryTurret), Is.False, "Only three turrets can be deployed");
            player.transform.position = Vector3.right * 100;
            Assert.That(skill.TryRecoverNearest(), Is.False, "Recovery requires proximity");
            var otherSkill = otherPlayer.AddComponent<PlayerTurretSkill>();
            Assert.That(first.TryRecover(otherSkill), Is.False, "Cannot recover another player's turret");
            player.transform.position = first.transform.position;
            Assert.That(skill.TryRecoverNearest(), Is.True);
            Assert.That(skill.CurrentTurretCount, Is.EqualTo(1));
            Assert.That(skill.TryRecoverNearest(), Is.False, "One press recovers only one nearby turret");
            Assert.That(skill.CurrentTurretCount, Is.EqualTo(1));
            Assert.That(controller.TryUse(PlayerSkillId.TemporaryTurret), Is.True, "Recovered turret can be installed again");
            Assert.That(skill.CurrentTurretCount, Is.Zero);
            Assert.That(skill.TryRecoverNearest(), Is.True, "Recovery remains available during installation cooldown");
            Assert.That(skill.IsCooldown, Is.True, "Recovery does not bypass installation cooldown");
            controller.BindSceneState(() => false, () => false);
            Assert.That(skill.TryRecoverNearest(), Is.False, "Pause/dialogue blocks recovery");
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(template);
            Object.DestroyImmediate(otherPlayer);
        }
    }

    [Test]
    public void SelectionPanel_OpensEmptyAssignsAndLocksWhenWaveStarts()
    {
        var player = new GameObject("Skill Test Player");
        var icon = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), Vector2.one * .5f);
        try
        {
            var controller = player.AddComponent<PlayerSkillController>();
            controller.SendMessage("Awake");
            var bomb = player.GetComponent<PlayerBomb>();
            var turret = player.GetComponent<PlayerTurretSkill>();
            bomb.SendMessage("Awake");
            turret.SendMessage("Awake");
            var bombSerialized = new SerializedObject(bomb);
            bombSerialized.FindProperty("bombPrefab").objectReferenceValue = player;
            bombSerialized.ApplyModifiedPropertiesWithoutUndo();
            var turretSerialized = new SerializedObject(turret);
            player.AddComponent<TemporaryTurret>();
            turretSerialized.FindProperty("turretPrefab").objectReferenceValue = player;
            turretSerialized.ApplyModifiedPropertiesWithoutUndo();
            bool preparing = true;
            controller.BindSceneState(() => preparing, () => !preparing);
            var controllerSerialized = new SerializedObject(controller);
            controllerSerialized.FindProperty("bombIcon").objectReferenceValue = icon;
            controllerSerialized.FindProperty("turretIcon").objectReferenceValue = icon;
            controllerSerialized.ApplyModifiedPropertiesWithoutUndo();
            controller.SendMessage("Start");
            var hud = player.GetComponentInChildren<PlayerSkillHud>();
            var hudObject = hud.gameObject;
            Assert.That(hudObject.transform.Find("Q Skill Slot/Background/Empty Grid").gameObject.activeSelf, Is.True);
            hud.Open();
            Assert.That(hud.IsOpen, Is.True);
            Assert.That(controller.IsSelectionOpen, Is.True);
            controller.BindSceneState(() => preparing, () => true);
            Assert.That(controller.CanUse, Is.False, "Selection panel blocks skill use");
            controller.BindSceneState(() => preparing, () => !preparing);
            var panel = hudObject.transform.Find("Skill Selection/Panel Border/Skill Selection Panel");
            for (int i = 5; i <= 8; i++)
            {
                var placeholder = panel.Find("Development Slot " + i);
                Assert.That(placeholder, Is.Not.Null);
                Assert.That(placeholder.GetComponent<Button>(), Is.Null, "Development slots cannot be selected");
                Assert.That(placeholder.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("개발 중"));
            }
            panel.Find("TemporaryTurret").GetComponent<Button>().onClick.Invoke();
            Assert.That(controller.Loadout.Q, Is.EqualTo(PlayerSkillId.TemporaryTurret));
            Assert.That(controller.Loadout.E, Is.EqualTo(PlayerSkillId.None));
            Assert.That(hudObject.transform.Find("Q Skill Slot/Background/Empty Grid").gameObject.activeSelf, Is.False);
            Assert.That(panel.Find("Q Skill Slot/Background/Empty Grid").gameObject.activeSelf, Is.False);
            Assert.That(panel.Find("E Skill Slot/Background/Empty Grid").gameObject.activeSelf, Is.True);
            var sweep = hudObject.transform.Find("Q Skill Slot/Background/Cooldown Sweep").GetComponent<Image>();
            Assert.That(sweep.type, Is.EqualTo(Image.Type.Filled));
            Assert.That(sweep.fillMethod, Is.EqualTo(Image.FillMethod.Radial360));
            Assert.That(sweep.fillOrigin, Is.EqualTo((int)Image.Origin360.Top));
            Assert.That(sweep.fillClockwise, Is.False, "Remaining mask reveals clockwise from 12 o'clock");
            Assert.That(sweep.sprite, Is.Not.Null);
            sweep.fillAmount = .75f;
            using (var vertices = new VertexHelper())
            {
                typeof(Image).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null).Invoke(sweep, new object[] { vertices });
                var triangles = new List<UIVertex>();
                vertices.GetUIVertexStream(triangles);
                Assert.That(MeshCovers(triangles, new Vector2(20, 20)), Is.False, "Top-right quarter is revealed first");
                Assert.That(MeshCovers(triangles, new Vector2(-20, 20)), Is.True, "Top-left stays gray until the end");
            }
            panel.Find("Bomb").GetComponent<Button>().onClick.Invoke();
            Assert.That(controller.Loadout.E, Is.EqualTo(PlayerSkillId.Bomb));
            Assert.That(hud.IsOpen, Is.False);
            Assert.That(panel.Find("E Skill Slot/Background/Empty Grid").gameObject.activeSelf, Is.False);
            // 선택 창을 닫고 전투로 전환한 뒤에도 HUD 아이콘에서 툴팁을 볼 수 있습니다.
            preparing = false;
            hud.SendMessage("LateUpdate");
            var qSlot = hudObject.transform.Find("Q Skill Slot").gameObject;
            Assert.That(qSlot.GetComponent<Image>().raycastTarget, Is.True);
            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(qSlot, pointer, ExecuteEvents.pointerEnterHandler);
            var tooltip = hudObject.transform.Find("Skill Tooltip");
            Assert.That(tooltip.gameObject.activeSelf, Is.True);
            Assert.That(tooltip.GetComponentInChildren<TMPro.TMP_Text>().text, Is.EqualTo("보조 포탑"));
            foreach (var graphic in tooltip.GetComponentsInChildren<Graphic>())
                Assert.That(graphic.raycastTarget, Is.False, "Tooltip must not block pointer exit or gameplay");
            ExecuteEvents.Execute(qSlot, pointer, ExecuteEvents.pointerExitHandler);
            Assert.That(tooltip.gameObject.activeSelf, Is.False);
            preparing = true;
            hud.Open();
            preparing = false;
            hud.SendMessage("LateUpdate");
            Assert.That(hud.IsOpen, Is.False, "Starting a wave closes selection");
            hud.Open();
            Assert.That(hud.IsOpen, Is.False);
            controller.ClearSelection();
            Assert.That(controller.Loadout.Q, Is.EqualTo(PlayerSkillId.TemporaryTurret));
            preparing = true;
            controller.ClearSelection();
            Assert.That(controller.Loadout.Q, Is.EqualTo(PlayerSkillId.None));
            Assert.That(controller.TryUse(PlayerSkillId.None), Is.False);
        }
        finally { Object.DestroyImmediate(player); Object.DestroyImmediate(icon); }
    }

    private static bool MeshCovers(List<UIVertex> vertices, Vector2 point)
    {
        for (int i = 0; i < vertices.Count; i += 3)
        {
            Vector2 a = vertices[i].position, b = vertices[i + 1].position, c = vertices[i + 2].position;
            float ab = Cross(b - a, point - a), bc = Cross(c - b, point - b), ca = Cross(a - c, point - c);
            if ((ab >= 0 && bc >= 0 && ca >= 0) || (ab <= 0 && bc <= 0 && ca <= 0)) return true;
        }
        return false;
    }

    private static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;
}
