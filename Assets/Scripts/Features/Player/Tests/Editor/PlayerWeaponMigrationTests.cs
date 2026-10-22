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
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
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
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
        {
            Assert.That(Frames(motions.GetClip(PlayerMotion.ToolIdle).Animation(facing))[0],
                Is.Not.EqualTo(Frames(motions.GetClip(PlayerMotion.Idle).Animation(facing))[0]));
            Assert.That(motions.GetClip(PlayerMotion.ToolMove).Animation(facing),
                Is.Not.EqualTo(motions.GetClip(PlayerMotion.Move).Animation(facing)));
        }
        var rig = PlayerPrefab.GetComponentInChildren<PlayerWeaponRig>(true);
        var rigData = new SerializedObject(rig);
        Assert.That(rigData.FindProperty("_bodyIncludesGunSprite").boolValue, Is.True);
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
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
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
        foreach (PlayerAimFacing facing in System.Enum.GetValues(typeof(PlayerAimFacing)))
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
            string[] fields = { "_bodyUpDown", "_bodyLeft", "_bodyRight", "_bulletUpDown", "_bulletLeft", "_bulletRight" };
            var data = new SerializedObject(selector);
            Collider2D[] colliders = fields.Select(field =>
                (Collider2D)data.FindProperty(field).objectReferenceValue).ToArray();
            Assert.That(colliders.Distinct().Count(), Is.EqualTo(6));

            colliders[1].transform.localPosition = new Vector3(-.3f, .2f, 0f);
            colliders[2].transform.localPosition = new Vector3(.4f, -.1f, 0f);
            Vector3[] positions = colliders.Select(collider => collider.transform.localPosition).ToArray();
            Vector2[] offsets = colliders.Select(collider => collider.offset).ToArray();
            foreach (PlayerAimFacing facing in new[] { PlayerAimFacing.Down, PlayerAimFacing.Left,
                         PlayerAimFacing.Right, PlayerAimFacing.Up, PlayerAimFacing.Down })
            {
                selector.SetFacing(facing);
                int active = facing == PlayerAimFacing.Left ? 1 : facing == PlayerAimFacing.Right ? 2 : 0;
                for (int i = 0; i < colliders.Length; i++)
                {
                    Assert.That(colliders[i].enabled, Is.EqualTo(i % 3 == active));
                    Assert.That(colliders[i].isTrigger, Is.EqualTo(i >= 3));
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
            PlayerAttack attack = player.GetComponent<PlayerAttack>();
            PlayerWeaponRig rig = player.GetComponentInChildren<PlayerWeaponRig>(true);
            PlayerSpannerWeapon spanner = player.GetComponentInChildren<PlayerSpannerWeapon>(true);
            Assert.That(spanner, Is.Not.Null);
            Assert.That(spanner.GetComponent<PlayerSpannerRepair>(), Is.Not.Null);
            Assert.That(spanner.gameObject.activeSelf, Is.False);
            attack.SelectNextWeapon();
            Assert.That(attack.CurrentWeapon, Is.EqualTo(WeaponType.CryoBlaster));
            attack.SelectNextWeapon();
            Assert.That(attack.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
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
            attack.SelectNextWeapon();
            Assert.That(attack.CurrentWeapon, Is.EqualTo(WeaponType.DefaultShotgun));
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
    public void SpannerTargetingRejectsRearDistantAndBlockedTargets()
    {
        var owner = new GameObject("Spanner owner");
        var target = new GameObject("Repair target");
        var wall = new GameObject("Wall", typeof(BoxCollider2D));
        try
        {
            owner.layer = LayerMask.NameToLayer("Player");
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
