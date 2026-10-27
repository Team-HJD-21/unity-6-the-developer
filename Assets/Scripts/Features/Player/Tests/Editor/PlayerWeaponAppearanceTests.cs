using System.Collections;
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

        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { ClipFolder }))
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            bool tool = clip.name.Contains("Attack") || clip.name.Contains("Repair") || clip.name.Contains("Tool");
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

    [Test]
    public void FirstShotOriginMatchesTheAuthoredStartOfTheFiringClip()
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        foreach (string name in new[] { "CryoBlasterAppearance", "SniperAppearance" })
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
        foreach (bool moving in new[] { false, true })
        {
            var appearance = AssetDatabase.LoadAssetAtPath<PlayerWeaponAppearance>(AppearanceFolder + name + ".asset");
            Sprite FirstBody(AnimationClip clip)
            {
                var binding = AnimationUtility.GetObjectReferenceCurveBindings(clip)
                    .Single(b => b.type == typeof(SpriteRenderer) && b.path == "" && b.propertyName == "m_Sprite");
                return AnimationUtility.GetObjectReferenceCurve(clip, binding).First().value as Sprite;
            }
            var ready = FirstBody(motions.GetClip(moving ? PlayerMotion.Move : PlayerMotion.Idle).Animation(facing));
            var firing = FirstBody(motions.GetClip(PlayerMotion.Fire, moving).Animation(facing));
            Assert.That(appearance.TryGetFrame(ready, out var readyFrame), Is.True);
            Assert.That(appearance.TryGetFrame(firing, out var fireFrame), Is.True);
            if (!readyFrame.OverrideMuzzle) continue;
            Assert.That(readyFrame.ShotMuzzleLocalPosition, Is.EqualTo(fireFrame.MuzzleLocalPosition),
                name + ": " + facing + " must not fire its first shot from a lowered idle/move muzzle.");
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
            var attack = player.GetComponent<PlayerAttack>();
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
                bool tool = attack.CurrentWeapon == WeaponType.Spanner;
                string property = attack.CurrentWeapon switch
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
                    Sprite currentBody = body.sprite;
                    Vector3 muzzlePosition = rig.ActiveMuzzle != null ? rig.ActiveMuzzle.position : Vector3.zero;
                    visual.Refresh();
                    Assert.That(appearance.TryGetFrame(currentBody, out var frame), Is.True, action + "_" + facing);
                    Assert.That(visual.Renderer.enabled, Is.True);
                    Assert.That(visual.Renderer.sprite, Is.EqualTo(frame.Weapon));
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
                attack.SelectNextWeapon();
            }
            visual.enabled = false;
            Assert.That(visual.Renderer.enabled, Is.False);
            Assert.That(motion.OwnsDisplay, Is.True);
        }
        finally { if (player != null) Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }
}
