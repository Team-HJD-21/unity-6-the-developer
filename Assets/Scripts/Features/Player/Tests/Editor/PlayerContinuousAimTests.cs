using System.Reflection;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerContinuousAimTests
{
    private GameObject _player;
    private PlayerWeaponVisual _visual;
    private PlayerWeaponRig _rig;
    private PlayerSpriteMotion _motion;
    private PlayerInputHandler _input;
    private PlayerWeaponSelection _selection;
    private PlayerMuzzleAnchor _anchor;
    private RepairPresentationRandom _presentationRandom;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;

    [SetUp]
    public void SetUp()
    {
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"));
        _input = _player.GetComponent<PlayerInputHandler>();
        _selection = _player.GetComponent<PlayerWeaponSelection>();
        _visual = _player.GetComponent<PlayerWeaponVisual>();
        _motion = _player.GetComponent<PlayerSpriteMotion>();
        _rig = _player.GetComponentInChildren<PlayerWeaponRig>(true);
        typeof(PlayerSpriteMotion).GetMethod("Awake", Private).Invoke(_motion, null);
        Set(_motion, "_ownsDisplay", true);
        typeof(PlayerWeaponVisual).GetMethod("Awake", Private).Invoke(_visual, null);
        _presentationRandom = new RepairPresentationRandom();
        _visual.BindPresentationRandom(_presentationRandom);
        typeof(PlayerWeaponRig).GetMethod("Initialize", Private).Invoke(_rig, null);
        _anchor = _player.GetComponentInChildren<PlayerMuzzleAnchor>(true);
        typeof(PlayerMuzzleAnchor).GetMethod("Awake", Private).Invoke(_anchor, null);
        Assert.That(_visual.UsesContinuousAim && _visual.IsConfigured && _motion.UsesSpaceManBody, Is.True);
    }

    [TearDown]
    public void TearDown() { if (_player != null) Object.DestroyImmediate(_player); }

    [TestCase(13f)] [TestCase(61f)] [TestCase(117f)] [TestCase(173f)]
    [TestCase(-24f)] [TestCase(-78f)] [TestCase(-136f)] [TestCase(-177f)]
    public void ContinuousAnglesKeepGunUprightAndMuzzleOnDisplayedWeapon(float angle)
    {
        Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
        Input("AimDirection", direction); Input("HasAimTarget", false);
        _motion.RefreshSpaceManBody();
        var body = _player.GetComponent<SpriteRenderer>();
        Assert.That(body.sprite.name, Is.EqualTo("Side_0"));
        Assert.That(body.flipX, Is.EqualTo(direction.x > 0));
        _visual.Refresh();
        Assert.That(Vector2.Dot(_visual.Renderer.transform.right, direction), Is.GreaterThan(.9999f));
        Assert.That(_visual.Renderer.flipY, Is.EqualTo(direction.x < 0));
        Assert.That(_visual.ForegroundHandsRenderer.enabled, Is.False);
        Assert.That(_visual.Renderer.sortingOrder, Is.GreaterThan(_player.GetComponent<SpriteRenderer>().sortingOrder));
        Assert.That(_visual.TryGetMuzzlePosition(out var expected), Is.True);
        Assert.That(_rig.TryGetMuzzle(out var actual), Is.True);
        Assert.That(Vector3.Distance(actual.position, expected), Is.LessThan(.0001f));
    }

    [TestCase(WeaponType.DefaultGun)] [TestCase(WeaponType.CryoBlaster)] [TestCase(WeaponType.Sniper)]
    public void SwitchingWeaponsAndFirstShotUseCurrentArtAndTargetFromHandMount(WeaponType weapon)
    {
        Set(_selection, "_currentWeapon", weapon);
        Vector2 target = (Vector2)_player.transform.position + new Vector2(-8, 5);
        Input("MouseWorldPosition", target); Input("HasAimTarget", true);
        Input("AimDirection", Vector2.down);
        Assert.That(_rig.TryGetMuzzle(out var muzzle, forShot: true), Is.True);
        Assert.That(_rig.TryGetContinuousAimDirection(out var direction), Is.True);
        Vector2 toTarget = target - (Vector2)muzzle.position;
        float cross = toTarget.x * direction.y - toTarget.y * direction.x;
        Assert.That(Mathf.Abs(cross), Is.LessThan(.001f), "The muzzle and projectile line should meet the actual pointer target.");
        Assert.That(_visual.Renderer.sprite.name, Is.EqualTo(weapon.ToString()));
        var before = muzzle.position;
        _motion.PlayRangedShot();
        Assert.That(_rig.TryGetMuzzle(out muzzle), Is.True);
        Assert.That(Vector3.Distance(muzzle.position, before), Is.LessThan(.0001f), "First-shot body state changes must not move the continuous gun's muzzle.");
    }

    [Test]
    public void SpaceshipHidesWeaponWhileBodyStillTurnsAndFieldReturnRestoresIt()
    {
        var controls = _player.GetComponent<PlayerControlState>();
        Set(_selection, "_currentWeapon", WeaponType.Sniper);
        _visual.Refresh();
        Assert.That(_visual.Renderer.enabled, Is.True);
        controls.BindHub(new TeamHJD.Game.Application.InputLockService());
        Assert.That(controls.CanMove, Is.True); Assert.That(controls.CanAct, Is.False);
        Input("AimDirection", Vector2.left); Input("HasAimTarget", false);
        _motion.RefreshSpaceManBody(); _visual.Refresh();
        Assert.That(_player.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Side_0"));
        Assert.That(_visual.Renderer.enabled, Is.False);
        Assert.That(_visual.ForegroundHandsRenderer.enabled, Is.False);
        Assert.That(_visual.TryGetMuzzlePosition(out _), Is.False);
        Assert.That(_visual.Renderer.enabled, Is.False, "A muzzle query must not show the weapon in the hub.");
        Input("AimDirection", Vector2.right);
        _motion.RefreshSpaceManBody();
        Assert.That(_player.GetComponent<SpriteRenderer>().flipX, Is.True);
        Input("AimDirection", Vector2.up);
        _motion.RefreshSpaceManBody();
        Assert.That(_player.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Side_0"));
        Assert.That(_player.GetComponent<SpriteRenderer>().flipX, Is.True);
        controls.SetInputBlocked(true);
        _motion.RefreshSpaceManBody(); _visual.Refresh();
        Assert.That(_player.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("Side_0"));
        Assert.That(_player.GetComponent<SpriteRenderer>().flipX, Is.True);
        Assert.That(_visual.TryGetContinuousAimDirection(out var lockedDirection), Is.True);
        Assert.That(lockedDirection, Is.EqualTo(Vector2.down));
        Assert.That(_visual.Renderer.enabled, Is.False);
        controls.SetInputBlocked(false);
        controls.UnbindHub();
        _visual.Refresh();
        Assert.That(_visual.Renderer.enabled, Is.True);
        Assert.That(_selection.CurrentWeapon, Is.EqualTo(WeaponType.Sniper));
        Assert.That(_visual.Renderer.sprite.name, Is.EqualTo("Sniper"));
    }

    [Test]
    public void SideBodyKeepsFootShadowAndCollisionPairAlignedAfterAnimatorEvaluation()
    {
        var shadow = _player.transform.Find("PlayerGroundShadow").GetComponentInChildren<SpriteRenderer>();
        var colliders = _player.GetComponentsInChildren<Collider2D>(true);
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Player/WeaponPosesV2/Idle_Down.anim");
        foreach (var direction in new[] { Vector2.left, Vector2.up, Vector2.right, Vector2.down })
        {
            Input("AimDirection", direction); Input("HasAimTarget", false);
            clip.SampleAnimation(_player, .4f);
            _motion.RefreshSpaceManBody();
            Physics2D.SyncTransforms();
            Collider2D body = null, bullet = null, solidBody = null;
            int activeCount = 0;
            foreach (var collider in colliders)
            {
                if (!collider.enabled || collider.GetComponent<PlayerEnemyDetector>() != null) continue;
                activeCount++;
                if (collider.isTrigger) bullet = collider;
                else if (collider is CapsuleCollider2D) body = collider;
                else solidBody = collider;
            }
            Assert.That(activeCount, Is.EqualTo(3));
            string facing = _motion.SpaceManFacing.ToString();
            Assert.That(body.name, Is.EqualTo(facing));
            Assert.That(bullet.name, Is.EqualTo(facing));
            Assert.That(solidBody.name, Is.EqualTo(facing));
            var capsule = (CapsuleCollider2D)body;
            Assert.That(capsule.size, Is.EqualTo(new Vector2(1.25f, .5f)));
            Vector3 center = body.bounds.center;
            Vector3 ellipse = shadow.transform.position;
            Assert.That(Vector2.Distance(center, ellipse), Is.LessThan(.0001f));
            Assert.That(_player.transform.InverseTransformPoint(center).y, Is.EqualTo(.10625f).Within(.0001f));
            Assert.That(bullet.OverlapPoint(_player.transform.TransformPoint(new Vector3(0, 2.1f))), Is.True);
            Assert.That(bullet.OverlapPoint(_player.transform.TransformPoint(new Vector3(2.5f, .65f))), Is.False);
            Assert.That(bullet.OverlapPoint(_player.transform.TransformPoint(new Vector3(1.55f, .1f))), Is.False,
                "Detached jetpack particles must stay outside the body hitbox.");
            CollectionAssert.AreEqual(((PolygonCollider2D)bullet).points, ((PolygonCollider2D)solidBody).points);
            Assert.That(shadow.sortingOrder, Is.LessThan(_player.GetComponent<SpriteRenderer>().sortingOrder));
        }
    }

    [TestCase(WeaponType.DefaultGun)] [TestCase(WeaponType.CryoBlaster)] [TestCase(WeaponType.Sniper)]
    public void FirePointAndFlashFollowTheBoreAcrossTurnsAndMovement(WeaponType weapon)
    {
        Set(_selection, "_currentWeapon", weapon);
        Input("HasAimTarget", false); Input("AimDirection", Vector2.right);
        Assert.That(_anchor.TryGetMuzzle(out var first), Is.True);
        _rig.ShowShotFlash(Color.white);
        var flash = _rig.ActiveGun.GetComponent<PlayerMuzzleFlash>();
        var effect = (GameObject)typeof(PlayerMuzzleFlash).GetField("_flashVisual", Private).GetValue(flash);
        foreach (var direction in new[] { Vector2.right, Vector2.up, Vector2.left, new Vector2(-1, -1).normalized })
        {
            _player.transform.position += new Vector3(.3f, -.2f);
            Input("AimDirection", direction);
            typeof(PlayerMuzzleAnchor).GetMethod("LateUpdate", Private).Invoke(_anchor, null);
            flash.RefreshMuzzlePose();
            Assert.That(_anchor.TryGetMuzzle(out var current), Is.True);
            Assert.That(current, Is.SameAs(first), "Continuous aim must keep the active flash when crossing a facing boundary.");
            Assert.That(effect.activeInHierarchy, Is.True);
            Assert.That(Vector3.Distance(effect.transform.position, current.position), Is.LessThan(.0001f));
            Assert.That(Vector3.Distance(_anchor.transform.position, current.position), Is.LessThan(.0001f));
            Assert.That(Vector2.Dot(effect.transform.right, direction), Is.GreaterThan(.9999f));
            Assert.That(Vector2.Dot(_anchor.transform.right, direction), Is.GreaterThan(.9999f));
        }
        var art = _visual.Renderer.sprite;
        var data = new SerializedObject(_visual).FindProperty(weapon == WeaponType.CryoBlaster
            ? "_aimCryoBlaster" : weapon == WeaponType.Sniper ? "_aimSniper" : "_aimDefaultGun");
        Vector2 tip = data.FindPropertyRelative("muzzle").vector2Value;
        int x = Mathf.RoundToInt(tip.x * art.rect.width) - 1;
        int y = Mathf.RoundToInt(tip.y * art.rect.height - .5f);
        var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        try
        {
            Assert.That(ImageConversion.LoadImage(source, System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(art))), Is.True);
            Assert.That(source.GetPixel(x, y).a, Is.GreaterThan(.5f), "The bore must meet the opaque weapon edge.");
            Assert.That(source.GetPixel(x + 1, y).a, Is.LessThan(.25f), "Only a faint antialias fringe may extend past the bore.");
        }
        finally { Object.DestroyImmediate(source); }
    }

    [Test]
    public void FlashFramesShareTheBoreOriginInsteadOfTheBodyPivot()
    {
        for (int i = 0; i < 4; i++)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Sprites/Player/Motions/V2/FX/Flash/Flash_{i}.png");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.pivot, Is.EqualTo(new Vector2(24, 48)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(96));
        }
    }

    [Test]
    public void AuthoredPoseFallbackRestoresScaleAndSeparateHandLayers()
    {
        Input("AimDirection", Vector2.right); Input("HasAimTarget", false);
        _visual.Refresh();
        Assert.That(_visual.Renderer.transform.localScale.x, Is.Not.EqualTo(1f));
        PlayerPresentationTestMode.UseAuthoredPoses(_player);
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Art/Animations/Player/WeaponPosesV2/Idle_Down.anim");
        clip.SampleAnimation(_player,0f);
        _visual.Refresh();
        Assert.That(_visual.Renderer.enabled, Is.True);
        Assert.That(_visual.Renderer.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(AssetDatabase.GetAssetPath(_visual.Renderer.sprite), Does.Contain("/SeparatedWeapons/"));
    }

    [Test]
    public void SourceSheetAndDerivedBodyHaveSeparateStableSpriteReferences()
    {
        Assert.That(AssetDatabase.LoadAllAssetsAtPath("Assets/Art/Sprites/Player/Indicators/space man.png").Length, Is.GreaterThanOrEqualTo(16));
        foreach (string name in new[] {"Down", "Up", "Side"})
        for (int i=0;i<5;i++)
        {
            var sprite=AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Sprites/Player/SpaceManBody/{name}_{i}.png");
            Assert.That(sprite, Is.Not.Null);
            Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(160,160)));
            Assert.That(sprite.pixelsPerUnit, Is.EqualTo(32));
            Assert.That(Vector2.Distance(sprite.pivot, new Vector2(80,41.6f)), Is.LessThan(.001f));
        }
    }

    [TestCase(0f, 2f)] [TestCase(0f, -2f)] [TestCase(2f, 0f)] [TestCase(-2f, 0f)]
    public void RepairSpannerSortsAboveTurretFromEverySideAndRestoresActorSorting(float x, float y)
    {
        var turretObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/FieldTurrets/FieldTurrets/FieldCanonTurret/stage1/FieldCanonTurret LV1.prefab"));
        try
        {
            turretObject.transform.position = new Vector3(50000, 50000);
            var turret = turretObject.GetComponent<TeamHJD.Game.FieldTurrets.FieldTurretBase>();
            var turretSorting = turretObject.GetComponent<UnityEngine.Rendering.SortingGroup>();
            turretSorting.sortingOrder = 50;
            var barrelSorting = turretObject.transform.Find("RotationPoint/Gun1")
                .GetComponent<UnityEngine.Rendering.SortingGroup>();
            Assert.That(barrelSorting.sortAtRoot, Is.True, "The detached barrel sorts outside the turret body group.");
            barrelSorting.sortingOrder = turretSorting.sortingOrder + 3;
            var body = _player.GetComponent<SpriteRenderer>();
            body.sortingOrder = -15;
            _player.transform.position = turret.transform.position + new Vector3(x, y);
            var repair = _player.GetComponent<PlayerTurretRepair>();
            typeof(PlayerTurretRepair).GetProperty("Target").GetSetMethod(true).Invoke(repair, new object[]{turret});
            typeof(PlayerTurretRepair).GetProperty("IsRepairing").GetSetMethod(true).Invoke(repair, new object[]{true});
            Set(repair, "_heldTarget", turret);
            _motion.SetHeldRepair(true);
            _visual.Refresh();
            var toolSorting = _visual.Renderer.GetComponent<UnityEngine.Rendering.SortingGroup>();
            Assert.That(_visual.Renderer.sprite.name, Is.EqualTo("Spanner"));
            Assert.That(toolSorting.enabled && toolSorting.sortAtRoot, Is.True);
            Assert.That(toolSorting.sortingLayerID, Is.EqualTo(turretSorting.sortingLayerID));
            Assert.That(toolSorting.sortingOrder, Is.GreaterThan(turretSorting.sortingOrder));
            Assert.That(toolSorting.sortingOrder, Is.GreaterThan(barrelSorting.sortingOrder),
                "Repair must draw over the detached barrel as well as the turret body.");
            Assert.That(_player.GetComponent<UnityEngine.Rendering.SortingGroup>().sortingOrder, Is.EqualTo(-15),
                "Only the spanner should move above the turret; the body must retain its depth.");
            barrelSorting.sortingOrder += 10;
            _visual.Refresh();
            Assert.That(toolSorting.sortingOrder, Is.GreaterThan(barrelSorting.sortingOrder),
                "A moving barrel's current sorting order must be respected during the same repair.");
            var clip = (AnimationClip)typeof(PlayerSpriteMotion).GetProperty("CurrentClip", Private).GetValue(_motion);
            void Advance(float fraction) => typeof(PlayerSpriteMotion).GetMethod("AdvanceHeldRepairTime", Private)
                .Invoke(_motion, new object[]{clip.length * fraction});
            Advance(.49f); _visual.Refresh();
            Assert.That(RepairSparks(), Is.Empty, "Windup must not emit a contact spark.");
            Advance(.02f); _visual.Refresh();
            Assert.That(repair.TryGetFacingDirection(out var contactDirection), Is.True);
            Assert.That(Vector2.Dot(_visual.Renderer.transform.right, contactDirection), Is.GreaterThan(.998f),
                "At contact the repair spanner should point into the turret, rather than past it.");
            var sparks = RepairSparks();
            Assert.That(sparks.Length, Is.EqualTo(1));
            Assert.That(sparks[0].useAutoRandomSeed, Is.False);
            Assert.That(sparks[0].randomSeed, Is.EqualTo(123u));
            Assert.That(_presentationRandom.Draws, Is.EqualTo(1));
            var settings = new SerializedObject(_visual).FindProperty("_aimSpanner");
            var sprite = _visual.Renderer.sprite;
            Vector2 localHead = (settings.FindPropertyRelative("muzzle").vector2Value * sprite.rect.size - sprite.pivot) / sprite.pixelsPerUnit;
            if (_visual.Renderer.flipY) localHead.y = -localHead.y;
            Assert.That(Vector3.Distance(sparks[0].transform.position, _visual.Renderer.transform.TransformPoint(localHead)), Is.LessThan(.01f));
            Assert.That(sparks[0].GetComponent<ParticleSystemRenderer>().sortingOrder, Is.GreaterThan(turretSorting.sortingOrder));
            Assert.That(sparks[0].GetComponent<ParticleSystemRenderer>().sortingOrder, Is.GreaterThan(barrelSorting.sortingOrder));
            _visual.Refresh(); Advance(.1f); _visual.Refresh();
            Assert.That(RepairSparks().Length, Is.EqualTo(1), "Repeated renders after one impact must not emit duplicates.");
            _motion.SetHeldRepair(false); _motion.SetHeldRepair(true);
            Advance(.51f); _visual.Refresh();
            Assert.That(RepairSparks().Length, Is.EqualTo(2), "The next repair stroke must emit again.");
            Assert.That(_presentationRandom.Draws, Is.EqualTo(2));
            var boundSource = (TeamHJD.Game.Contracts.IRandomSource)typeof(PlayerWeaponVisual)
                .GetField("_repairImpactRandom", Private).GetValue(_visual);
            _visual.UnbindPresentationRandom();
            Assert.That(boundSource.IsAvailable, Is.False);
            _motion.SetHeldRepair(false); _motion.SetHeldRepair(true);
            Advance(.51f); _visual.Refresh();
            Assert.That(RepairSparks().Length, Is.EqualTo(2), "Missing presentation random must not use an automatic or fixed-seed fallback.");
            Assert.That(_presentationRandom.Draws, Is.EqualTo(2));
            typeof(PlayerTurretRepair).GetProperty("IsRepairing").GetSetMethod(true).Invoke(repair, new object[]{false});
            _motion.SetHeldRepair(false);
            // A delayed impact notification after release must be consumed silently.
            typeof(PlayerSpriteMotion).GetProperty("HeldRepairImpactSequence").GetSetMethod(true)
                .Invoke(_motion, new object[]{_motion.HeldRepairImpactSequence + 1});
            _visual.Refresh();
            Assert.That(toolSorting.enabled, Is.False, "Finishing or cancelling repair must restore normal actor sorting.");
            Assert.That(RepairSparks().Length, Is.EqualTo(2));
        }
        finally
        {
            foreach(var effect in RepairSparks()) Object.DestroyImmediate(effect.gameObject);
            Object.DestroyImmediate(turretObject);
        }
    }

    [Test]
    public void RepairSparkPrefabIsBriefMetalBurstAndCleansItselfUp()
    {
        var effect = (ParticleSystem)new SerializedObject(_visual).FindProperty("_repairImpactPrefab").objectReferenceValue;
        Assert.That(effect, Is.Not.Null);
        Assert.That(effect.useAutoRandomSeed, Is.False);
        Assert.That(effect.main.loop || effect.main.playOnAwake, Is.False);
        Assert.That(effect.main.startLifetime.constantMax, Is.LessThanOrEqualTo(.2f));
        Assert.That(effect.main.stopAction, Is.EqualTo(ParticleSystemStopAction.Destroy));
        Assert.That(effect.emission.burstCount, Is.EqualTo(1));
        Assert.That(effect.emission.GetBurst(0).count.constant, Is.EqualTo(12));
        Assert.That(effect.GetComponent<ParticleSystemRenderer>().renderMode, Is.EqualTo(ParticleSystemRenderMode.Stretch));
    }

    private ParticleSystem[] RepairSparks() => Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
        .Where(effect => effect.name == "SpannerRepairImpact(Clone)" && effect.gameObject.scene == _player.scene &&
            Vector3.Distance(effect.transform.position, _player.transform.position) < 10f).ToArray();

    [Test]
    public void PlayerSpawnerInjectsPresentationRandomBeforeTheFirstEffect()
    {
        var startObject = new GameObject("Presentation random test start");
        startObject.transform.position = new Vector3(50000, 50000);
        var start = startObject.AddComponent<TeamHJD.Game.Presentation.Scene.PlayerStart>();
        var spawner = new TeamHJD.Game.Presentation.Scene.LocalPlayerActorSpawner(
            AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), null, _presentationRandom);
        GameObject actor = null;
        try
        {
            actor = spawner.Spawn(new TeamHJD.Game.Domain.PlayerId("presentation-random-test"), start);
            var visual = actor.GetComponent<PlayerWeaponVisual>();
            var source = (TeamHJD.Game.Contracts.IRandomSource)typeof(PlayerWeaponVisual)
                .GetField("_repairImpactRandom", Private).GetValue(visual);
            Assert.That(source != null && source.IsAvailable, Is.True);
            Assert.That(_presentationRandom.Draws, Is.Zero, "Binding must not draw a seed before a valid repair impact.");
            typeof(PlayerWeaponVisual).GetMethod("OnDestroy", Private).Invoke(visual, null);
            Assert.That(source.IsAvailable, Is.False);
        }
        finally
        {
            if(actor != null) Object.DestroyImmediate(actor);
            spawner.Dispose(); Object.DestroyImmediate(startObject);
        }
    }

    private sealed class RepairPresentationRandom : TeamHJD.Game.Contracts.IRandomStreams, TeamHJD.Game.Contracts.IRandomSource
    {
        public bool IsAvailable => true;
        public int Draws { get; private set; }
        public TeamHJD.Game.Contracts.IRandomSource GetStream(string id)
        {
            Assert.That(id, Is.EqualTo("vfx/player-repair-impact")); return this;
        }
        public int NextInt(int min, int max)
        {
            Assert.That(min, Is.EqualTo(1)); Assert.That(max, Is.EqualTo(int.MaxValue));
            return 123 + Draws++;
        }
        public uint NextUInt32() => throw new System.NotSupportedException();
        public float NextFloat() => throw new System.NotSupportedException();
        public float NextFloat(float min, float max) => throw new System.NotSupportedException();
        public double NextDouble() => throw new System.NotSupportedException();
        public double NextDouble(double min, double max) => throw new System.NotSupportedException();
    }

    [TestCase(0f)] [TestCase(45f)] [TestCase(90f)] [TestCase(135f)]
    [TestCase(180f)] [TestCase(-135f)] [TestCase(-90f)] [TestCase(-45f)]
    public void LargerSpannerHitRangeAndSlashStayOutsideEveryBodyFrame(float angle)
    {
        Set(_selection, "_currentWeapon", WeaponType.Spanner);
        Vector2 direction = Quaternion.Euler(0, 0, angle) * Vector2.right;
        Input("AimDirection", direction); Input("HasAimTarget", false);
        _motion.RefreshSpaceManBody();
        _visual.Refresh();
        Assert.That(_visual.TryGetSpannerStrikeGeometry(direction, out var origin, out var range), Is.True);
        var settings = new SerializedObject(_visual).FindProperty("_aimSpanner");
        var sprite = _visual.Renderer.sprite;
        Vector2 Point(Vector2 point)
        {
            var local = (point * sprite.rect.size - sprite.pivot) / sprite.pixelsPerUnit;
            if (_visual.Renderer.flipY) local.y = -local.y;
            return _visual.Renderer.transform.TransformPoint(local);
        }
        var grip = Point(settings.FindPropertyRelative("grip").vector2Value);
        var head = Point(settings.FindPropertyRelative("muzzle").vector2Value);
        float multiplier = new SerializedObject(_visual).FindProperty("_spannerAttackScale").floatValue;
        Assert.That(multiplier, Is.GreaterThan(1f));
        Assert.That(range, Is.EqualTo(Vector2.Distance(grip, head) * multiplier).Within(.001f));
        var slash = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/VisualsV2/SpannerSlash.prefab")).GetComponent<PlayerSpannerSlash>();
        try
        {
            Assert.That(slash.Show(_player.transform, direction, range, origin, direction.x < 0f), Is.True);
            Assert.That(Vector2.Distance(slash.transform.position, origin), Is.LessThan(.001f));
            var authored = new SerializedObject(slash).FindProperty("_authoredRadius").floatValue;
            Assert.That(slash.transform.lossyScale.x * authored, Is.EqualTo(range).Within(.001f));
            Assert.That(slash.GetComponent<SpriteRenderer>().flipY, Is.EqualTo(direction.x < 0f));
            for (int frame = 0; frame < 5; frame++)
            {
                Set(_motion, "_spaceManPhase", (float)frame); _motion.RefreshSpaceManBody();
                _visual.TryGetSpannerStrikeGeometry(direction, out origin, out range, out var minimumForward);
                float visibleFront = Vector2.Dot(origin + direction * minimumForward, direction);
                var body = _player.GetComponent<SpriteRenderer>();
                foreach (Vector2 vertex in body.sprite.vertices)
                {
                    var point = vertex; if (body.flipX) point.x = -point.x; if (body.flipY) point.y = -point.y;
                    Vector2 worldPoint = body.transform.TransformPoint(point);
                    Assert.That(visibleFront, Is.GreaterThan(Vector2.Dot(worldPoint, direction)),
                        "The inner edge of the enlarged slash must clear the body's visible mesh.");
                }
            }
        }
        finally { Object.DestroyImmediate(slash.gameObject); }
    }

    [Test]
    public void SpannerStrikeHitsFromHandOnceAndRejectsRearDistantAndSensorOnlyTargets()
    {
        _player.transform.position = new Vector3(50000, 50000);
        var spanner = CreateBoundSpanner();
        var objects = new System.Collections.Generic.List<GameObject>();
        try
        {
            Set(spanner, "_swingDirection", Vector2.right);
            Set(spanner, "_damage", 5); Set(spanner, "_impactPrefab", null);
            _visual.TryGetSpannerStrikeGeometry(Vector2.right, out var origin, out var range);
            EnemyHealth Enemy(Vector2 position, bool trigger = false)
            {
                var go = new GameObject("Spanner range target"); objects.Add(go);
                go.transform.position = position; go.layer = LayerMask.NameToLayer("Enemy");
                var collider = go.AddComponent<CircleCollider2D>(); collider.radius = .03f; collider.isTrigger = trigger;
                var health = go.AddComponent<EnemyHealth>(); health.Initialize(); return health;
            }
            var front = Enemy(origin + Vector2.right * (range - .1f));
            front.gameObject.AddComponent<CircleCollider2D>().radius = .02f;
            var rear = Enemy(origin - Vector2.right * .6f);
            var distant = Enemy(origin + Vector2.right * (range + .2f));
            var sensor = Enemy(origin + new Vector2(.5f, .2f), true);
            var outsideArc = Enemy(origin + Vector2.up * .8f);
            var insideBodySide = Enemy(origin + Vector2.right * (range * .25f));
            Physics2D.SyncTransforms();
            typeof(PlayerSpannerWeapon).GetMethod("ApplyMeleeDamage", Private).Invoke(spanner, null);
            Assert.That(front.CurrentHealth, Is.EqualTo(front.MaxHealth - 5), "Multiple body colliders must still take one hit.");
            foreach (var target in new[]{rear, distant, sensor, outsideArc, insideBodySide})
                Assert.That(target.CurrentHealth, Is.EqualTo(target.MaxHealth));
            Assert.That(spanner.AttackRange, Is.EqualTo(range).Within(.001f), "Status UI must report the actual range.");
        }
        finally { foreach(var go in objects) Object.DestroyImmediate(go); Object.DestroyImmediate(spanner.gameObject); }
    }

    [Test]
    public void PendingSpannerStrikeKeepsVisualAimUntilCancelled()
    {
        var spanner = CreateBoundSpanner();
        try
        {
            Set(_selection, "_currentWeapon", WeaponType.Spanner);
            Set(spanner, "_swingDirection", Vector2.left); Set(spanner, "_pendingStrike", true);
            Input("AimDirection", Vector2.right); Input("HasAimTarget", false);
            Assert.That(_visual.TryGetContinuousAimDirection(out var direction), Is.True);
            Assert.That(direction, Is.EqualTo(Vector2.left));
            spanner.CancelPendingAttack();
            _visual.TryGetContinuousAimDirection(out direction);
            Assert.That(direction, Is.EqualTo(Vector2.right));
        }
        finally { Object.DestroyImmediate(spanner.gameObject); }
    }

    private PlayerSpannerWeapon CreateBoundSpanner()
    {
        var spanner = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/PlayerSpanner.prefab"), _player.transform).GetComponent<PlayerSpannerWeapon>();
        spanner.Bind(_player.transform, _input); Set(_selection, "_spanner", spanner);
        return spanner;
    }

    private void Input(string property, object value) => typeof(PlayerInputHandler).GetProperty(property).GetSetMethod(true).Invoke(_input, new[]{value});
    private static void Set(object owner, string field, object value) => owner.GetType().GetField(field, Private).SetValue(owner, value);
}
