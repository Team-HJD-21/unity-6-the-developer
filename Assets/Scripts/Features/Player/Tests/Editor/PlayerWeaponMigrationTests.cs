// 현행 Player Prefab의 무기·총구 연결과 구형 컨트롤러 제거 상태를 검증합니다.
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class PlayerWeaponMigrationTests
{
    private GameObject PlayerPrefab => AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab");

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
