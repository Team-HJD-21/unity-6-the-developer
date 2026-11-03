using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

public sealed class PlayerWeaponAppearanceTests
{
    private const string PlayerPath = "Assets/Prefabs/Player/Player.prefab";
    private const string ClipFolder = "Assets/Art/Animations/Player/WeaponPosesV2";
    private const string AppearanceFolder = "Assets/Data/Player/Weapons/";

    [TestCase("Idle_UpLeft", "Idle_UpRight")]
    [TestCase("Idle_DownLeft", "Idle_DownRight")]
    [TestCase("ToolIdle_UpLeft", "ToolIdle_UpRight")]
    [TestCase("ToolIdle_DownLeft", "ToolIdle_DownRight")]
    [TestCase("Move_Left", "Move_Right")]
    [TestCase("Attack_Left", "Attack_Right")]
    [TestCase("HoverRepair_UpLeft", "HoverRepair_UpRight")]
    [TestCase("HoverFireLeanRight_Left", "HoverFireLeanLeft_Right")]
    public void OppositeFacingsUseSeparateAuthoredSpritesWithoutFlipping(string leftName, string rightName)
    {
        var left = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{leftName}.anim");
        var right = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{rightName}.anim");
        Assert.That(left, Is.Not.Null);
        Assert.That(right, Is.Not.Null);
        var spriteBinding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        var leftKeys = AnimationUtility.GetObjectReferenceCurve(left, spriteBinding);
        var rightKeys = AnimationUtility.GetObjectReferenceCurve(right, spriteBinding);
        Assert.That(leftKeys.Select(key => key.value).Intersect(rightKeys.Select(key => key.value)), Is.Empty,
            "Each facing uses its own authored body and hand pose.");
        foreach (var key in leftKeys)
            Assert.That(AssetDatabase.GetAssetPath(key.value), Does.Contain("Left"));
        foreach (var key in rightKeys)
            Assert.That(AssetDatabase.GetAssetPath(key.value), Does.Contain("Right"));
        Assert.That(leftKeys.Select(key => key.time), Is.EqualTo(rightKeys.Select(key => key.time)));
        Assert.That(left.length, Is.EqualTo(right.length).Within(.001f));
        var flipBinding = EditorCurveBinding.FloatCurve("", typeof(SpriteRenderer), "m_FlipX");
        var leftFlip = AnimationUtility.GetEditorCurve(left, flipBinding);
        var rightFlip = AnimationUtility.GetEditorCurve(right, flipBinding);
        Assert.That(leftFlip, Is.Not.Null);
        Assert.That(rightFlip, Is.Not.Null);
        foreach (var key in leftKeys)
            Assert.That(leftFlip.Evaluate(key.time), Is.Zero.Within(.001f));
        foreach (var key in rightKeys)
            Assert.That(rightFlip.Evaluate(key.time), Is.Zero.Within(.001f));
    }

