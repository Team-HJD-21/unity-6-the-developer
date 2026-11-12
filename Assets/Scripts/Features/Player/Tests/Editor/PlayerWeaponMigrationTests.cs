// 현행 Player Prefab의 무기·총구 연결과 구형 컨트롤러 제거 상태를 검증합니다.
using System.Linq;
using System.Collections;
using UnityEngine.TestTools;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerWeaponMigrationTests
{
    private GameObject PlayerPrefab => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");

    [Test]
    public void EightWaySpannerClips_HaveAuthoredImpactRecoveryAndMatchingStates()
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        Assert.That(motions.HasEightWayTools, Is.True);
        var states = ((UnityEditor.Animations.AnimatorController)motions.Controller).layers[0].stateMachine.states;
        foreach (PlayerAimFacing facing in new[] { PlayerAimFacing.DownLeft, PlayerAimFacing.DownRight, PlayerAimFacing.UpLeft, PlayerAimFacing.UpRight })
        foreach (PlayerMotion action in new[] { PlayerMotion.ToolIdle, PlayerMotion.ToolMove, PlayerMotion.Attack, PlayerMotion.Repair })
        foreach (bool hovering in new[] { false, true })
        {
            if (hovering && (action == PlayerMotion.ToolIdle || action == PlayerMotion.ToolMove)) continue;
            var clip = motions.GetClip(action, hovering).Animation(facing);
            string stateName = $"{(hovering ? "Hover" : "")}{action}_{facing}";
            Assert.That(states.Any(state => state.state.name == stateName && state.state.motion == clip), Is.True, stateName);
            bool toolAction = action == PlayerMotion.Attack || action == PlayerMotion.Repair;
            Assert.That(clip.length, Is.EqualTo(toolAction ? (action == PlayerMotion.Attack ? .4f : .6f) : .8f).Within(.001f));
            Assert.That(clip.isLooping, Is.EqualTo(!toolAction));
            foreach (Sprite frame in Frames(clip))
            {
                Assert.That(frame.rect.size, Is.EqualTo(new Vector2(160, 160)));
                Assert.That(frame.pixelsPerUnit, Is.EqualTo(32));
            }
            if (!toolAction) continue;
            var keys = AnimationUtility.GetObjectReferenceCurve(clip, AnimationUtility.GetObjectReferenceCurveBindings(clip).Single());
            Assert.That(keys.Length, Is.EqualTo(5));
            Assert.That(keys[2].time, Is.EqualTo(clip.length * .5f).Within(.001f));
            Assert.That(keys.First().value, Is.EqualTo(keys.Last().value));
            var ready = motions.GetClip(hovering ? PlayerMotion.ToolMove : PlayerMotion.ToolIdle).Animation(facing);
            Assert.That(keys.First().value, Is.EqualTo(Frames(ready).Single()), "The same wrench must be held before and after the action.");
            Assert.That(keys[1].value, Is.Not.EqualTo(keys[2].value));
            Assert.That(keys[2].value, Is.Not.EqualTo(keys[3].value));
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform)))
                Assert.That(binding.path, Is.EqualTo("PlayerGroundShadow"), "Tool artwork must not animate physics or weapon roots.");
        }
    }

    [Test]
    public void EightWaySpannerConfiguration_FallsBackWhenAHoverRepairDirectionIsMissing()
    {
        var source = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var motions = Object.Instantiate(source);
        try
        {
            var data = new SerializedObject(motions);
            data.FindProperty("_hoverRepair").FindPropertyRelative("upLeftAnimation").objectReferenceValue = null;
            data.ApplyModifiedPropertiesWithoutUndo();
            Assert.That(motions.HasEightWayTools, Is.False);
            Assert.That(motions.HasEightWayRanged, Is.True, "An incomplete tool set must not disable ranged diagonals.");
        }
        finally { Object.DestroyImmediate(motions); }
    }

    [UnityTest]
    public IEnumerator DiagonalSpanner_TurningAndChangingMovementPreserveImpactPhaseAndPhysics()
    {
        yield return new EnterPlayMode();
        GameObject player = null;
        try
        {
            player = Object.Instantiate(PlayerPrefab, new Vector3(12000, 12000, 0), Quaternion.identity);
            var input = player.GetComponent<PlayerInputHandler>();
            input.enabled = false;
            var selection = player.GetComponent<PlayerWeaponSelection>();
            Assert.That(selection.TrySelectWeaponSlot(1), Is.True);
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            var rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            var motion = player.GetComponent<PlayerSpriteMotion>();
            var animator = player.GetComponent<Animator>();
            var renderer = player.GetComponent<SpriteRenderer>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var evaluate = typeof(PlayerSpriteMotion).GetMethod("UpdateAnimatorState", flags);
            var update = typeof(PlayerSpriteMotion).GetMethod("Update", flags);
            var elapsed = typeof(PlayerSpriteMotion).GetField("_elapsed", flags);
            var setAim = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.AimDirection)).GetSetMethod(true);
            var setMove = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.MoveInput)).GetSetMethod(true);
            Vector3 rootPosition = player.transform.position;
            Quaternion rootRotation = player.transform.rotation;
            foreach (bool repairing in new[] { false, true })
            {
                setMove.Invoke(input, new object[] { Vector2.zero });
                Assert.That(motion.PlayToolAction(repairing), Is.True);
                evaluate.Invoke(motion, null);
                elapsed.SetValue(motion, repairing ? .15f : .1f);
                foreach (PlayerAimFacing facing in new[] { PlayerAimFacing.DownLeft, PlayerAimFacing.DownRight, PlayerAimFacing.UpLeft, PlayerAimFacing.UpRight })
                foreach (Vector2 movement in new[] { Vector2.zero, Vector2.right, Vector2.left })
                {
                    Vector2 aim = PlayerAimUtility.GetFacingDirection(facing);
                    setAim.Invoke(input, new object[] { aim });
                    setMove.Invoke(input, new object[] { movement });
                    rig.RefreshPose(aim);
                    evaluate.Invoke(motion, null);
                    animator.Update(0f);
                    Assert.That(rig.Facing, Is.EqualTo(facing));
                    bool hovering = movement != Vector2.zero;
                    string action = repairing ? "Repair" : "Attack";
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                        Is.EqualTo(Animator.StringToHash($"Base Layer.{(hovering ? "Hover" : "")}{action}_{facing}")));
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(.25f).Within(.001f));
                    Assert.That(renderer.sprite, Is.Not.Null);
                    Assert.That(rig.ActiveGun.gameObject.activeSelf, Is.False);
                    Assert.That(motion.ToolImpactDelay(repairing), Is.EqualTo(repairing ? .3f : .2f).Within(.001f));
                    Assert.That(player.transform.position, Is.EqualTo(rootPosition));
                    Assert.That(player.transform.rotation, Is.EqualTo(rootRotation));
                }
            }
            motion.CancelToolAction();
            elapsed.SetValue(motion, 10f);
            update.Invoke(motion, null);
            animator.Update(0f);
            Assert.That(motion.IsPlayingToolAction, Is.False);
        }
        finally { if (player != null) Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    [TestCase(PlayerAimFacing.UpLeft, 1, -1, true)]
    [TestCase(PlayerAimFacing.UpLeft, 1, 0, true)]
    [TestCase(PlayerAimFacing.UpLeft, 0, -1, true)]
    [TestCase(PlayerAimFacing.UpLeft, 1, 1, false)]
    [TestCase(PlayerAimFacing.UpLeft, -1, 1, false)]
    [TestCase(PlayerAimFacing.DownRight, -1, 1, true)]
    [TestCase(PlayerAimFacing.UpRight, -1, -1, true)]
    [TestCase(PlayerAimFacing.DownLeft, 1, 1, true)]
    [TestCase(PlayerAimFacing.DownLeft, 0, 0, false)]
    public void BackpedalDetection_ExcludesForwardStationaryAndPerpendicularMovement(PlayerAimFacing facing, float x, float y, bool expected)
    {
        Assert.That(PlayerAimUtility.IsMovingBackward(facing, new Vector2(x, y)), Is.EqualTo(expected));
        Assert.That(PlayerAimUtility.IsMovingBackward(facing, new Vector2(float.NaN, 0)), Is.False);
    }

    [UnityTest]
    public IEnumerator DiagonalBackpedal_PreservesShotPhaseAndUsesTheSameMovingSpriteAndMuzzle()
    {
        yield return new EnterPlayMode();
        GameObject player = null;
        try
        {
            player = Object.Instantiate(PlayerPrefab, new Vector3(12000, 12000, 0), Quaternion.identity);
            var input = player.GetComponent<PlayerInputHandler>();
            input.enabled = false;
            var rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            var motion = player.GetComponent<PlayerSpriteMotion>();
            var animator = player.GetComponent<Animator>();
            var renderer = player.GetComponent<SpriteRenderer>();
            var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
            Assert.That(motions.HasDiagonalBackpedal, Is.True);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var evaluate = typeof(PlayerSpriteMotion).GetMethod("UpdateAnimatorState", flags);
            var update = typeof(PlayerSpriteMotion).GetMethod("Update", flags);
            var elapsed = typeof(PlayerSpriteMotion).GetField("_elapsed", flags);
            var setMove = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.MoveInput)).GetSetMethod(true);
            var setAim = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.AimDirection)).GetSetMethod(true);
            Vector3 root = player.transform.position;
            Quaternion rotation = player.transform.rotation;
            foreach (PlayerAimFacing facing in new[] { PlayerAimFacing.UpLeft, PlayerAimFacing.UpRight, PlayerAimFacing.DownLeft, PlayerAimFacing.DownRight })
            {
                Vector2 aim = PlayerAimUtility.GetFacingDirection(facing);
                setAim.Invoke(input, new object[] { aim });
                setMove.Invoke(input, new object[] { -aim });
                rig.RefreshPose(aim);
                motion.PlayRangedShot();
                evaluate.Invoke(motion, null);
                elapsed.SetValue(motion, .05f);
                var sprite = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Art/Sprites/Player/Motions/V2/BackpedalEightWay/Backpedal_{facing}.png");
                foreach (Vector2 movement in new[] { aim, -aim })
                {
                    setMove.Invoke(input, new object[] { movement });
                    rig.RefreshPose(aim);
                    evaluate.Invoke(motion, null);
                    animator.Update(0);
                    Assert.That(animator.GetCurrentAnimatorStateInfo(0).normalizedTime, Is.EqualTo(.25f).Within(.001f));
                }
                Assert.That(renderer.sprite, Is.EqualTo(sprite));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    Is.EqualTo(Animator.StringToHash($"Base Layer.HoverFireLeanBack_{facing}")));
                Assert.That(rig.TryGetMuzzle(out Transform muzzle), Is.True);
                var pose = new SerializedObject(rig).FindProperty("_backpedalMuzzles");
                string field = char.ToLowerInvariant(facing.ToString()[0]) + facing.ToString().Substring(1);
                Vector2 expected = pose.FindPropertyRelative(field).vector2Value;
                Assert.That(Vector2.Distance(rig.transform.InverseTransformPoint(muzzle.position), expected), Is.LessThan(.002f));
                if (facing == PlayerAimFacing.UpLeft)
                {
                    Vector2 right = pose.FindPropertyRelative("upRight").vector2Value;
                    // The left rifle is shorter in the authored sprite. Pixel
                    // (31, 64) is its barrel tip; a pure X mirror leaves a gap.
                    Vector2 barrelTip = new Vector2(-.91875f, .3f);
                    Assert.That(expected.y, Is.GreaterThan(0f), "The upper-left muzzle must stay above the player's feet.");
                    Assert.That(expected.y, Is.EqualTo(right.y).Within(.002f));
                    Assert.That(Vector2.Distance(expected, barrelTip), Is.LessThan(.002f));
                }
                Assert.That(Frames(motions.GetClip(PlayerMotion.Move, false, PlayerLateralLean.Back).Animation(facing))[0], Is.EqualTo(sprite));
                elapsed.SetValue(motion, 1f);
                update.Invoke(motion, null);
                animator.Update(0);
                Assert.That(renderer.sprite, Is.EqualTo(sprite));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    Is.EqualTo(Animator.StringToHash($"Base Layer.MoveLeanBack_{facing}")));
                Assert.That(player.transform.position, Is.EqualTo(root));
                Assert.That(player.transform.rotation, Is.EqualTo(rotation));
            }
        }
        finally { if (player != null) Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    [TestCase(0f, PlayerAimFacing.Right)]
    [TestCase(22.4f, PlayerAimFacing.Right)]
    [TestCase(22.6f, PlayerAimFacing.UpRight)]
    [TestCase(67.4f, PlayerAimFacing.UpRight)]
    [TestCase(67.6f, PlayerAimFacing.Up)]
    [TestCase(135f, PlayerAimFacing.UpLeft)]
    [TestCase(180f, PlayerAimFacing.Left)]
    [TestCase(-135f, PlayerAimFacing.DownLeft)]
    [TestCase(-90f, PlayerAimFacing.Down)]
    [TestCase(-45f, PlayerAimFacing.DownRight)]
    [TestCase(-22.4f, PlayerAimFacing.Right)]
    [TestCase(-22.6f, PlayerAimFacing.DownRight)]
    public void EightWayFacing_UsesFortyFiveDegreeSectors(float angle, PlayerAimFacing expected)
    {
        float radians = angle * Mathf.Deg2Rad;
        Assert.That(PlayerAimUtility.GetEightWayFacing(new Vector2(Mathf.Cos(radians), Mathf.Sin(radians))), Is.EqualTo(expected));
        Assert.That(PlayerAimUtility.GetEightWayFacing(Vector2.zero), Is.EqualTo(PlayerAimFacing.Down));
        Assert.That(PlayerAimUtility.GetEightWayFacing(new Vector2(float.NaN, 1)), Is.EqualTo(PlayerAimFacing.Down));
    }

    [Test]
    public void EightWayRanged_ConnectsIdleMovementFireAndHoverWithoutMovingThePhysicsRoot()
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        Assert.That(motions.HasEightWayRanged, Is.True);
        var states = ((UnityEditor.Animations.AnimatorController)motions.Controller).layers[0].stateMachine.states;
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
        foreach (PlayerMotion action in new[] { PlayerMotion.Idle, PlayerMotion.Move, PlayerMotion.Fire })
        foreach (bool hovering in new[] { false, true })
        {
            var clip = motions.GetClip(action, hovering).Animation(facing);
            string name = $"{(hovering && action == PlayerMotion.Fire ? "Hover" : "")}{action}_{facing}";
            Assert.That(states.Any(state => state.state.name == name && state.state.motion == clip), Is.True, name);
            Assert.That(clip.length, Is.EqualTo(action == PlayerMotion.Fire ? .2f : .8f).Within(.001f));
            foreach (var binding in AnimationUtility.GetCurveBindings(clip).Where(binding => binding.type == typeof(Transform)))
                Assert.That(binding.path, Is.EqualTo("PlayerGroundShadow"), "Only the shadow child may move.");
            if (PlayerAimUtility.IsDiagonal(facing))
            {
                bool stationaryShot = action == PlayerMotion.Fire && !hovering;
                Assert.That(Frames(clip).Length, Is.EqualTo(stationaryShot ? 3 : 1));
                foreach (Sprite frame in Frames(clip))
                {
                    Assert.That(frame.rect.size, Is.EqualTo(new Vector2(160, 160)));
                    Assert.That(frame.pixelsPerUnit, Is.EqualTo(32));
                }
                if (action == PlayerMotion.Fire && hovering)
                    Assert.That(Frames(clip)[0], Is.EqualTo(Frames(motions.GetClip(PlayerMotion.Move).Animation(facing))[0]));
            }
        }
    }

    [TestCase(PlayerAimFacing.DownLeft)]
    [TestCase(PlayerAimFacing.DownRight)]
    [TestCase(PlayerAimFacing.UpLeft)]
    [TestCase(PlayerAimFacing.UpRight)]
    public void StationaryDiagonalShot_HasRecoilAndRecoveryThenReturnsToIdle(PlayerAimFacing facing)
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var fire = motions.GetClip(PlayerMotion.Fire).Animation(facing);
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(fire).Single();
        var keys = AnimationUtility.GetObjectReferenceCurve(fire, binding);
        Sprite idle = Frames(motions.GetClip(PlayerMotion.Idle).Animation(facing)).Single();
        Assert.That(keys.First().value, Is.EqualTo(idle), "The first shot uses the idle muzzle and pose.");
        Assert.That(keys.Last().value, Is.EqualTo(idle), "The recoil must finish at the idle pose.");
        Assert.That(keys.Skip(1).First().value, Is.Not.EqualTo(idle));
        Assert.That(keys[2].value, Is.Not.EqualTo(keys[1].value), "Recovery needs its own pose.");
        Assert.That(Frames(fire).Length, Is.EqualTo(3));
        Assert.That(fire.isLooping, Is.False);
        Assert.That(fire.length, Is.EqualTo(.2f).Within(.001f));
    }

    [UnityTest]
    public IEnumerator DiagonalAim_UsesMatchingPoseAndMuzzleBeforeTheFirstShot()
    {
        yield return new EnterPlayMode();
        GameObject player = null;
        try
        {
            player = Object.Instantiate(PlayerPrefab, new Vector3(12000, 12000, 0), Quaternion.identity);
            var input = player.GetComponent<PlayerInputHandler>();
            input.enabled = false;
            var rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            var motion = player.GetComponent<PlayerSpriteMotion>();
            var animator = player.GetComponent<Animator>();
            var setAim = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.AimDirection)).GetSetMethod(true);
            var setMove = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.MoveInput)).GetSetMethod(true);
            var update = typeof(PlayerSpriteMotion).GetMethod("Update", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var evaluate = typeof(PlayerSpriteMotion).GetMethod("UpdateAnimatorState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var elapsed = typeof(PlayerSpriteMotion).GetField("_elapsed", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Vector3 position = player.transform.position;
            foreach (Vector2 direction in new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) })
            foreach (Vector2 movement in new[] { Vector2.zero, Vector2.right, Vector2.left })
            {
                setAim.Invoke(input, new object[] { direction.normalized });
                setMove.Invoke(input, new object[] { movement });
                rig.RefreshPose(direction);
                elapsed.SetValue(motion, 10f);
                update.Invoke(motion, null);
                Assert.That(rig.TryGetMuzzle(out Transform displayedMuzzle, forShot: false), Is.True);
                var rigData = new SerializedObject(rig);
                string directionField = char.ToLowerInvariant(rig.Facing.ToString()[0]) + rig.Facing.ToString().Substring(1);
                bool backpedaling = movement != Vector2.zero && PlayerAimUtility.IsMovingBackward(rig.Facing, movement);
                string movingPose = backpedaling ? "_backpedalMuzzles" : "_moveMuzzles";
                string displayedPose = movement == Vector2.zero ? "_idleMuzzles" : movingPose;
                Vector2 idleExpected = rigData.FindProperty(displayedPose).FindPropertyRelative(directionField).vector2Value;
                Assert.That(Vector2.Distance(rig.transform.InverseTransformPoint(displayedMuzzle.position), idleExpected), Is.LessThan(.002f));
                Assert.That(rig.TryGetMuzzle(out Transform muzzle), Is.True);
                Assert.That(muzzle.IsChildOf(rig.ActiveGun), Is.True);
                string poseField = movement == Vector2.zero ? "_firingMuzzles" : movingPose;
                Vector2 expected = rigData.FindProperty(poseField).FindPropertyRelative(directionField).vector2Value;
                Assert.That(Vector2.Distance(rig.transform.InverseTransformPoint(muzzle.position), expected), Is.LessThan(.002f));
                motion.PlayRangedShot();
                evaluate.Invoke(motion, null);
                animator.Update(0);
                string prefix = movement == Vector2.zero ? "" : "Hover";
                string lean = backpedaling ? "LeanBack" : "";
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    Is.EqualTo(Animator.StringToHash($"Base Layer.{prefix}Fire{lean}_{rig.Facing}")));
                Assert.That(player.transform.position, Is.EqualTo(position));
            }
        }
        finally { if (player != null) Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    // 별도 프레임 배열 없이 AnimationClip에 저장된 실제 Sprite 키를 확인합니다.
    private static Sprite[] Frames(AnimationClip animation)
    {
        var binding = AnimationUtility.GetObjectReferenceCurveBindings(animation).Single();
        return AnimationUtility.GetObjectReferenceCurve(animation, binding)
            .Select(key => (Sprite)key.value).Distinct().ToArray();
    }

    [Test]
    public void MotionSet_ContainsEveryDirectionAndUsesNewSpriteAssets()
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        Assert.That(motions, Is.Not.Null);
        Assert.That(motions.IsConfigured, Is.True);
        Assert.That(motions.HasAnimatorConfiguration, Is.True);
        Assert.That(PlayerPrefab.GetComponent<Animator>().runtimeAnimatorController, Is.EqualTo(motions.Controller));
        var controller = (UnityEditor.Animations.AnimatorController)motions.Controller;
        var states = controller.layers[0].stateMachine.states;
        Assert.That(PlayerPrefab.GetComponent<PlayerSpriteMotion>(), Is.Not.Null);
        foreach (PlayerMotion motion in System.Enum.GetValues(typeof(PlayerMotion)))
        foreach (PlayerAimFacing facing in new[] { PlayerAimFacing.Down, PlayerAimFacing.Up, PlayerAimFacing.Left, PlayerAimFacing.Right })
        {
            PlayerMotionSet.Clip clip = motions.GetClip(motion);
            AnimationClip animation = clip.Animation(facing);
            Sprite[] frames = Frames(animation);
            Assert.That(animation.length, Is.GreaterThan(0f));
            bool looping = motion == PlayerMotion.Idle || motion == PlayerMotion.Move ||
                motion == PlayerMotion.ToolIdle || motion == PlayerMotion.ToolMove;
            Assert.That(animation.isLooping, Is.EqualTo(looping));
            Assert.That(states.Any(state => state.state.name == $"{motion}_{facing}" && state.state.motion == animation), Is.True);
            var binding = AnimationUtility.GetObjectReferenceCurveBindings(animation).Single();
            Assert.That(binding.type, Is.EqualTo(typeof(SpriteRenderer)));
            Assert.That(binding.propertyName, Is.EqualTo("m_Sprite"));
            Assert.That(binding.path, Is.Empty);
            var keys = AnimationUtility.GetObjectReferenceCurve(animation, binding);
            Assert.That(keys.Take(4).Select(key => key.value), Is.EqualTo(frames));
            Assert.That(keys.Last().time + 1f / animation.frameRate, Is.EqualTo(animation.length).Within(.001f));
            Assert.That(frames.Length, Is.EqualTo(4));
            foreach (Sprite sprite in frames)
            {
                Assert.That(AssetDatabase.GetAssetPath(sprite), Does.StartWith("Assets/Art/Sprites/Player/Motions/"));
                Assert.That(sprite.rect.size, Is.EqualTo(new Vector2(160, 160)));
            }
        }
    }

    [Test]
    public void WeaponSpecificVisualsUseDistinctPosesAndAuthoredProjectileEffects()
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        foreach (PlayerAimFacing facing in new[] { PlayerAimFacing.Down, PlayerAimFacing.Up, PlayerAimFacing.Left, PlayerAimFacing.Right })
        {
            Assert.That(Frames(motions.GetClip(PlayerMotion.ToolIdle).Animation(facing))[0],
                Is.Not.EqualTo(Frames(motions.GetClip(PlayerMotion.Idle).Animation(facing))[0]));
            Assert.That(motions.GetClip(PlayerMotion.ToolMove).Animation(facing),
                Is.Not.EqualTo(motions.GetClip(PlayerMotion.Move).Animation(facing)));
        }
        var rig = PlayerPrefab.GetComponentInChildren<PlayerWeaponRig>(true);
        var rigData = new SerializedObject(rig);
        Assert.That(rigData.FindProperty("_bodyIncludesGunSprite").boolValue, Is.False);
        Assert.That(rigData.FindProperty("_separatedWeaponVisual").objectReferenceValue,
            Is.EqualTo(PlayerPrefab.GetComponent<PlayerWeaponVisual>()));
        foreach (string direction in new[] { "down", "up", "left", "right" })
        {
            var muzzle = (Transform)rigData.FindProperty("_" + direction + "Muzzle").objectReferenceValue;
            Assert.That(muzzle, Is.Not.Null);
            var gun = (Transform)rigData.FindProperty("_" + direction + "Gun").objectReferenceValue;
            Assert.That(gun.GetComponent<SpriteRenderer>().enabled, Is.False);
        }
        var bullet = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerBullet.prefab");
        var view = bullet.GetComponent<PlayerProjectileView>();
        Assert.That(view, Is.Not.Null);
        var animator = bullet.GetComponent<Animator>();
        var controller = (UnityEditor.Animations.AnimatorController)animator.runtimeAnimatorController;
        Assert.That(controller.layers[0].stateMachine.states.Select(state => state.state.name),
            Is.EquivalentTo(new[] { "Warm", "Cryo" }));
        var viewData = new SerializedObject(view);
        Assert.That(viewData.FindProperty("_impactPrefab").objectReferenceValue, Is.Not.Null);
    }

    [Test]
    public void SpannerPrefabUsesSpriteAndAuthoredImpactPrefab()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerSpanner.prefab");
        var renderer = prefab.GetComponent<SpriteRenderer>();
        Assert.That(renderer, Is.Not.Null);
        Assert.That(prefab.GetComponent<MeshFilter>(), Is.Null);
        Assert.That(renderer.sprite, Is.Not.Null);
        Assert.That(renderer.sprite.rect.size, Is.EqualTo(new Vector2(30, 24)));
        Assert.That(renderer.sprite.pixelsPerUnit, Is.EqualTo(32));
        Assert.That(AssetDatabase.GetAssetPath(renderer.sprite), Does.EndWith("/Spanner.png"));
        var data = new SerializedObject(prefab.GetComponent<PlayerSpannerWeapon>());
        var impact = (ParticleSystem)data.FindProperty("_impactPrefab").objectReferenceValue;
        Assert.That(impact, Is.Not.Null);
        Assert.That(PrefabUtility.IsPartOfPrefabAsset(impact), Is.True);
        Assert.That(impact.main.loop, Is.False);
        Assert.That(impact.main.playOnAwake, Is.False);
        Assert.That(impact.GetComponent<ParticleSystemRenderer>().sharedMaterial, Is.Not.Null);
    }

    [Test]
    public void HoverActions_KeepOriginalTimingAndHaveMatchingAnimatorStates()
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var controller = (UnityEditor.Animations.AnimatorController)motions.Controller;
        var states = controller.layers[0].stateMachine.states;
        foreach (PlayerMotion action in new[] { PlayerMotion.Fire, PlayerMotion.Attack, PlayerMotion.Repair })
        foreach (PlayerAimFacing facing in new[] { PlayerAimFacing.Down, PlayerAimFacing.Up, PlayerAimFacing.Left, PlayerAimFacing.Right })
        {
            var grounded = motions.GetClip(action);
            var hovering = motions.GetClip(action, true);
            Assert.That(hovering, Is.Not.SameAs(grounded));
            var animation = hovering.Animation(facing);
            Sprite[] frames = Frames(animation);
            Assert.That(animation.length, Is.EqualTo(grounded.Animation(facing).length).Within(.001f));
            Assert.That(animation.isLooping, Is.False);
            Assert.That(states.Any(state => state.state.name == $"Hover{action}_{facing}" && state.state.motion == animation), Is.True);
            Assert.That(frames.Length, Is.EqualTo(4));
            foreach (Sprite sprite in frames)
                Assert.That(AssetDatabase.GetAssetPath(sprite), Does.StartWith($"Assets/Art/Sprites/Player/Motions/V2/Hover{action}/"));
        }
    }

    [UnityTest]
    public IEnumerator MovingDuringAction_ChangesHoverPoseWithoutRestartingActionOrMovingRoot()
    {
        yield return new EnterPlayMode();
        GameObject player = null;
        try
        {
            player = Object.Instantiate(PlayerPrefab, new Vector3(12000, 12000, 0), Quaternion.identity);
            var motion = player.GetComponent<PlayerSpriteMotion>();
            var input = player.GetComponent<PlayerInputHandler>();
            var animator = player.GetComponent<Animator>();
            var rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
            var setMove = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.MoveInput)).GetSetMethod(true);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var evaluate = typeof(PlayerSpriteMotion).GetMethod("UpdateAnimatorState", flags);
            var elapsed = typeof(PlayerSpriteMotion).GetField("_elapsed", flags);
            Vector3 position = player.transform.position;
            foreach (PlayerMotion action in new[] { PlayerMotion.Fire, PlayerMotion.Attack, PlayerMotion.Repair })
            {
                setMove.Invoke(input, new object[] { Vector2.zero });
                if (action == PlayerMotion.Fire) motion.PlayRangedShot();
                else Assert.That(motion.PlayToolAction(action == PlayerMotion.Repair), Is.True);
                evaluate.Invoke(motion, null);
                float time = motions.GetClip(action).Animation(rig.Facing).length * .25f;
                elapsed.SetValue(motion, time);

                foreach (Vector2 movement in new[] { Vector2.left, Vector2.right, Vector2.up, Vector2.zero, Vector2.right })
                {
                    bool moving = movement.sqrMagnitude > 0;
                    setMove.Invoke(input, new object[] { movement });
                    Assert.That(rig.TryGetMuzzle(out Transform muzzle), Is.True);
                    var rigData = new SerializedObject(rig);
                    string poseField = movement.x < 0 ? "_leftLeanMuzzles" : movement.x > 0 ? "_rightLeanMuzzles" : "_firingMuzzles";
                    var pose = rigData.FindProperty(poseField);
                    Vector2 expectedMuzzle = pose.FindPropertyRelative(rig.Facing.ToString().ToLowerInvariant()).vector2Value;
                    Assert.That(Vector2.Distance((Vector2)rig.transform.InverseTransformPoint(muzzle.position), expectedMuzzle), Is.LessThan(.002f));
                    evaluate.Invoke(motion, null);
                    animator.Update(0);
                    var state = animator.GetCurrentAnimatorStateInfo(0);
                    string prefix = moving ? "Hover" : "";
                    string lean = action == PlayerMotion.Fire && movement.x != 0
                        ? (movement.x < 0 ? "LeanLeft" : "LeanRight") : "";
                    Assert.That(state.fullPathHash, Is.EqualTo(Animator.StringToHash($"Base Layer.{prefix}{action}{lean}_{rig.Facing}")));
                    Assert.That(state.normalizedTime, Is.EqualTo(.25f).Within(.001f));
                    Assert.That((float)elapsed.GetValue(motion), Is.EqualTo(time).Within(.001f));
                    Assert.That(player.transform.position, Is.EqualTo(position));
                }
                motion.CancelToolAction();
            }
        }
        finally { if (player != null) Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    [Test]
    public void LateralLean_KeepsAimDirectionAndOriginalClipDuration()
    {
        var motions = AssetDatabase.LoadAssetAtPath<PlayerMotionSet>("Assets/Data/Player/PlayerMotions.asset");
        var controller = (UnityEditor.Animations.AnimatorController)motions.Controller;
        var states = controller.layers[0].stateMachine.states;
        foreach (PlayerMotion action in new[] { PlayerMotion.Move, PlayerMotion.Fire })
        foreach (PlayerLateralLean lean in new[] { PlayerLateralLean.Left, PlayerLateralLean.Right })
        foreach (PlayerAimFacing facing in new[] { PlayerAimFacing.Down, PlayerAimFacing.Up, PlayerAimFacing.Left, PlayerAimFacing.Right })
        {
            bool hovering = action == PlayerMotion.Fire;
            var original = motions.GetClip(action, hovering);
            var banked = motions.GetClip(action, hovering, lean);
            Assert.That(banked, Is.Not.SameAs(original));
            var animation = banked.Animation(facing);
            Sprite[] frames = Frames(animation);
            Assert.That(animation.length, Is.EqualTo(original.Animation(facing).length).Within(.001f));
            Assert.That(animation.isLooping, Is.EqualTo(action == PlayerMotion.Move));
            string name = $"{(hovering ? "Hover" : "")}{action}Lean{lean}_{facing}";
            Assert.That(states.Any(state => state.state.name == name && state.state.motion == animation), Is.True);
            Assert.That(frames.Length, Is.EqualTo(1));
            if (action == PlayerMotion.Fire)
                Assert.That(frames[0], Is.EqualTo(Frames(motions.GetClip(PlayerMotion.Move, false, lean).Animation(facing))[0]),
                    "Moving fire must keep the exact movement pose.");
            var opposite = motions.GetClip(action, hovering,
                lean == PlayerLateralLean.Left ? PlayerLateralLean.Right : PlayerLateralLean.Left);
            Assert.That(frames[0], Is.Not.EqualTo(Frames(opposite.Animation(facing))[0]));
        }
    }

    [UnityTest]
    public System.Collections.IEnumerator Backpedal_OppositeMovementUsesNewPoseWithoutMovingPhysicsRoot()
    {
        yield return new EnterPlayMode();
        GameObject player = null;
        try
        {
            player = Object.Instantiate(PlayerPrefab, new Vector3(12000, 12000, 0), Quaternion.identity);
            var input = player.GetComponent<PlayerInputHandler>();
            var rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            var motion = player.GetComponent<PlayerSpriteMotion>();
            var animator = player.GetComponent<Animator>();
            var renderer = player.GetComponent<SpriteRenderer>();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var evaluate = typeof(PlayerSpriteMotion).GetMethod("UpdateAnimatorState", flags);
            var setMove = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.MoveInput)).GetSetMethod(true);
            var setAim = typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.AimDirection)).GetSetMethod(true);
            Vector3 root = player.transform.position;
            foreach (bool aimingLeft in new[] { true, false })
            {
                Vector2 aim = aimingLeft ? Vector2.left : Vector2.right;
                setAim.Invoke(input, new object[] { aim });
                setMove.Invoke(input, new object[] { -aim });
                rig.RefreshPose(aim);
                motion.PlayRangedShot();
                evaluate.Invoke(motion, null);
                animator.Update(0f);
                string facing = aimingLeft ? "Left" : "Right";
                string movement = aimingLeft ? "Right" : "Left";
                var expected = AssetDatabase.LoadAssetAtPath<Sprite>(
                    $"Assets/Art/Sprites/Player/Motions/V2/Backpedal/Backpedal_{facing}.png");
                Assert.That(renderer.sprite, Is.EqualTo(expected));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).fullPathHash,
                    Is.EqualTo(Animator.StringToHash($"Base Layer.HoverFireLean{movement}_{facing}")));
                Assert.That(rig.TryGetMuzzle(out Transform muzzle), Is.True);
                Assert.That(Vector2.Dot((Vector2)(muzzle.position - player.transform.position), aim), Is.GreaterThan(0f));
                Assert.That(player.transform.position, Is.EqualTo(root));
                Assert.That(renderer.flipX, Is.False, "Each direction has its own authored sprite.");
            }
        }
        finally { if (player != null) Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }

    [Test]
    public void DirectionalColliders_SelectOnePairAndPreserveManualPositions()
    {
        var player = Object.Instantiate(PlayerPrefab);
        try
        {
            var selector = player.GetComponent<PlayerDirectionalColliders>();
            Assert.That(selector.IsConfigured, Is.True);
            var data = new SerializedObject(selector);
            var directions = data.FindProperty("_directions");
            Assert.That(directions.arraySize, Is.EqualTo(8));
            Collider2D[] colliders = Enumerable.Range(0, directions.arraySize).SelectMany(i =>
            {
                var pair = directions.GetArrayElementAtIndex(i);
                Assert.That(pair.FindPropertyRelative("facing").intValue, Is.EqualTo(i));
                return new[] { (Collider2D)pair.FindPropertyRelative("body").objectReferenceValue,
                    (Collider2D)pair.FindPropertyRelative("bullet").objectReferenceValue };
            }).ToArray();
            Assert.That(colliders.Distinct().Count(), Is.EqualTo(16));

            colliders[1].transform.localPosition = new Vector3(-.3f, .2f, 0f);
            colliders[2].transform.localPosition = new Vector3(.4f, -.1f, 0f);
            Vector3[] positions = colliders.Select(collider => collider.transform.localPosition).ToArray();
            Vector2[] offsets = colliders.Select(collider => collider.offset).ToArray();
            foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
            {
                selector.SetFacing(facing);
                for (int i = 0; i < colliders.Length; i++)
                {
                    Assert.That(colliders[i].enabled, Is.EqualTo(i / 2 == (int)facing));
                    Assert.That(colliders[i].isTrigger, Is.EqualTo(i % 2 == 1));
                    Assert.That(colliders[i].attachedRigidbody, Is.EqualTo(player.GetComponent<Rigidbody2D>()));
                    Assert.That(colliders[i].transform.localPosition, Is.EqualTo(positions[i]));
                    Assert.That(colliders[i].offset, Is.EqualTo(offsets[i]));
                }
            }
        }
        finally { Object.DestroyImmediate(player); }
    }

    [UnityTest]
    public IEnumerator WeaponSwitch_CyclesSpannerAndRestoresDirectionalGun()
    {
        yield return new EnterPlayMode();
        GameObject player = null;
        try
        {
            player = Object.Instantiate(PlayerPrefab, new Vector3(12000, 12000, 0), Quaternion.identity);
            var selection = player.GetComponent<PlayerWeaponSelection>();
            PlayerWeaponRig rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            PlayerSpannerWeapon spanner = player.GetComponentInChildren<PlayerSpannerWeapon>(true);
            Assert.That(spanner, Is.Not.Null);
            Assert.That(spanner.GetComponent<PlayerSpannerRepair>(), Is.Not.Null);
            Assert.That(spanner.gameObject.activeSelf, Is.False);
            selection.SelectNextWeapon();
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.CryoBlaster));
            selection.SelectNextWeapon();
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Sniper));
            selection.SelectNextWeapon();
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
            Assert.That(spanner.gameObject.activeSelf, Is.True);
            var rigData = new SerializedObject(rig);
            foreach (string field in new[] { "_leftGun", "_rightGun", "_upGun", "_downGun" })
                Assert.That(((Transform)rigData.FindProperty(field).objectReferenceValue).gameObject.activeSelf, Is.False);
            Assert.That(spanner.TryAttack(Vector2.zero), Is.False);
            Assert.That(spanner.TryAttack(Vector2.down), Is.True);
            var spannerData = new SerializedObject(spanner);
            Assert.That(spannerData.FindProperty("_pendingStrike").boolValue, Is.True);
            Assert.That(spannerData.FindProperty("_impactAt").floatValue, Is.GreaterThan(Time.time));
            Assert.That(player.GetComponent<PlayerSpriteMotion>().IsPlayingToolAction, Is.True);
            Assert.That(spanner.TryAttack(Vector2.down), Is.False, "Holding attack must respect the melee cooldown.");
            selection.SelectNextWeapon();
            Assert.That(selection.CurrentWeapon, Is.EqualTo(WeaponType.DefaultGun));
            Assert.That(spanner.gameObject.activeSelf, Is.False);
            spannerData.Update();
            Assert.That(spannerData.FindProperty("_pendingStrike").boolValue, Is.False,
                "Switching weapons must cancel a strike before it applies damage or repair.");
            Assert.That(player.GetComponent<PlayerSpriteMotion>().IsPlayingToolAction, Is.False);
            Assert.That(rig.ActiveGun.gameObject.activeSelf, Is.True);
        }
        finally { if (player != null) Object.DestroyImmediate(player); }
        yield return new ExitPlayMode();
    }
    [Test]
    public void SpannerSlashPrefab_UsesAuthoredSpritesWithoutPhysicsColliders()
    {
        var spanner = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerSpanner.prefab");
        var slash = (PlayerSpannerSlash)new SerializedObject(spanner.GetComponent<PlayerSpannerWeapon>())
            .FindProperty("_slashPrefab").objectReferenceValue;
        Assert.That(slash, Is.Not.Null);
        Assert.That(PrefabUtility.IsPartOfPrefabAsset(slash), Is.True);
        Assert.That(slash.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
        var animator = slash.GetComponent<Animator>();
        Assert.That(animator.runtimeAnimatorController, Is.Not.Null);
        Assert.That(animator.runtimeAnimatorController.animationClips.All(clip => !clip.isLooping), Is.True);
        Assert.That(slash.GetComponent<PlayerSpriteEffect>(), Is.Not.Null);
        Assert.That(slash.GetComponentsInChildren<Collider2D>(true), Is.Empty);
    }

    [UnityTest]
    public IEnumerator SpannerMiss_PlaysSlashAtStrikeWithLockedAimAndWorldRange()
    {
        yield return new EnterPlayMode();
        var owner = new GameObject("Spanner slash test owner", typeof(SpriteRenderer));
        owner.transform.position = new Vector3(12000f, 12000f, 67f);
        owner.transform.localScale = new Vector3(2f, 3f, 1f);
        owner.GetComponent<SpriteRenderer>().sortingOrder = 37;
        try
        {
            PlayerSpannerWeapon spanner = CreateSpannerForSlashTest(owner.transform);
            Vector2 direction = new Vector2(-1f, 1f).normalized;
            Assert.That(spanner.TryAttack(direction), Is.True);
            Assert.That(spanner.TryAttack(Vector2.down), Is.False, "A repeated input during windup must not schedule another effect.");
            Assert.That(FindTestSpannerSlashes(), Is.Empty, "Starting a windup must not create the strike effect.");
            owner.transform.position += Vector3.left * .3f;
            Vector3 strikeOrigin = owner.transform.position;
            float deadline = Time.time + .3f;
            while (FindTestSpannerSlashes().Length == 0 && Time.time < deadline) yield return null;
            var effects = FindTestSpannerSlashes();
            Assert.That(effects.Length, Is.EqualTo(1), "A missed attack still shows exactly one range slash at impact.");
            var slash = effects[0];
            Assert.That(Vector3.Distance(slash.transform.position, strikeOrigin), Is.LessThan(.001f));
            Assert.That(Vector2.Distance(slash.transform.right, direction), Is.LessThan(.001f));
            Assert.That(slash.transform.parent, Is.Null, "The effect must remain at the strike instead of following the hand.");
            Assert.That(slash.gameObject.scene, Is.EqualTo(owner.scene));
            float authoredRadius = new SerializedObject(slash).FindProperty("_authoredRadius").floatValue;
            float range = new SerializedObject(spanner).FindProperty("_range").floatValue;
            Assert.That(slash.transform.lossyScale.x * authoredRadius, Is.EqualTo(range).Within(.001f));
            Assert.That(slash.transform.lossyScale.y, Is.EqualTo(slash.transform.lossyScale.x).Within(.001f));
            var renderer = slash.GetComponent<SpriteRenderer>();
            Assert.That(renderer.sortingLayerID, Is.EqualTo(owner.GetComponent<SpriteRenderer>().sortingLayerID));
            Assert.That(renderer.sortingOrder, Is.GreaterThan(owner.GetComponent<SpriteRenderer>().sortingOrder));
            owner.transform.position += Vector3.right * 3f;
            yield return null;
            Assert.That(Vector3.Distance(slash.transform.position, strikeOrigin), Is.LessThan(.001f));
        }
        finally
        {
            foreach (var slash in FindTestSpannerSlashes()) Object.DestroyImmediate(slash.gameObject);
            Object.DestroyImmediate(owner);
        }
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator RepairIntentAndCancelledSpannerStrikes_DoNotPlayAttackSlash()
    {
        yield return new EnterPlayMode();
        var owner = new GameObject("Spanner slash exclusion test owner", typeof(SpriteRenderer));
        owner.transform.position = new Vector3(12000f, 12000f, 67f);
        try
        {
            foreach (bool repairIntent in new[] { true, false })
            {
                PlayerSpannerWeapon spanner = CreateSpannerForSlashTest(owner.transform);
                Assert.That(spanner.TryAttack(Vector2.right), Is.True);
                if (repairIntent)
                {
                    // 수리 대상 생성 없이 예약된 수리 분기 자체가 공격 검기를 생략하는지 확인합니다.
                    typeof(PlayerSpannerWeapon).GetField("_repairIntent",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                        .SetValue(spanner, true);
                }
                else spanner.gameObject.SetActive(false);
                yield return new WaitForSeconds(.16f);
                Assert.That(FindTestSpannerSlashes(), Is.Empty,
                    repairIntent ? "A repair strike must preserve its repair visuals without an attack slash." : "Cancelling windup must prevent its slash.");
                Object.DestroyImmediate(spanner.gameObject);
            }
        }
        finally
        {
            foreach (var slash in FindTestSpannerSlashes()) Object.DestroyImmediate(slash.gameObject);
            Object.DestroyImmediate(owner);
        }
        yield return new ExitPlayMode();
    }

    private static PlayerSpannerWeapon CreateSpannerForSlashTest(Transform owner)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerSpanner.prefab");
        var spanner = Object.Instantiate(prefab, owner).GetComponent<PlayerSpannerWeapon>();
        Assert.That(new SerializedObject(spanner).FindProperty("_slashPrefab").objectReferenceValue, Is.Not.Null);
        spanner.Bind(owner, null);
        return spanner;
    }

    private static PlayerSpannerSlash[] FindTestSpannerSlashes() =>
        Object.FindObjectsByType<PlayerSpannerSlash>(FindObjectsSortMode.None)
            .Where(slash => Vector2.Distance(slash.transform.position, new Vector2(12000f, 12000f)) < 30f).ToArray();

    [Test]
    public void SpannerTargetingRejectsRearDistantAndBlockedTargets()
    {
        var owner = new GameObject("Spanner owner");
        var target = new GameObject("Repair target");
        var wall = new GameObject("Wall", typeof(BoxCollider2D));
        try
        {
            owner.layer = LayerMask.NameToLayer("PlayerFeetBox");
            owner.transform.position = new Vector3(12000, 12000);
            target.transform.position = owner.transform.position + Vector3.right * 2;
            wall.transform.position = owner.transform.position + Vector3.right;
            Physics2D.SyncTransforms();
            Assert.That(PlayerSpannerTargeting.CanReach(owner.transform, target.transform, target.transform.position,
                Vector2.right, 3, 100), Is.False, "A solid wall must block repairs.");
            wall.GetComponent<BoxCollider2D>().isTrigger = true;
            Assert.That(PlayerSpannerTargeting.CanReach(owner.transform, target.transform, target.transform.position,
                Vector2.right, 3, 100), Is.True);
            Assert.That(PlayerSpannerTargeting.CanReach(owner.transform, target.transform, target.transform.position,
                Vector2.left, 3, 100), Is.False);
            Assert.That(PlayerSpannerTargeting.CanReach(owner.transform, target.transform, target.transform.position,
                Vector2.right, 1, 100), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(owner);
            Object.DestroyImmediate(target);
            Object.DestroyImmediate(wall);
        }
    }
    [Test]
    public void WeaponRig_IsOwnedByPlayer()
    {
        var rig = PlayerPrefab.GetComponentInChildren<PlayerWeaponRig>(true);
        Assert.That(rig.transform.parent, Is.EqualTo(PlayerPrefab.transform));
        Assert.That(Vector3.Distance(rig.transform.localPosition, Vector3.zero), Is.LessThan(.001f));
        Assert.That(new SerializedObject(rig).FindProperty("_playerRoot").objectReferenceValue, Is.EqualTo(PlayerPrefab.transform));
    }
    [TestCase("left")]
    [TestCase("right")]
    [TestCase("up")]
    [TestCase("down")]
    public void Muzzle_IsChildOfItsDirectionalGun(string direction)
    {
        var data = new SerializedObject(PlayerPrefab.GetComponentInChildren<PlayerWeaponRig>(true));
        var gun = (Transform)data.FindProperty("_" + direction + "Gun").objectReferenceValue;
        var muzzle = (Transform)data.FindProperty("_" + direction + "Muzzle").objectReferenceValue;
        Assert.That(muzzle.IsChildOf(gun), Is.True);
        Assert.That(muzzle.localPosition.sqrMagnitude, Is.GreaterThan(0f));
    }
    [Test]
    public void Attack_UsesOwnedMuzzleAnchor()
    {
        var rig = PlayerPrefab.GetComponentInChildren<PlayerWeaponRig>(true);
        var anchor = PlayerPrefab.GetComponentInChildren<PlayerMuzzleAnchor>(true);
        var data = new SerializedObject(PlayerPrefab.GetComponent<PlayerAttack>());
        Assert.That(anchor.WeaponRig, Is.EqualTo(rig));
        Assert.That(data.FindProperty("_muzzleAnchor").objectReferenceValue, Is.EqualTo(anchor));
        Assert.That(data.FindProperty("_weaponRig").objectReferenceValue, Is.EqualTo(rig));
    }
    [Test]
    public void PlayerPrefab_DoesNotAttachLegacyControllers()
    {
        // Legacy 제거로 인해 로직 무효화: 삭제된 타입을 사용하는 직접 조회.
        // GetComponent<Player>(), GetComponentsInChildren<PlayerGunControl/FirePoint>()
        var names = PlayerPrefab.GetComponentsInChildren<MonoBehaviour>(true)
            .Where(component => component != null).Select(component => component.GetType().Name);
        Assert.That(names.Intersect(new[] { "Player", "PlayerGunControl", "FirePoint" }), Is.Empty);
    }
    [TestCase("Player.cs")]
    [TestCase("playerguncontrol.cs")]
    [TestCase("FirePoint.cs")]
    [TestCase("gunflash.cs")]
    [TestCase("bombscript.cs")]
    [TestCase("bombEffect.cs")]
    public void LegacySource_IsRetired(string source) => Assert.That(AssetDatabase.LoadAssetAtPath<MonoScript>("Assets/Scripts/Player/" + source), Is.Null);
    [TestCase(1, 0, PlayerAimFacing.Right)]
    [TestCase(-1, 0, PlayerAimFacing.Left)]
    [TestCase(0, 1, PlayerAimFacing.Up)]
    [TestCase(0, -1, PlayerAimFacing.Down)]
    [TestCase(0, 0, PlayerAimFacing.Down)]
    [TestCase(float.NaN, 1, PlayerAimFacing.Down)]
    [TestCase(float.PositiveInfinity, 1, PlayerAimFacing.Down)]
    public void Facing_UsesValidCardinalDirection(float x, float y, PlayerAimFacing expected) =>
        Assert.That(PlayerAimUtility.GetFacing(new Vector2(x, y)), Is.EqualTo(expected));
}
