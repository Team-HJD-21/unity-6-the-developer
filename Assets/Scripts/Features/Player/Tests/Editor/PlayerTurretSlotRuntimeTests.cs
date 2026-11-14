using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Infrastructure;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

public sealed class PlayerTurretSlotRuntimeTests
{
    private Scene _scene;
    private GameObject _player;
    private PlayerSkillController _controller;
    private PlayerTurretSkill _skill;
    private PlayerSkillHud _hud;
    private Tile _tile;
    private Mouse _mouse;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static readonly Vector3 Origin = new(26000, 26000);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _mouse = InputSystem.AddDevice<Mouse>();
        MovePointer(new Vector2(Screen.width * .5f, Screen.height * .7f));
        _scene = SceneManager.CreateScene("Player turret slots");
        var grid = Create("Land Grid"); grid.AddComponent<Grid>(); grid.transform.position = Origin;
        var mapObject = Create("Land"); mapObject.transform.SetParent(grid.transform, false);
        var map = mapObject.AddComponent<Tilemap>(); mapObject.AddComponent<TilemapCollider2D>();
        _tile = ScriptableObject.CreateInstance<Tile>(); _tile.colliderType = Tile.ColliderType.None;
        for (int y = -15; y <= 15; y++) for (int x = -15; x <= 15; x++) map.SetTile(new Vector3Int(x, y), _tile);
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), Origin, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(_player, _scene);
        _controller = _player.GetComponent<PlayerSkillController>(); _skill = _player.GetComponent<PlayerTurretSkill>();
        var data = new SerializedObject(_skill); data.FindProperty("_placementGround").objectReferenceValue = map; data.ApplyModifiedPropertiesWithoutUndo();
        var hudObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab"));
        SceneManager.MoveGameObjectToScene(hudObject, _scene); _hud = hudObject.GetComponent<PlayerSkillHud>();
        _hud.GetComponent<PlayerSkillHudSceneBinding>().enabled = false;
        Assert.That(_controller.BindHud(_hud), Is.True);
        Assert.That(_controller.TrySelect(PlayerSkillId.PlayerTurret), Is.True);
        yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        if (_tile != null) Object.DestroyImmediate(_tile);
        if (_mouse != null) InputSystem.RemoveDevice(_mouse);
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    private GameObject Create(string name)
    { var go = new GameObject(name); SceneManager.MoveGameObjectToScene(go, _scene); return go; }
    private void MovePointer(Vector2 position)
    { InputSystem.QueueStateEvent(_mouse, new MouseState { position = position }); InputSystem.Update(); }
    private PlayerTurret Deploy(int slot, Vector2 direction)
    {
        _skill.CancelPlacement(); _skill.Tick(_skill.SkillCooldown);
        Assert.That(_controller.TryUse(PlayerSkillId.PlayerTurret), Is.True);
        Assert.That(_skill.IsSelecting && !_skill.IsPlacing, Is.True);
        Assert.That(_skill.TrySelectSlot(slot), Is.True);
        Assert.That(_skill.TryConfirmPlacement(Origin + (Vector3)direction * 4), Is.True);
        return Find(slot);
    }
    private PlayerTurret Find(int slot) => Object.FindObjectsByType<PlayerTurret>(FindObjectsSortMode.None)
        .Single(t => t.gameObject.scene == _scene && t.SlotIndex == slot);

    [UnityTest]
    public IEnumerator SlotCardsRequireSelectionShowIndependentStatesAndPreserveRecoveredHealth()
    {
        Assert.That(_controller.TryUse(PlayerSkillId.PlayerTurret), Is.True, "canUse=" + _controller.CanUse +
            ", selection=" + _controller.IsSelectionOpen + ", controls=" + _player.GetComponent<PlayerControlState>().CanAct +
            ", scale=" + Time.timeScale + ", ready=" + _skill.IsReady + ", equipped=" + _controller.Loadout.Contains(PlayerSkillId.PlayerTurret));
        Assert.That(_skill.IsSelecting, Is.True); Assert.That(_skill.IsPlacing, Is.False);
        Assert.That(_controller.IsPlacingSkill, Is.True);
        MovePointer(new Vector2(Screen.width * .5f, 5));
        yield return null;
        Assert.That(_hud.IsOpen, Is.False, "The hover picker must not steal the turret slot selection.");
        Assert.That(_skill.IsSelecting, Is.True);
        var panel = _hud.GetComponent<PlayerTurretSlotPanel>(); panel.Refresh();
        Assert.That(panel.IsConfigured, Is.True);
        var data = new SerializedObject(panel);
        ((Button)data.FindProperty("_buttons").GetArrayElementAtIndex(2).objectReferenceValue).onClick.Invoke();
        Assert.That(_skill.SelectedSlotIndex, Is.EqualTo(2)); Assert.That(_skill.IsPlacing, Is.True);
        _skill.CancelPlacement(); Assert.That(_skill.AvailableCount, Is.EqualTo(3));
        var recovered = Deploy(2, Vector2.right); recovered.Health.TakeDamage(50);
        Assert.That(recovered.TryRecover(_skill), Is.True);
        var deployed = Deploy(0, Vector2.down);
        var destroyed = Deploy(1, Vector2.up); destroyed.Health.TakeDamage(100);
        _skill.Tick(_skill.SkillCooldown);
        Assert.That(_controller.TryUse(PlayerSkillId.PlayerTurret), Is.True); panel.Refresh();
        Assert.That(panel.Select(0), Is.False); Assert.That(panel.Select(1), Is.False);
        Assert.That(_skill.GetSlot(2).HealthRatio, Is.EqualTo(.5f));
        deployed.Health.ApplyDamage(40); panel.Refresh();
        string Text(int i) => ((TMPro.TMP_Text)data.FindProperty("_statuses").GetArrayElementAtIndex(i).objectReferenceValue).text;
        Assert.That(Text(0), Does.Contain("DEPLOYED").And.Contain("60%"));
        Assert.That(Text(1), Does.Contain("REBUILD"));
        Assert.That(Text(2), Does.Contain("READY").And.Contain("50%"));
        yield return null;
        Assert.That(panel.Select(2), Is.True);
        Assert.That(_skill.TryConfirmPlacement(Origin + Vector3.right * 4), Is.True);
        Assert.That(Find(2).Health.Health, Is.EqualTo(50).Within(.001f));
    }

    [UnityTest]
    public IEnumerator EnemyTargetingAndRealMeleeDamageUseTheFieldTurretContractWithoutPower()
    {
        int controllers = Object.FindObjectsByType<FieldTurretController>(FindObjectsSortMode.None).Length;
        var turret = Deploy(0, Vector2.right);
        yield return null;
        var target = turret.GetComponent<TargetableComponent>();
        Assert.That(target.CanBeTargeted, Is.True); Assert.That(target.TargetType, Is.EqualTo(TargetType.Turret));
        Assert.That(TeamHJD.Game.Enemies.Targeting.EnemyTargetSelector.TryFindNearest(Origin,
            new[] { target }, TargetType.Turret, out var chosen), Is.True);
        Assert.That(chosen.TargetTransform, Is.EqualTo(turret.transform));
        var enemy = Create("Melee attacker"); enemy.transform.position = turret.Health.ApproachPoint(Origin) + Vector2.left * .1f;
        enemy.AddComponent<BoxCollider2D>().size = Vector2.one * .3f;
        enemy.AddComponent<EnemyTargeting>().SetTarget(turret.transform);
        var ability = enemy.AddComponent<EnemyCloseRangeAbility>();
        Physics2D.SyncTransforms();
        Assert.That(ability.TryUse(), Is.True); Assert.That(turret.Health.Health, Is.EqualTo(95));
        Assert.That(turret.GetComponent<IDamageableTarget>(), Is.Not.Null);
        Assert.That(turret.GetComponent<FieldTurretBase>(), Is.Null);
        Assert.That(Object.FindObjectsByType<FieldTurretController>(FindObjectsSortMode.None).Length, Is.EqualTo(controllers));
        Assert.That(turret.Health.ApplyDamage(100), Is.True);
        Assert.That(target.CanBeTargeted, Is.False);
        Assert.That(_skill.GetSlot(0).Status, Is.EqualTo(PlayerTurretInventory.SlotStatus.Rebuilding));
        _skill.Tick(30);
        Assert.That(_skill.GetSlot(0).Status, Is.EqualTo(PlayerTurretInventory.SlotStatus.Available));
        Assert.That(_skill.GetSlot(0).HealthRatio, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator EnemyProjectilePhysicallyHitsTheTurretAndUpdatesItsSlotHealth()
    {
        var turret = Deploy(0, Vector2.right);
        var point = turret.Health.ApproachPoint(Origin);
        var projectileObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Enemies/Runtime/Projectile/PlantLeaferProjectile.prefab"),
            (Vector3)(point + Vector2.left * .5f), Quaternion.identity);
        SceneManager.MoveGameObjectToScene(projectileObject, _scene);
        var projectile = projectileObject.GetComponent<EnemyDamageProjectile>();
        var data = new SerializedObject(projectile); data.FindProperty("projectileSpeed").floatValue = 10; data.ApplyModifiedPropertiesWithoutUndo();
        projectile.Initialize(13, Vector2.right); Physics2D.SyncTransforms();
        float until = Time.realtimeSinceStartup + 1.5f;
        while (Time.realtimeSinceStartup < until && turret.Health.Health == 100) yield return null;
        Assert.That(turret.Health.Health, Is.EqualTo(87));
        Assert.That(_skill.GetSlot(0).HealthRatio, Is.EqualTo(.87f).Within(.001f));
    }

    [UnityTest]
    public IEnumerator HeldRRepairsTheInstalledTurretAndRecoveryKeepsTheRepairedHealth()
    {
        var turret = Deploy(0, Vector2.down); turret.Health.TakeDamage(60);
        var repair = _player.GetComponent<PlayerTurretRepair>();
        _player.GetComponent<Rigidbody2D>().linearVelocity = Vector2.zero;
        Call(repair, "Tick", false, 0f);
        Assert.That(repair.PlayerTarget, Is.SameAs(turret.Health));
        Call(repair, "Tick", true, .5f);
        Assert.That(repair.IsRepairing, Is.True); Assert.That(repair.ConsumesRepairInput, Is.True);
        Assert.That(turret.Health.Health, Is.EqualTo(45).Within(.001f));
        Call(repair, "Tick", false, .5f);
        Assert.That(turret.Health.Health, Is.EqualTo(45).Within(.001f));
        Assert.That(repair.IsRepairing, Is.False);
        Assert.That(turret.TryRecover(_skill), Is.True);
        var restored = Deploy(0, Vector2.down);
        Assert.That(restored.Health.Health, Is.EqualTo(45).Within(.001f));
        Assert.That(restored.Health.Repair(float.NaN, 1), Is.EqualTo(FieldTurretRepairResult.InvalidRepairRate));
        yield break;
    }

    [UnityTest]
    public IEnumerator CheckpointRoundTripRestoresSlotIdentityHealthRebuildAndSelectionWithoutRefilling()
    {
        var recovered = Deploy(0, Vector2.down); recovered.Health.TakeDamage(60); recovered.TryRecover(_skill);
        var deployed = Deploy(1, Vector2.right); deployed.Health.TakeDamage(25);
        var destroyed = Deploy(2, Vector2.up); destroyed.Health.TakeDamage(100);
        _skill.Tick(3); _controller.TryUse(PlayerSkillId.PlayerTurret);
        var codec = new BinaryCheckpointPayloadCodec();
        var write = (Action<BinaryWriter>)Call(_skill, "CaptureCheckpointState", codec);
        var section = codec.Write("slot-test", 2, CheckpointSectionKind.World, write);
        Assert.That(((ICheckpointParticipant)_skill).Version, Is.EqualTo(2));
        deployed.Health.TakeDamage(10); _skill.Tick(30); _skill.CancelPlacement();
        using var step = codec.Read(section, 2, reader => (ICheckpointRestoreStep)Call(_skill, "PrepareCheckpointState", reader, codec));
        step.Apply();
        Assert.That(_skill.IsSelecting, Is.True);
        Assert.That(_skill.GetSlot(0).HealthRatio, Is.EqualTo(.4f).Within(.0001f));
        Assert.That(_skill.GetSlot(1).Status, Is.EqualTo(PlayerTurretInventory.SlotStatus.Deployed));
        Assert.That(Find(1).Health.Health, Is.EqualTo(75).Within(.001f));
        Assert.That(_skill.GetSlot(2).Status, Is.EqualTo(PlayerTurretInventory.SlotStatus.Rebuilding));
        Assert.That(_skill.GetSlot(2).Remaining, Is.InRange(26.9f, 27.1f));
        step.Rollback();
        Assert.That(Find(1).Health.Health, Is.EqualTo(65).Within(.001f));
        Assert.That(_skill.GetSlot(2).Status, Is.EqualTo(PlayerTurretInventory.SlotStatus.Available));
        yield break;
    }

    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Private).Invoke(target, args);
}