    [TestCase("Idle")]
    [TestCase("ToolIdle")]
    public void DiagonalIdleUsesAnimatedFramesWithoutMovingThePhysicsRoot(string action)
    {
        foreach (string facing in new[] { "UpRight", "UpLeft", "DownRight", "DownLeft" })
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>($"{ClipFolder}/{action}_{facing}.anim");
            Assert.That(clip, Is.Not.Null);
            Assert.That(clip.isLooping, Is.True);
            var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            Assert.That(keys.Select(key => key.value).Distinct().Count(), Is.EqualTo(4), clip.name);
            Assert.That(keys.First().value, Is.EqualTo(keys.Last().value), "The loop closes on its starting pose.");
            Assert.That(AnimationUtility.GetCurveBindings(clip)
                .Any(curve => curve.path == "" && curve.type == typeof(Transform)), Is.False,
                "Breathing changes sprite frames while the root and colliders remain fixed.");
        }
    }

    [Test]
    public void EveryAuthoredBodyFrameHasASeparateWeaponInItsAppearance()
    {
        var gun = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + "DefaultGunAppearance.asset");
        var cryo = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + "CryoBlasterAppearance.asset");
        var sniper = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + "SniperAppearance.asset");
        var spanner = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + "SpannerAppearance.asset");
        Assert.That(gun, Is.Not.Null);
        Assert.That(cryo, Is.Not.Null);
        Assert.That(sniper, Is.Not.Null);
        Assert.That(spanner, Is.Not.Null);
        Assert.That(gun.IsConfigured && cryo.IsConfigured && spanner.IsConfigured && sniper.IsConfigured, Is.True);

        foreach (bool tool in new[] { false, true })
        foreach (AnimationClip clip in GetAuthoredBodyClips(tool))
        {
            foreach (var binding in AnimationUtility.GetObjectReferenceCurveBindings(clip)
                         .Where(b => b.type == typeof(SpriteRenderer) && b.path == "" && b.propertyName == "m_Sprite"))
            foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
            {
                var body = key.value as Sprite;
                Assert.That(body, Is.Not.Null, clip.name);
                var appearance = tool ? spanner : gun;
                Assert.That(appearance.TryGetFrame(body, out var frame), Is.True, clip.name + ": " + body.name);
                Assert.That(frame.Weapon, Is.Not.EqualTo(body), "The weapon must use its own sprite asset.");
                Assert.That(frame.Weapon.pixelsPerUnit, Is.EqualTo(body.pixelsPerUnit));
                Assert.That(AssetDatabase.GetAssetPath(frame.Weapon), Does.Contain("/SeparatedWeapons/"));
                if (tool) continue;
                Assert.That(cryo.TryGetFrame(body, out var cryoFrame), Is.True, clip.name);
                Assert.That(sniper.TryGetFrame(body, out var sniperFrame), Is.True, clip.name);
                Assert.That(cryoFrame.Weapon, Is.Not.EqualTo(frame.Weapon));
                Assert.That(sniperFrame.Weapon, Is.Not.EqualTo(frame.Weapon));
                Assert.That(cryoFrame.Weapon, Is.Not.EqualTo(sniperFrame.Weapon));
                if (clip.name.Contains("Fire"))
                    Assert.That(cryoFrame.OverrideMuzzle && sniperFrame.OverrideMuzzle, Is.True);
            }
        }
    }

    [TestCase("DefaultGunAppearance")]
    [TestCase("CryoBlasterAppearance")]
    [TestCase("SniperAppearance")]
    [TestCase("SpannerAppearance")]
    public void ImportedFrameCountMatchesAllAuthoredWeaponEntries(string name)
    {
        string path = AppearanceFolder + name + ".asset";
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(path);
        Assert.That(appearance, Is.Not.Null);
        int authoredCount = System.Text.RegularExpressions.Regex.Matches(
            File.ReadAllText(path), @"(?m)^  - _body:").Count;
        Assert.That(authoredCount, Is.GreaterThan(0));
        // Text can contain valid-looking entries which Unity's importer did not load.
        Assert.That(appearance.FrameCount, Is.EqualTo(authoredCount), name);
        Assert.That(new SerializedObject(appearance).FindProperty("_frames").arraySize,
            Is.EqualTo(authoredCount), name);
    }

    [TestCase("DefaultGunAppearance", false)]
    [TestCase("CryoBlasterAppearance", false)]
    [TestCase("SniperAppearance", false)]
    [TestCase("SpannerAppearance", true)]
    public void RuntimeBodyFrames_NeverSelectFullyTransparentWeaponSprites(string name, bool tool)
    {
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + name + ".asset");
        var checkedSprites = new HashSet<Sprite>();
        var binding = EditorCurveBinding.PPtrCurve("", typeof(SpriteRenderer), "m_Sprite");
        foreach (AnimationClip clip in GetAuthoredBodyClips(tool))
        foreach (var key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
        {
            Assert.That(appearance.TryGetFrame(key.value as Sprite, out var frame), Is.True, clip.name);
            if (!checkedSprites.Add(frame.Weapon)) continue;
            string path = AssetDatabase.GetAssetPath(frame.Weapon);
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                Color32[] pixels = texture.GetPixels32();
                Rect rect = frame.Weapon.rect;
                bool visible = false;
                for (int y = Mathf.RoundToInt(rect.yMin); y < Mathf.RoundToInt(rect.yMax) && !visible; y++)
                for (int x = Mathf.RoundToInt(rect.xMin); x < Mathf.RoundToInt(rect.xMax); x++)
                    if (pixels[y * texture.width + x].a >= 32) { visible = true; break; }
                Assert.That(visible, Is.True, name + ": " + clip.name + " selects an empty weapon: " + path);
            }
            finally { Object.DestroyImmediate(texture); }
        }
    }

    private static IEnumerable<AnimationClip> GetAuthoredBodyClips(bool tool)
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var clips = new HashSet<AnimationClip>();
        PlayerMotion[] actions = tool
            ? new[] { PlayerMotion.ToolIdle, PlayerMotion.ToolMove, PlayerMotion.Attack, PlayerMotion.Repair, PlayerMotion.ToolDash }
            : new[] { PlayerMotion.Idle, PlayerMotion.Move, PlayerMotion.Fire, PlayerMotion.Dash };
        foreach (PlayerMotion action in actions)
        foreach (bool hovering in new[] { false, true })
        foreach (PlayerLateralLean lean in System.Enum.GetValues(typeof(PlayerLateralLean)))
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
        {
            AnimationClip clip = motions.GetClip(action, hovering, lean).Animation(facing);
            if (clip != null && clips.Add(clip)) yield return clip;
        }
    }

    [TestCase("DefaultGun")]
    [TestCase("CryoBlaster")]
    [TestCase("Sniper")]
    public void EveryRangedPoseUsesTheSameCompleteWeaponForItsFacing(string weapon)
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + weapon + "Appearance.asset");
        var directions = new HashSet<Sprite>();
        foreach (var pose in GetRangedShotPoses(motions))
        {
            string path = $"Assets/Art/Sprites/Player/Motions/V2/SeparatedWeapons/Canonical/{weapon}/{pose.facing}.png";
            var canonical = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            Assert.That(canonical, Is.Not.Null, path);
            directions.Add(canonical);
            foreach (Sprite body in RootBodyKeys(pose.current).Select(key => key.value as Sprite).Distinct())
            {
                Assert.That(appearance.TryGetFrame(body, out var frame), Is.True, pose.current.name);
                Assert.That(frame.Weapon, Is.EqualTo(canonical),
                    weapon + ": " + pose.current.name + " must keep its complete weapon when the body pose changes.");
            }
        }
        Assert.That(directions.Count, Is.EqualTo(8), "Each facing must retain its independently authored weapon view.");
    }

    [Test]
    public void PlayerPrefabContainsAuthoredForegroundHandsAtTheBodyOrigin()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        var visual = prefab.GetComponent<PlayerWeaponVisual>();
        var body = prefab.GetComponent<SpriteRenderer>();
        var hands = visual.ForegroundHandsRenderer;
        Assert.That(hands, Is.Not.Null);
        Assert.That(hands, Is.Not.EqualTo(body));
        Assert.That(hands, Is.Not.EqualTo(visual.Renderer));
        Assert.That(PrefabUtility.IsPartOfPrefabAsset(hands), Is.True,
            "The hand layer must be authored in the Player prefab.");
        Assert.That(hands.transform.parent, Is.EqualTo(prefab.transform));
        Assert.That(hands.transform.localPosition, Is.EqualTo(Vector3.zero));
        Assert.That(Quaternion.Angle(hands.transform.localRotation, Quaternion.identity), Is.LessThan(.0001f));
        Assert.That(hands.transform.localScale, Is.EqualTo(Vector3.one));
        Assert.That(hands.enabled, Is.False, "An optional hand layer starts hidden until its body frame is selected.");
        Assert.That(hands.GetComponents<Collider2D>(), Is.Empty);
    }

    [TestCase("DefaultGun")]
    [TestCase("CryoBlaster")]
    [TestCase("Sniper")]
    public void RearFacingWeaponsUseTheBodyAsTheirOcclusionLayer(string weapon)
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + weapon + "Appearance.asset");
        foreach (var pose in GetRangedShotPoses(motions))
        foreach (Sprite body in RootBodyKeys(pose.current).Select(key => key.value as Sprite).Distinct())
        {
            Assert.That(appearance.TryGetFrame(body, out var frame), Is.True, pose.current.name);
            bool rear = pose.facing == PlayerAimFacing.Up || pose.facing == PlayerAimFacing.UpLeft ||
                pose.facing == PlayerAimFacing.UpRight;
            Assert.That(frame.SortingOffset, rear ? Is.LessThan(0) : Is.GreaterThan(0),
                weapon + ": " + pose.current.name + " must keep rear weapons behind the helmet and front weapons in front of the torso.");
            if (rear)
                Assert.That(frame.ForegroundHands, Is.Null,
                    "The body already supplies rear-facing arm occlusion; an extra foreground cutout would duplicate those hands.");
            if (frame.ForegroundHands == null) continue;
            Assert.That(frame.ForegroundHands, Is.Not.EqualTo(body));
            Assert.That(frame.ForegroundHands.rect.size, Is.EqualTo(body.rect.size), body.name);
            Assert.That(frame.ForegroundHands.pivot, Is.EqualTo(body.pivot), body.name);
            Assert.That(frame.ForegroundHands.pixelsPerUnit, Is.EqualTo(body.pixelsPerUnit), body.name);
        }
    }

    [TestCase("DefaultGun")]
    [TestCase("CryoBlaster")]
    [TestCase("Sniper")]
    public void CanonicalRangedWeaponsShareTheBodyCanvasPivotAndPixelScale(string weapon)
    {
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + weapon + "Appearance.asset");
        var checkedWeapons = new HashSet<Sprite>();
        foreach (var clip in GetAuthoredBodyClips(false))
        foreach (Sprite body in RootBodyKeys(clip).Select(key => key.value as Sprite).Distinct())
        {
            Assert.That(appearance.TryGetFrame(body, out var frame), Is.True, clip.name);
            Assert.That(body.rect.size, Is.EqualTo(new Vector2(160f, 160f)), body.name);
            Assert.That(body.pivot, Is.EqualTo(new Vector2(80f, 80f)), body.name);
            Assert.That(frame.Weapon.pixelsPerUnit, Is.EqualTo(body.pixelsPerUnit), frame.Weapon.name);
            if (!checkedWeapons.Add(frame.Weapon)) continue;
            Assert.That(frame.Weapon.rect.size, Is.EqualTo(body.rect.size),
                "A shared canvas avoids odd/even sprite dimensions moving the weapon onto a half-pixel grid.");
            Assert.That(frame.Weapon.pivot, Is.EqualTo(body.pivot), frame.Weapon.name);
            string path = AssetDatabase.GetAssetPath(frame.Weapon);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.That(importer, Is.Not.Null, path);
            Assert.That(importer.filterMode, Is.EqualTo(FilterMode.Point), path);
        }
        Assert.That(checkedWeapons.Count, Is.EqualTo(8));
    }

    [TestCase("DefaultGun")]
    [TestCase("CryoBlaster")]
    [TestCase("Sniper")]
    public void EveryRangedAttachmentStaysOnTheBodyPixelGrid(string weapon)
    {
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + weapon + "Appearance.asset");
        var bodies = new HashSet<Sprite>();
        foreach (var clip in GetAuthoredBodyClips(false))
        foreach (Sprite body in RootBodyKeys(clip).Select(key => key.value as Sprite))
        {
            if (!bodies.Add(body)) continue;
            Assert.That(appearance.TryGetFrame(body, out var frame), Is.True, clip.name);
            Vector2 pixels = frame.LocalPosition * body.pixelsPerUnit;
            Assert.That(Mathf.Abs(pixels.x - Mathf.Round(pixels.x)), Is.LessThan(.00001f),
                weapon + ": " + body.name + " has a fractional horizontal pixel offset.");
            Assert.That(Mathf.Abs(pixels.y - Mathf.Round(pixels.y)), Is.LessThan(.00001f),
                weapon + ": " + body.name + " has a fractional vertical pixel offset.");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(frame.LocalAngle, 0f)), Is.LessThan(.00001f),
                "Each independent facing is authored on the same pixel grid; an extra rotation would resample it.");
        }
    }

    [TestCase("DefaultGun")]
    [TestCase("CryoBlaster")]
    [TestCase("Sniper")]
    public void RangedMuzzleMatchesTheVisibleCanonicalBarrelForEveryPose(string weapon)
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + weapon + "Appearance.asset");
        var textures = new Dictionary<Sprite, (Color32[] pixels, int width, int height)>();
        var checkedBodies = new HashSet<Sprite>();
        foreach (var pose in GetRangedShotPoses(motions))
        foreach (Sprite body in RootBodyKeys(pose.current).Select(key => key.value as Sprite))
        {
            if (!checkedBodies.Add(body)) continue;
            Assert.That(appearance.TryGetFrame(body, out var frame), Is.True, pose.current.name);
            Assert.That(frame.OverrideMuzzle, Is.True, weapon + ": " + body.name);
            if (!textures.TryGetValue(frame.Weapon, out var source))
            {
                string path = AssetDatabase.GetAssetPath(frame.Weapon);
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                try
                {
                    Assert.That(texture.LoadImage(File.ReadAllBytes(path)), Is.True, path);
                    source = (texture.GetPixels32(), texture.width, texture.height);
                    textures.Add(frame.Weapon, source);
                }
                finally { Object.DestroyImmediate(texture); }
            }

            // Inspect the painted PNG, independently from the first-shot mapping.
            // Unity GetPixels32 uses bottom-left pixel order, matching Sprite.pivot.
            Vector2 relative = Quaternion.Euler(0f, 0f, -frame.LocalAngle) *
                (frame.MuzzleLocalPosition - frame.LocalPosition);
            Vector2 muzzlePixel = frame.Weapon.pivot + relative * frame.Weapon.pixelsPerUnit;
            Vector2 aim = PlayerAimUtility.GetFacingDirection(pose.facing);
            float nearestInkSquared = float.PositiveInfinity;
            float furthestInkAlongAim = float.NegativeInfinity;
            for (int y = 0; y < source.height; y++)
            for (int x = 0; x < source.width; x++)
            {
                if (source.pixels[y * source.width + x].a < 128) continue;
                Vector2 pixel = new Vector2(x + .5f, y + .5f);
                nearestInkSquared = Mathf.Min(nearestInkSquared, (pixel - muzzlePixel).sqrMagnitude);
                furthestInkAlongAim = Mathf.Max(furthestInkAlongAim, Vector2.Dot(pixel, aim));
            }
            Assert.That(nearestInkSquared, Is.LessThanOrEqualTo(4f),
                weapon + ": " + body.name + " muzzle is detached from the visible weapon.");
            float tipGap = furthestInkAlongAim - Vector2.Dot(muzzlePixel, aim);
            Assert.That(tipGap, Is.InRange(-2f, 4f),
                weapon + ": " + body.name + " must emit near the painted barrel cap, rather than the receiver or hand.");
        }
    }

    [TestCase("DefaultGun")]
    [TestCase("CryoBlaster")]
    [TestCase("Sniper")]
    public void IdleBreathingKeepsTheWeaponGripAndAngleFixed(string weapon)
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + weapon + "Appearance.asset");
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
        {
            var clip = motions.GetClip(PlayerMotion.Idle).Animation(facing);
            var frames = RootBodyKeys(clip).Select(key => key.value as Sprite).Distinct().Select(body =>
            {
                Assert.That(appearance.TryGetFrame(body, out var frame), Is.True, clip.name);
                return frame;
            }).ToArray();
            Assert.That(frames.Length, Is.GreaterThan(1), clip.name + " must keep its authored breathing frames.");
            foreach (var frame in frames)
            {
                Assert.That(frame.Weapon, Is.EqualTo(frames[0].Weapon), clip.name);
                Assert.That(Vector2.Distance(frame.LocalPosition, frames[0].LocalPosition), Is.LessThan(.00001f),
                    weapon + ": " + clip.name + " must not move the trigger away from the fixed right glove.");
                Assert.That(Mathf.Abs(Mathf.DeltaAngle(frame.LocalAngle, frames[0].LocalAngle)), Is.LessThan(.00001f), clip.name);
            }
        }
    }

    [TestCase("DefaultGun")]
    [TestCase("CryoBlaster")]
    [TestCase("Sniper")]
    public void FirstShotOriginMatchesTheAuthoredStartOfEveryRangedFiringClip(string weapon)
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + weapon + "Appearance.asset");
        foreach (var pose in GetRangedShotPoses(motions))
        {
            // Fire clips can start with an idle Sprite. The actual object-reference
            // curve is authoritative; a similarly named PNG is not necessarily used.
            var firstKey = RootBodyKeys(pose.firstShot).OrderBy(key => key.time).First();
            Assert.That(firstKey.time, Is.EqualTo(0f), pose.firstShot.name);
            Assert.That(appearance.TryGetFrame(firstKey.value as Sprite, out var firstFrame), Is.True, pose.firstShot.name);
            Assert.That(firstFrame.OverrideMuzzle, Is.True, weapon + ": " + pose.firstShot.name);
            foreach (Sprite body in RootBodyKeys(pose.current).Select(key => key.value as Sprite).Distinct())
            {
                Assert.That(appearance.TryGetFrame(body, out var frame), Is.True, pose.current.name);
                Assert.That(frame.OverrideMuzzle, Is.True, weapon + ": " + pose.current.name);
                Assert.That(Vector2.Distance(frame.ShotMuzzleLocalPosition, firstFrame.MuzzleLocalPosition),
                    Is.LessThan(.00001f), weapon + ": " + pose.current.name + " / " + body.name +
                    " must emit from the actual first firing pose, including strafing and diagonal backpedal.");
            }
        }
    }

    private static ObjectReferenceKeyframe[] RootBodyKeys(AnimationClip clip)
    {
        Assert.That(clip, Is.Not.Null);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
            .Single(b => b.type == typeof(SpriteRenderer) && b.path == "" && b.propertyName == "m_Sprite");
        var keys = AnimationUtility.GetObjectReferenceCurve(clip, binding);
        Assert.That(keys, Is.Not.Empty, clip.name);
        return keys;
    }

    private static IEnumerable<(AnimationClip current, AnimationClip firstShot, PlayerAimFacing facing)>
        GetRangedShotPoses(PlayerMotionSet motions)
    {
        Assert.That(motions, Is.Not.Null);
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
        {
            var firing = motions.GetClip(PlayerMotion.Fire).Animation(facing);
            yield return (motions.GetClip(PlayerMotion.Idle).Animation(facing), firing, facing);
            yield return (firing, firing, facing);
            var leans = PlayerAimUtility.IsDiagonal(facing)
                ? new[] { PlayerLateralLean.Neutral, PlayerLateralLean.Back }
                : new[] { PlayerLateralLean.Neutral, PlayerLateralLean.Left, PlayerLateralLean.Right };
            foreach (var lean in leans)
            {
                var movingFire = motions.GetClip(PlayerMotion.Fire, true, lean).Animation(facing);
                yield return (motions.GetClip(PlayerMotion.Move, false, lean).Animation(facing), movingFire, facing);
                yield return (movingFire, movingFire, facing);
            }
        }
    }

    [Test]
    public void MissingWeaponReferenceRejectsAnIncompleteAppearance()
    {
        var source = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + "DefaultGunAppearance.asset");
        var incomplete = Object.Instantiate(source);
        try
        {
            var serialized = new SerializedObject(incomplete);
            var first = serialized.FindProperty("_frames").GetArrayElementAtIndex(0);
            var body = first.FindPropertyRelative("_body").objectReferenceValue as Sprite;
            first.FindPropertyRelative("_weapon").objectReferenceValue = null;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(incomplete.IsConfigured, Is.False);
            Assert.That(incomplete.TryGetFrame(body, out _), Is.False);
        }
        finally { Object.DestroyImmediate(incomplete); }
    }

    [UnityTest]
    public IEnumerator SwitchingWeaponFamilies_ResolvesBodyAndWeaponInTheSameFrame()
    {
        yield return new EnterPlayMode();
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath),
            new Vector3(12000f, 12000f, 0f), Quaternion.identity);
        try
        {
            var input = player.GetComponent<PlayerInputHandler>();
            input.enabled = false;
            var selection = player.GetComponent<PlayerWeaponSelection>();
            var visual = player.GetComponent<PlayerWeaponVisual>();
            var body = player.GetComponent<SpriteRenderer>();
            var rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            var setMove = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.MoveInput)).GetSetMethod(true);
            var setAim = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.AimDirection)).GetSetMethod(true);
            foreach (bool moving in new[] { false, true })
            foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
            {
                setMove.Invoke(input, new object[] { moving ? Vector2.up : Vector2.zero });
                Vector2 aim = PlayerAimUtility.GetFacingDirection(facing);
                setAim.Invoke(input, new object[] { aim });
                rig.RefreshPose(aim);
                Assert.That(rig.Facing, Is.EqualTo(facing), "Set up the actual eight-way rig pose before switching.");
                foreach (var choice in new[] { (weapon: WeaponType.Spanner, appearance: "SpannerAppearance"),
                    (weapon: WeaponType.DefaultGun, appearance: "DefaultGunAppearance") })
                {
                    Assert.That(selection.TrySelectWeapon(choice.weapon), Is.True);
                    var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + choice.appearance + ".asset");
                    Assert.That(appearance.TryGetFrame(body.sprite, out var frame), Is.True,
                        "A successful switch must evaluate its body before the next Update: " + choice.weapon + " " + facing);
                    visual.Refresh();
                    Assert.That(visual.Renderer.enabled, Is.True);
                    Assert.That(visual.Renderer.sprite, Is.EqualTo(frame.Weapon));
                    Assert.That(visual.ForegroundHandsRenderer.sprite, Is.EqualTo(frame.ForegroundHands));
                    Assert.That(visual.ForegroundHandsRenderer.enabled, Is.EqualTo(frame.ForegroundHands != null),
                        "Switching weapon families must remove the previous family's hand overlay in the same frame.");
                }
            }
        }
        finally { Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator BlockedSwitchesAndDisabledMotion_DoNotForceANewPose()
    {
        yield return new EnterPlayMode();
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath),
            new Vector3(12000f, 12000f, 0f), Quaternion.identity);
        float previousScale = Time.timeScale;
        try
        {
            player.GetComponent<PlayerInputHandler>().enabled = false;
            var selection = player.GetComponent<PlayerWeaponSelection>();
            var controls = player.GetComponent<PlayerControlState>();
            var motion = player.GetComponent<PlayerSpriteMotion>();
            var body = player.GetComponent<SpriteRenderer>();
            Sprite previousBody = body.sprite;
            Time.timeScale = 0f;
            Assert.That(selection.TrySelectWeapon(WeaponType.Spanner), Is.False);
            Assert.That(body.sprite, Is.EqualTo(previousBody));
            Time.timeScale = previousScale;
            controls.SetInputBlocked(true);
            Assert.That(selection.TrySelectWeapon(WeaponType.Spanner), Is.False);
            Assert.That(body.sprite, Is.EqualTo(previousBody));
            controls.SetInputBlocked(false);
            selection.enabled = false;
            Assert.That(selection.TrySelectWeapon(WeaponType.Spanner), Is.False);
            Assert.That(body.sprite, Is.EqualTo(previousBody));
            selection.enabled = true;

            motion.enabled = false;
            previousBody = body.sprite;
            Assert.That(selection.TrySelectWeapon(WeaponType.Spanner), Is.True);
            Assert.That(body.sprite, Is.EqualTo(previousBody), "Disabled motion must unsubscribe from successful weapon switches.");
            motion.enabled = true;
            player.GetComponent<Animator>().Update(0f);
            var tool = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + "SpannerAppearance.asset");
            Assert.That(tool.TryGetFrame(body.sprite, out _), Is.True, "Re-enabling motion must restore the currently equipped family.");
        }
        finally { Time.timeScale = previousScale; Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator SwitchingRangedWeapons_PreservesCurrentFiringPhase()
    {
        yield return new EnterPlayMode();
        var player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath),
            new Vector3(12000f, 12000f, 0f), Quaternion.identity);
        try
        {
            player.GetComponent<PlayerInputHandler>().enabled = false;
            var selection = player.GetComponent<PlayerWeaponSelection>();
            var motion = player.GetComponent<PlayerSpriteMotion>();
            var animator = player.GetComponent<Animator>();
            var body = player.GetComponent<SpriteRenderer>();
            motion.PlayRangedShot();
            yield return null;
            animator.Play("Base Layer.Fire_Down", 0, .55f);
            animator.Update(0f);
            var state = animator.GetCurrentAnimatorStateInfo(0);
            Sprite previousBody = body.sprite;
            foreach (WeaponType weapon in new[] { WeaponType.CryoBlaster, WeaponType.Sniper })
            {
                Assert.That(selection.TrySelectWeapon(weapon), Is.True);
                var after = animator.GetCurrentAnimatorStateInfo(0);
                Assert.That(after.fullPathHash, Is.EqualTo(state.fullPathHash));
                Assert.That(after.normalizedTime, Is.EqualTo(state.normalizedTime).Within(.0001f));
                Assert.That(body.sprite, Is.EqualTo(previousBody));
                Assert.That(motion.IsFiring, Is.True);
            }
        }
        finally { Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator EquippedWeaponTracksAnimatorFramesAcrossDirectionsAndWeaponSwitches()
    {
        yield return new EnterPlayMode();
        GameObject player = null;
        try
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
            player = Object.Instantiate(prefab, new Vector3(12000, 12000, 0), Quaternion.identity);
            var input = player.GetComponent<PlayerInputHandler>();
            input.enabled = false;
            var selection = player.GetComponent<PlayerWeaponSelection>();
            var motion = player.GetComponent<PlayerSpriteMotion>();
            var animator = player.GetComponent<Animator>();
            var visual = player.GetComponent<PlayerWeaponVisual>();
            var body = player.GetComponent<SpriteRenderer>();
            var sorting = player.GetComponent<SortingGroup>();
            var rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            Assert.That(visual.IsConfigured, Is.True);
            var settings = new SerializedObject(visual);
            Vector3 rootPosition = player.transform.position;

            for (int weapon = 0; weapon < 4; weapon++)
            {
                bool tool = selection.CurrentWeapon == WeaponType.Spanner;
                string property = selection.CurrentWeapon switch
                {
                    WeaponType.Spanner => "_spanner",
                    WeaponType.CryoBlaster => "_cryoBlaster",
                    WeaponType.Sniper => "_sniper",
                    _ => "_defaultGun"
                };
                var appearance = settings.FindProperty(property).objectReferenceValue as PlayerWeaponAppearance;
                foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
                foreach (string action in tool ? new[] { "ToolIdle", "ToolMove", "Attack", "HoverAttack", "Repair", "HoverRepair" }
                                              : new[] { "Idle", "Move", "Fire", "HoverFire" })
                {
                    animator.Play("Base Layer." + action + "_" + facing, 0, .5f);
                    animator.Update(0f);
                    // Exercise changes the Animator's authored no-flip clips do
                    // not normally make, so stale hand tint/flip state is caught.
                    body.color = new Color(.52f, .73f, .94f, .66f);
                    body.flipX = facing == PlayerAimFacing.Left;
                    body.flipY = facing == PlayerAimFacing.Up;
                    body.sortingOrder = 50 + (int)facing;
                    visual.ForegroundHandsRenderer.transform.localPosition = Vector3.one;
                    visual.ForegroundHandsRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, 25f);
                    visual.ForegroundHandsRenderer.transform.localScale = new Vector3(2f, 2f, 1f);
                    Sprite currentBody = body.sprite;
                    Vector3 muzzlePosition = rig.ActiveMuzzle != null ? rig.ActiveMuzzle.position : Vector3.zero;
                    visual.Refresh();
                    Assert.That(appearance.TryGetFrame(currentBody, out var frame), Is.True, action + "_" + facing);
                    Assert.That(visual.Renderer.enabled, Is.True);
                    Assert.That(visual.Renderer.sprite, Is.EqualTo(frame.Weapon));
                    Assert.That(visual.Renderer.sortingOrder, Is.EqualTo(body.sortingOrder + frame.SortingOffset));
                    var hands = visual.ForegroundHandsRenderer;
                    Assert.That(hands.sprite, Is.EqualTo(frame.ForegroundHands), action + "_" + facing);
                    Assert.That(hands.enabled, Is.EqualTo(frame.ForegroundHands != null), action + "_" + facing);
                    Assert.That(hands.transform.localPosition, Is.EqualTo(Vector3.zero));
                    Assert.That(Quaternion.Angle(hands.transform.localRotation, Quaternion.identity), Is.LessThan(.0001f));
                    Assert.That(hands.transform.localScale, Is.EqualTo(Vector3.one));
                    Assert.That(hands.sortingLayerID, Is.EqualTo(body.sortingLayerID));
                    Assert.That(hands.sortingOrder, Is.EqualTo(body.sortingOrder + 2));
                    Assert.That(hands.color, Is.EqualTo(body.color));
                    Assert.That(hands.flipX, Is.EqualTo(body.flipX));
                    Assert.That(hands.flipY, Is.EqualTo(body.flipY));
                    Assert.That(sorting.sortingOrder, Is.EqualTo(body.sortingOrder));
                    Assert.That(sorting.sortingLayerID, Is.EqualTo(body.sortingLayerID));
                    Assert.That(body.sprite, Is.EqualTo(currentBody), "Weapon selection must not replace the body pose.");
                    Assert.That(player.transform.position, Is.EqualTo(rootPosition));
                    if (rig.ActiveMuzzle != null) Assert.That(rig.ActiveMuzzle.position, Is.EqualTo(muzzlePosition));
                    if (frame.OverrideMuzzle)
                    {
                        Assert.That(visual.TryGetMuzzlePosition(out Vector3 expectedMuzzle), Is.True);
                        Assert.That(rig.TryGetMuzzle(out var muzzle, false), Is.True);
                        Assert.That(Vector3.Distance(muzzle.position, expectedMuzzle), Is.LessThan(.001f));
                    }
                }
                selection.SelectNextWeapon();
            }
            body.enabled = false;
            visual.Refresh();
            Assert.That(visual.Renderer.enabled, Is.False);
            Assert.That(visual.ForegroundHandsRenderer.enabled, Is.False);
            body.enabled = true;
            visual.Refresh();
            visual.enabled = false;
            Assert.That(visual.Renderer.enabled, Is.False);
            Assert.That(visual.ForegroundHandsRenderer.enabled, Is.False);
            Assert.That(motion.OwnsDisplay, Is.True);
        }
        finally { if (player != null) Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }
}
