using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Content;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public sealed class PlayerTurretRepairTests
{
    private readonly List<Object> _objects = new();
    private static readonly Vector3 Origin = new(50000f, 50000f, 0f);
    private PlayerTurretRepair _repair;
    private PlayerInputHandler _input;
    private PlayerControlState _controls;
    private Rigidbody2D _body;
    private FieldTurretDefinition _definition;
    private float _timeScale;
    private bool _hasTimeScaleSnapshot;

    [SetUp]
    public void SetUp()
    {
        _timeScale = Time.timeScale;
        _hasTimeScaleSnapshot = true;
        Time.timeScale = 1f;
        // Use temporary objects away from the game map, like other EditMode
        // physics tests. Do not create/save/replace the user's open scenes.
        var player = Track(new GameObject("Hold repair test player"));
        player.transform.position = Origin;
        _body = player.AddComponent<Rigidbody2D>();
        _body.bodyType = RigidbodyType2D.Kinematic;
        _repair = player.AddComponent<PlayerTurretRepair>();
        _input = player.GetComponent<PlayerInputHandler>();
        _controls = player.GetComponent<PlayerControlState>();
        // EditMode tests explicitly initialize components; no gameplay Update is run.
        Invoke(_repair, "Awake");
        Invoke(_repair, "OnEnable");
        _definition = Track(ScriptableObject.CreateInstance<FieldTurretDefinition>());
        SetField(_definition, "_id", "hold-repair-" + Guid.NewGuid());
        SetField(_definition, "_maxHealth", 100);
    }

    [TearDown]
    public void TearDown()
    {
        try
        {
            foreach (Object value in _objects)
                if (value is RepairTestFieldTurret turret) FieldTurretInstanceRegistry.Unregister(turret);
            for (int i = _objects.Count - 1; i >= 0; i--)
                if (_objects[i] != null) Object.DestroyImmediate(_objects[i]);
        }
        finally
        {
            _objects.Clear();
            // Failed initialization/cleanup must never leave the project paused.
            // Restore only a value actually captured before this test changed it.
            if (_hasTimeScaleSnapshot) Time.timeScale = _timeScale;
            _hasTimeScaleSnapshot = false;
        }
    }

    [Test]
    public void RepairReservesRUntilReleaseEvenWhenCompleted()
    {
        CreateTurret(99);
        Tick(true, 1f);
        Assert.That(_repair.IsRepairing, Is.False);
        Assert.That(_repair.ConsumesRepairInput, Is.True);
        Tick(false, 0f);
        Assert.That(_repair.ConsumesRepairInput, Is.False);
    }

    [Test]
    public void PressingRAwayFromTurretsLeavesInputForReload()
    {
        Tick(true, .1f);
        Assert.That(_repair.RequiresRelease, Is.True);
        Assert.That(_repair.ConsumesRepairInput, Is.False);
    }

    [TestCase(100, 30)]
    [TestCase(100, 60)]
    [TestCase(200, 120)]
    public void HoldingForOneSecondRepairsTenPercentOfMaximumHealth(int maximum, int ticks)
    {
        SetField(_definition, "_maxHealth", maximum);
        var turret = CreateTurret(maximum / 2);
        for (int i = 0; i < ticks; i++) Tick(true, 1f / ticks);
        Assert.That(turret.CurrentHealth, Is.EqualTo(maximum / 2 + maximum / 10));
        Assert.That(_repair.IsRepairing, Is.True);
        Tick(false, 1f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(maximum / 2 + maximum / 10));
        Assert.That(_repair.IsRepairing, Is.False);
    }

    [TestCase("input")]
    [TestCase("velocity")]
    [TestCase("position")]
    public void MovementCancelsImmediatelyAndRequiresReleasingTheKey(string movement)
    {
        var turret = CreateTurret(50);
        Tick(true, .5f);
        if (movement == "input") SetField(_input, "<MoveInput>k__BackingField", Vector2.right);
        if (movement == "velocity") _body.linearVelocity = Vector2.right;
        if (movement == "position") _repair.transform.position += Vector3.right * .1f;
        Tick(true, .5f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(55));
        Assert.That(_repair.IsRepairing, Is.False);
        Assert.That(_repair.RequiresRelease, Is.True);
        SetField(_input, "<MoveInput>k__BackingField", Vector2.zero);
        _body.linearVelocity = Vector2.zero;
        Tick(true, 1f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(55), "Stopping must not resume a held R gesture.");
        Tick(false, .1f);
        Tick(true, .5f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void LeavingRangeAndReturningWithRHeldDoesNotResumeRepair()
    {
        var turret = CreateTurret(50);
        Tick(true, .5f);
        _repair.transform.position = Origin + new Vector3(10f, 0f);
        Tick(true, 1f);
        Assert.That(_repair.Target, Is.Null);
        _repair.transform.position = Origin;
        Tick(true, 1f);
        Tick(true, 1f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(55));
        Tick(false, .1f);
        Tick(true, .5f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void RepairTargetStaysFixedUntilReleaseEvenWhenAnotherTurretBecomesCloser()
    {
        var first = CreateTurret(50, new Vector2(2f, 0f));
        Tick(true, .5f);
        var second = CreateTurret(40, Vector2.right);
        Tick(true, .5f);
        Assert.That(first.CurrentHealth, Is.EqualTo(60));
        Assert.That(second.CurrentHealth, Is.EqualTo(40));
        Assert.That(_repair.Target, Is.SameAs(first));
        Tick(false, .1f);
        Tick(true, .5f);
        Assert.That(second.CurrentHealth, Is.EqualTo(45));
    }

    [Test]
    public void ReachingFullHealthDoesNotTransferTheHeldGestureToAnotherTurret()
    {
        var first = CreateTurret(99, Vector2.right);
        var second = CreateTurret(40, new Vector2(2f, 0f));
        Tick(true, 1f);
        Tick(true, 1f);
        Assert.That(first.CurrentHealth, Is.EqualTo(100));
        Assert.That(second.CurrentHealth, Is.EqualTo(40));
        Assert.That(_repair.IsRepairing, Is.False);
        Assert.That(_repair.RequiresRelease, Is.True);
    }

    [Test]
    public void FullAndLockedTurretsDoNotHealWhileDestroyedTurretAccumulatesOnlyProgress()
    {
        var full = CreateTurret(100);
        Tick(false, .1f);
        Assert.That(_repair.Target, Is.SameAs(full));
        Assert.That(_repair.CanRepair, Is.False);
        var locked = CreateTurret(50, Vector2.up, true);
        var destroyed = CreateTurret(0, Vector2.down);
        Tick(true, 1f);
        Assert.That(full.CurrentHealth, Is.EqualTo(100));
        Assert.That(locked.CurrentHealth, Is.EqualTo(50));
        Assert.That(destroyed.CurrentHealth, Is.Zero);
        Assert.That(_repair.Target, Is.SameAs(destroyed));
        Assert.That(_repair.IsRestoring, Is.True);
        Assert.That(_repair.Progress, Is.EqualTo(.1f).Within(.00001f));
    }

    [TestCase(30)]
    [TestCase(60)]
    [TestCase(120)]
    public void DestroyedTurretRestoresOnceAfterTenSecondsAndStaysOff(int ticksPerSecond)
    {
        var turret = CreateTurret(0);
        int restored = 0;
        Action<FieldTurretBase> onRestored = value => { if (value == turret) restored++; };
        FieldTurretInstanceRegistry.Restored += onRestored;
        try
        {
            for (int i = 0; i < ticksPerSecond * 10 - 1; i++) Tick(true, 1f / ticksPerSecond);
            Assert.That(turret.IsDestroyed, Is.True);
            Assert.That(turret.CurrentHealth, Is.Zero);
            Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.Destroyed));
            Assert.That(_repair.Progress, Is.GreaterThan(.99f).And.LessThan(1f));
            Tick(true, 1f / ticksPerSecond);
            Assert.That(turret.IsDestroyed, Is.False);
            Assert.That(turret.CurrentHealth, Is.EqualTo(turret.MaxHealth));
            Assert.That(turret.IsActivated, Is.False);
            Assert.That(restored, Is.EqualTo(1));
            Assert.That(_repair.RequiresRelease, Is.True);
            Tick(true, 1f);
            Assert.That(restored, Is.EqualTo(1));
        }
        finally { FieldTurretInstanceRegistry.Restored -= onRestored; }
    }

    [Test]
    public void MovementCancelsRestorationWithoutLosingProgressAndRequiresNewPress()
    {
        var turret = CreateTurret(0);
        Tick(true, 2f);
        SetField(_input, "<MoveInput>k__BackingField", Vector2.right);
        Tick(true, 1f);
        Assert.That(_repair.IsRepairing, Is.False);
        Assert.That(_repair.Progress, Is.EqualTo(.2f).Within(.00001f));
        SetField(_input, "<MoveInput>k__BackingField", Vector2.zero);
        Tick(true, 1f);
        Assert.That(_repair.Progress, Is.EqualTo(.2f).Within(.00001f));
        Tick(false, .1f);
        Tick(true, 3f);
        Assert.That(_repair.Progress, Is.EqualTo(.5f).Within(.00001f));
        Assert.That(turret.CurrentHealth, Is.Zero);
    }

    [Test]
    public void RestorationIsSharedByControllersAndClearedAfterRestoreOrNewDestruction()
    {
        var turret = CreateTurret(0);
        Tick(true, 2f);
        var second = Track(new GameObject("Second repair test player")).AddComponent<PlayerTurretRepair>();
        second.transform.position = Origin;
        Invoke(second, "Awake");
        Invoke(second, "OnEnable");
        Invoke(second, "Tick", true, 3f);
        Assert.That(second.Progress, Is.EqualTo(.5f).Within(.00001f));
        Assert.That(_repair.Progress, Is.EqualTo(.5f).Within(.00001f));
        Assert.That(turret.Restore(), Is.True);
        Assert.That(turret.ApplyDamage(turret.MaxHealth), Is.True);
        Assert.That(PlayerTurretRestoreProgress.GetProgress(turret), Is.Zero);
    }

    [Test]
    public void DestructionDuringNormalRepairRequiresNewPressBeforeRestoring()
    {
        var turret = CreateTurret(50);
        Tick(true, .5f);
        turret.ApplyDamage(turret.CurrentHealth);
        Tick(true, 1f);
        Assert.That(_repair.IsRepairing, Is.False);
        Assert.That(_repair.RequiresRelease, Is.True);
        Assert.That(_repair.Progress, Is.Zero);
        Tick(false, .1f);
        Tick(true, 1f);
        Assert.That(_repair.Progress, Is.EqualTo(.1f).Within(.00001f));
        Assert.That(turret.CurrentHealth, Is.Zero);
    }

    [Test]
    public void ChangingTargetsDoesNotTransferRestorationProgress()
    {
        var first = CreateTurret(0, Vector2.right);
        var second = CreateTurret(0, Vector2.right * 2f);
        Tick(true, 2f);
        first.transform.position = Origin + Vector3.right * 10f;
        Tick(true, 1f);
        Assert.That(_repair.Target, Is.SameAs(second));
        Assert.That(_repair.Progress, Is.Zero);
        Tick(false, .1f);
        Tick(true, 1f);
        Assert.That(_repair.Progress, Is.EqualTo(.1f).Within(.00001f));
        Assert.That(PlayerTurretRestoreProgress.GetProgress(first), Is.EqualTo(.2f).Within(.00001f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void InputLockOrFieldPauseStopsRepairWithoutResumingOnUnlock(bool pause)
    {
        var turret = CreateTurret(50);
        Tick(true, .5f);
        if (pause) Time.timeScale = 0f;
        else _controls.SetInputBlocked(true);
        Tick(true, 1f);
        Assert.That(_repair.IsRepairing, Is.False);
        Time.timeScale = 1f;
        _controls.SetInputBlocked(false);
        Tick(true, 1f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(55));
        Tick(false, .1f);
        Tick(true, .5f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void SolidObstacleBlocksRepairWhileTriggerDoesNot()
    {
        var turret = CreateTurret(50, new Vector2(2f, 0f));
        var obstacle = Track(new GameObject("Repair line of sight obstacle")).AddComponent<BoxCollider2D>();
        obstacle.transform.position = Origin + Vector3.right;
        obstacle.size = new Vector2(.2f, 1f);
        Physics2D.SyncTransforms();
        Tick(true, 1f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(50));
        obstacle.isTrigger = true;
        Physics2D.SyncTransforms();
        Tick(false, .1f);
        Tick(true, 1f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(60));
    }

    [Test]
    public void PlayerAndHudPrefabsContainAuthoredRepairComponents()
    {
        var player = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");
        var hud = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab");
        Assert.That(player.GetComponent<PlayerTurretRepair>(), Is.Not.Null);
        Assert.That(hud.GetComponent<PlayerTurretRepairPromptView>(), Is.Not.Null);
        Assert.That(hud.GetComponent<PlayerTurretRepairPromptView>().IsConfigured, Is.True);
        var serialized = new SerializedObject(hud.GetComponent<PlayerTurretRepairPromptView>());
        Assert.That(serialized.FindProperty("_progressFill").objectReferenceValue, Is.Not.Null);
        var panel = (RectTransform)serialized.FindProperty("_panel").objectReferenceValue;
        foreach (var graphic in panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
            Assert.That(graphic.raycastTarget, Is.False, graphic.name + " must not consume attack clicks.");
    }

    [Test]
    public void AddingPresenceWithoutAuthorityAfterAwakePreventsFurtherRepair()
    {
        var turret = CreateTurret(50);
        Tick(true, .5f);
        _repair.gameObject.AddComponent<PlayerMatchPresence>();
        Tick(true, 1f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(55));
        Assert.That(_repair.IsRepairing, Is.False);
        Assert.That(_repair.Target, Is.Null);
    }

    [Test]
    public void RepairPromptTracksCameraOutputReplacementAndUnsubscribesWhenDisabled()
    {
        var root = Track(new GameObject("Repair prompt camera test"));
        root.SetActive(false);
        var view = root.AddComponent<PlayerTurretRepairPromptView>();
        var cameras = new List<MatchCameraFollow>();
        Invoke(view, "OnEnable");
        try
        {
            for (int i = 0; i < 2; i++)
            {
                var output = Track(new GameObject("Repair prompt output " + i));
                var camera = output.AddComponent<Camera>();
                var follow = output.AddComponent<MatchCameraFollow>();
                if (follow.Manager == null) Invoke(follow, "Awake");
                if (follow.Manager.OutputCamera == null) Invoke(follow.Manager, "Awake");
                cameras.Add(follow);
                follow.Bind(_repair.transform);
                Assert.That(typeof(PlayerTurretRepairPromptView).GetField("_worldCamera",
                    BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view), Is.SameAs(camera));
            }

            Invoke(view, "OnDisable");
            cameras[0].Bind(_repair.transform);
            Assert.That(typeof(PlayerTurretRepairPromptView).GetField("_worldCamera",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(view), Is.Null,
                "A disabled prompt must not retain camera output subscriptions.");
        }
        finally
        {
            Invoke(view, "OnDisable");
            for (int i = cameras.Count - 1; i >= 0; i--) Invoke(cameras[i].Manager, "OnDisable");
        }
    }

    [TestCase(3.2f)]
    [TestCase(-1.4f)]
    public void RepairRangeUsesFeetFromEitherSideOfTheTurret(float rootHeight)
    {
        var turret = CreateTurret(50, Vector2.zero);
        AddGroundColliders();
        _repair.transform.position = Origin + Vector3.up * rootHeight;
        Invoke(_repair, "Awake");
        Invoke(_repair, "OnEnable");
        Physics2D.SyncTransforms();
        Tick(false, .1f);
        Assert.That(_repair.Target, Is.SameAs(turret));
        Assert.That(_repair.CanRepair, Is.True);
        Tick(true, .5f);
        Assert.That(turret.CurrentHealth, Is.EqualTo(55));
    }

    [Test]
    public void RepairLineOfSightUsesTheSameGroundOriginAsItsRange()
    {
        var turret = CreateTurret(50, Vector2.right * 2);
        var wall = Track(new GameObject("Low obstacle at ground level")).AddComponent<BoxCollider2D>();
        _repair.transform.position = Origin + Vector3.up;
        wall.transform.position = Origin + Vector3.right;
        wall.size = new Vector2(.2f, .2f);
        Physics2D.SyncTransforms();
        Assert.That(PlayerSpannerTargeting.CanReach(_repair.transform, turret.transform, turret.transform.position,
            Vector2.right, 3f, 180f, (Vector2)Origin), Is.False);
        wall.isTrigger = true;
        Physics2D.SyncTransforms();
        Assert.That(PlayerSpannerTargeting.CanReach(_repair.transform, turret.transform, turret.transform.position,
            Vector2.right, 3f, 180f, (Vector2)Origin), Is.True);
    }

    [TestCase(1f, 0f, PlayerAimFacing.Right)]
    [TestCase(-1f, 0f, PlayerAimFacing.Left)]
    [TestCase(0f, 1f, PlayerAimFacing.Up)]
    [TestCase(0f, -1f, PlayerAimFacing.Down)]
    [TestCase(1f, 1f, PlayerAimFacing.UpRight)]
    [TestCase(-1f, 1f, PlayerAimFacing.UpLeft)]
    [TestCase(1f, -1f, PlayerAimFacing.DownRight)]
    [TestCase(-1f, -1f, PlayerAimFacing.DownLeft)]
    public void WorkingFacesTheTurretWithoutChangingMouseAimAndReleasesOnCancel(float x, float y, PlayerAimFacing facing)
    {
        var direction = new Vector2(x, y).normalized;
        CreateTurret(50, direction * 2f);
        SetField(_input, "<AimDirection>k__BackingField", -direction);
        Tick(true, .5f);
        Assert.That(_repair.TryGetFacingDirection(out Vector2 workDirection), Is.True);
        Assert.That(PlayerAimUtility.GetEightWayFacing(workDirection), Is.EqualTo(facing));
        Assert.That(_input.AimDirection, Is.EqualTo(-direction));
        SetField(_input, "<MoveInput>k__BackingField", Vector2.right);
        Tick(true, .1f);
        Assert.That(_repair.TryGetFacingDirection(out _), Is.False);
    }

    private void AddGroundColliders()
    {
        var selector = _repair.gameObject.AddComponent<PlayerDirectionalColliders>();
        Type pairType = typeof(PlayerDirectionalColliders).GetNestedType("DirectionColliders", BindingFlags.NonPublic);
        Array pairs = Array.CreateInstance(pairType, 8);
        for (int i = 0; i < 8; i++)
        {
            var bodyObject = new GameObject("Test feet " + i);
            bodyObject.transform.SetParent(_repair.transform, false);
            var body = bodyObject.AddComponent<BoxCollider2D>();
            body.offset = new Vector2(0, -.9f);
            body.size = new Vector2(.5f, .5f);
            var bullet = bodyObject.AddComponent<BoxCollider2D>();
            bullet.isTrigger = true;
            body.enabled = bullet.enabled = i == 0;
            object pair = Activator.CreateInstance(pairType, true);
            pairType.GetField("facing").SetValue(pair, (PlayerAimFacing)i);
            pairType.GetField("body").SetValue(pair, body);
            pairType.GetField("bullet").SetValue(pair, bullet);
            pairs.SetValue(pair, i);
        }
        SetField(selector, "_directions", pairs);
        Assert.That(selector.IsConfigured, Is.True);
    }

    private RepairTestFieldTurret CreateTurret(int health, Vector2? position = null, bool locked = false)
    {
        var root = Track(new GameObject("Hold repair test turret"));
        root.SetActive(false);
        var turret = Track(root.AddComponent<RepairTestFieldTurret>());
        SetField(turret, "_definition", _definition, typeof(FieldTurretBase));
        turret.PrepareInitialization(new FieldTurretInitializationData(_definition, health, isLocked: locked), new Power());
        Assert.That(turret.Initialize(), Is.True);
        root.transform.position = Origin + (Vector3)(position ?? Vector2.right);
        root.SetActive(true);
        FieldTurretInstanceRegistry.Register(turret);
        return turret;
    }

    private void Tick(bool held, float seconds) => Invoke(_repair, "Tick", held, seconds);
    private static void Invoke(object target, string method, params object[] args) => target.GetType()
        .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void SetField(object target, string field, object value, Type owner = null) =>
        (owner ?? target.GetType()).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private T Track<T>(T value) where T : Object { _objects.Add(value); return value; }
    private sealed class Power : IFieldTurretPowerSource
    {
        public int CurrentPower => 100;
        public int MaximumPower => 100;
        public event Action<int, int> PowerChanged { add { } remove { } }
        public bool TryConsumePower(int power) => true;
        public bool TryChangeReservation(int previousPower, int newPower) => true;
        public void ReleasePower(int power) { }
    }
}

public sealed class RepairTestFieldTurret : FieldTurretBase
{
    public bool Initialize() => ConfigureSceneActivation();
}
