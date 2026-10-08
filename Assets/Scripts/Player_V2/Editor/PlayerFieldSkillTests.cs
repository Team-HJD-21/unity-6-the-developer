using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerFieldSkillTests
{
    [Test]
    public void BarrierPrefab_VisualsFollowEndpointsWithoutRotatingBodies()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/EnergyBarrier.prefab");
        var instance = Object.Instantiate(prefab);
        try
        {
            Vector3 start = new Vector3(5000, 5000, 0);
            Vector3 end = start + new Vector3(3, 2, 0);
            var device = instance.GetComponent<PlayerFieldDevice>();
            Assert.That(device.EmitterSprite, Is.Not.Null);
            device.Initialize(PlayerFieldDeviceKind.Barrier, start, end, .3f, 300, 8);
            var bodies = instance.GetComponentsInChildren<SpriteRenderer>();
            Assert.That(bodies.Length, Is.EqualTo(2));
            Assert.That(instance.transform.localScale, Is.EqualTo(Vector3.one), "Visual enlargement must not stretch the blocker");
            Assert.That(Vector3.Distance(bodies[0].transform.position, start), Is.LessThan(.001f));
            Assert.That(Vector3.Distance(bodies[1].transform.position, end), Is.LessThan(.001f));
            foreach (var body in bodies)
            {
                Assert.That(body.sprite, Is.EqualTo(device.EmitterSprite));
                Assert.That(body.transform.localScale.x, Is.EqualTo(2.5f));
                Assert.That(Quaternion.Angle(body.transform.rotation, Quaternion.identity), Is.LessThan(.001f));
            }
            var membrane = instance.transform.Find("Energy Membrane").GetComponent<LineRenderer>();
            Assert.That(membrane.useWorldSpace, Is.True);
            Assert.That(membrane.GetPosition(0), Is.EqualTo(start + Vector3.up * .3f));
            Assert.That(membrane.GetPosition(1), Is.EqualTo(end + Vector3.up * .3f));
            Assert.That(membrane.startWidth, Is.EqualTo(.48f).Within(.001f), "Barrier visual thickness is unchanged");
            Assert.That(instance.GetComponent<BoxCollider2D>().size.x, Is.EqualTo(Vector3.Distance(start, end)).Within(.001f));
            Assert.That(instance.GetComponent<BoxCollider2D>().size.y, Is.EqualTo(.3f));
        }
        finally { Object.DestroyImmediate(instance); }
        var beacon = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/LureBeacon.prefab");
        Assert.That(beacon.GetComponent<PlayerFieldDevice>().EmitterSprite, Is.Not.Null);
    }

    [Test]
    public void TwoPulseBarrier_InvalidPlacementPreservesChargeAndRechargeIsSequential()
    {
        var player = new GameObject("Field skill player");
        var template = new GameObject("Barrier template", typeof(PlayerFieldDevice));
        var obstacle = new GameObject("Impassable terrain", typeof(BoxCollider2D));
        try
        {
            player.layer = LayerMask.NameToLayer("Player");
            player.transform.position = new Vector3(2000, 3000, 0);
            var controller = player.AddComponent<PlayerSkillController>();
            controller.SendMessage("Awake");
            controller.BindSceneState(() => true, () => true);
            var skill = player.GetComponent<PlayerBarrierSkill>();
            skill.SendMessage("Awake");
            var serialized = new SerializedObject(skill);
            serialized.FindProperty("_prefab").objectReferenceValue = template.GetComponent<PlayerFieldDevice>();
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(controller.TrySelect(PlayerSkillId.EnergyBarrier), Is.True);
            Assert.That(skill.Charges, Is.EqualTo(3));
            obstacle.transform.position = player.transform.position + Vector3.up * 50;
            Physics2D.SyncTransforms();
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
            Assert.That(skill.IsPending, Is.True);
            Assert.That(skill.Charges, Is.EqualTo(3));
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.False, "Coincident pulses rejected");
            player.transform.position += Vector3.right * 7;
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.False, "Out-of-range pulse rejected");
            Assert.That(skill.IsPending, Is.True);
            player.transform.position -= Vector3.right * 4;
            obstacle.transform.position = player.transform.position - Vector3.right * 1.5f;
            // 검사 대상 레이어는 실제 플레이어와 충돌 가능한 레이어로 고릅니다.
            for (int layer = 0; layer < 32; layer++)
                if (!Physics2D.GetIgnoreLayerCollision(player.layer, layer)) { obstacle.layer = layer; break; }
            Physics2D.SyncTransforms();
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.False, "Terrain blocks entire connection");
            Assert.That(skill.Charges, Is.EqualTo(3));
            Object.DestroyImmediate(obstacle);
            obstacle = null;
            Physics2D.SyncTransforms();
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
            Assert.That(skill.Charges, Is.EqualTo(2));
            Assert.That(skill.IsPending, Is.False);
            Assert.That(skill.RechargeRemaining, Is.EqualTo(18));
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
            skill.CancelPlacement();
            Assert.That(skill.Charges, Is.EqualTo(2), "Cancel does not consume a charge");
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
            player.transform.position += Vector3.up * 2;
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
            Assert.That(skill.Charges, Is.EqualTo(1));
            Assert.That(skill.RechargeRemaining, Is.EqualTo(18), "Second deployment does not reset recharge");
            skill.Tick(18);
            Assert.That(skill.Charges, Is.EqualTo(2));
            skill.Tick(18);
            Assert.That(skill.Charges, Is.EqualTo(3));
            skill.Tick(180);
            Assert.That(skill.Charges, Is.EqualTo(3), "Charge count is capped");
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.True);
            skill.Tick(15);
            Assert.That(skill.IsPending, Is.False, "Unfinished pulse expires without consumption");
            Assert.That(skill.Charges, Is.EqualTo(3));
            controller.BindSceneState(() => false, () => false);
            Assert.That(controller.TryUse(PlayerSkillId.EnergyBarrier), Is.False);
        }
        finally
        {
            if (obstacle != null) Object.DestroyImmediate(obstacle);
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(template);
        }
    }

    [Test]
    public void Devices_BlockOnlyCrossingRoutesAndBarrierOverridesBeacon()
    {
        Vector3 origin = new Vector3(1000, 1000, 0);
        var wallObject = new GameObject("Wall");
        var beaconObject = new GameObject("Beacon");
        try
        {
            var wall = wallObject.AddComponent<PlayerFieldDevice>();
            wall.Initialize(PlayerFieldDeviceKind.Barrier, origin + new Vector3(-2, 0), origin + new Vector3(2, 0), .3f, 300, 8);
            Physics2D.SyncTransforms();
            Assert.That(PlayerFieldDevice.Resolve(origin + Vector3.down * 3, origin + Vector3.up * 3), Is.EqualTo(wall));
            Assert.That(PlayerFieldDevice.Resolve(origin + new Vector3(4, -3), origin + new Vector3(4, 3)), Is.Null);
            var blocker = wall.GetComponent<BoxCollider2D>();
            foreach (string layer in new[] { "Player", "Bullet", "PlayerBulletCollider" })
                Assert.That(blocker.excludeLayers.value & (1 << LayerMask.NameToLayer(layer)), Is.Not.Zero, layer + " passes");
            Assert.That(blocker.excludeLayers.value & (1 << LayerMask.NameToLayer("Enemy")), Is.Zero);
            var beacon = beaconObject.AddComponent<PlayerFieldDevice>();
            beacon.Initialize(PlayerFieldDeviceKind.Beacon, origin + Vector3.up * 3, origin + Vector3.up * 3, .3f, 80, 6, 7);
            Assert.That(PlayerFieldDevice.Resolve(origin + Vector3.down * 3, origin + Vector3.down * 4), Is.EqualTo(wall));
            wall.TakeDamage(300);
            Assert.That(PlayerFieldDevice.Resolve(origin + Vector3.down * 3, origin + Vector3.down * 4), Is.EqualTo(beacon));
            beacon.TakeDamage(80);
            Assert.That(PlayerFieldDevice.Resolve(origin + Vector3.down * 3, origin + Vector3.up * 3), Is.Null);
        }
        finally
        {
            if (wallObject != null) Object.DestroyImmediate(wallObject);
            if (beaconObject != null) Object.DestroyImmediate(beaconObject);
        }
    }

    [Test]
    public void EnemyAdapter_RestoresOriginalBehaviourAndDoesNotReplaceItsTarget()
    {
        Vector3 origin = new Vector3(4000, 4000, 0);
        var enemyObject = new GameObject("Existing enemy");
        var wallObject = new GameObject("Temporary wall");
        var originalGoal = new GameObject("Original enemy goal");
        try
        {
            var enemy = enemyObject.AddComponent<WalkAndAttackMonster>();
            enemy.maxHealth = enemy.currentHealth = 100;
            enemy.attackDamage = 10;
            enemy.attackRange = .4f;
            enemy.attackCooldown = 1;
            var serialized = new SerializedObject(enemy);
            serialized.FindProperty("currentTarget").objectReferenceValue = originalGoal.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            enemyObject.transform.position = origin + Vector3.down * .25f;
            var wall = wallObject.AddComponent<PlayerFieldDevice>();
            wall.Initialize(PlayerFieldDeviceKind.Barrier, origin + Vector3.left, origin + Vector3.right, .3f, 300, 8);
            Physics2D.SyncTransforms();
            var adapter = enemyObject.AddComponent<PlayerFieldEnemyAdapter>();
            adapter.SendMessage("Awake");
            adapter.SendMessage("LateUpdate");
            Assert.That(enemy.enabled, Is.False, "Only the temporary adapter controls the blocked enemy");
            Assert.That(wall.Health, Is.EqualTo(290), "The enemy attacks the blocking wall");
            Assert.That(new SerializedObject(enemy).FindProperty("currentTarget").objectReferenceValue, Is.EqualTo(originalGoal.transform));
            wall.TakeDamage(300);
            adapter.SendMessage("LateUpdate");
            Assert.That(enemy.enabled, Is.True, "Original behaviour resumes after wall destruction");
            enemy.enabled = false;
            adapter.SendMessage("LateUpdate");
            Assert.That(enemy.enabled, Is.False, "An already disabled enemy stays disabled");
        }
        finally
        {
            if (wallObject != null) Object.DestroyImmediate(wallObject);
            Object.DestroyImmediate(enemyObject);
            Object.DestroyImmediate(originalGoal);
        }
    }

    [Test]
    public void PlayerPrefab_AssignsThirdAndFourthSkills()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        foreach (var component in new MonoBehaviour[] { player.GetComponent<PlayerBarrierSkill>(), player.GetComponent<PlayerBeaconSkill>() })
        {
            Assert.That(component, Is.Not.Null);
            Assert.That(new SerializedObject(component).FindProperty("_prefab").objectReferenceValue, Is.Not.Null);
        }
        var loadout = new PlayerSkillLoadout();
        Assert.That(loadout.TrySelect(PlayerSkillId.EnergyBarrier), Is.True);
        Assert.That(loadout.TrySelect(PlayerSkillId.LureBeacon), Is.True);
        Assert.That(loadout.Q, Is.EqualTo(PlayerSkillId.EnergyBarrier));
        Assert.That(loadout.E, Is.EqualTo(PlayerSkillId.LureBeacon));
        Assert.That(loadout.TrySelect((PlayerSkillId)99), Is.False);
    }
}
