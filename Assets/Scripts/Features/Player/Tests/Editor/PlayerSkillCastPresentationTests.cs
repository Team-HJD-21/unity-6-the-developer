using System;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

public sealed class PlayerSkillCastPresentationTests
{
    private GameObject _player;
    private Scene _scene;
    private Tile _tile;
    private Clock _clock;
    private PlayerSkillController _skills;
    private PlayerSkillCastPresentation _cast;
    private Tilemap _ground;
    private static readonly Vector3 Origin = new(64000, 64000);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _scene = SceneManager.CreateScene("Skill casting integration");
        var grid = new GameObject("Casting ground", typeof(Grid));
        SceneManager.MoveGameObjectToScene(grid, _scene); grid.transform.position = Origin;
        var ground = new GameObject("Ground", typeof(Tilemap)); ground.transform.SetParent(grid.transform, false);
        _ground = ground.GetComponent<Tilemap>();
        _tile = ScriptableObject.CreateInstance<Tile>(); _tile.colliderType = Tile.ColliderType.None;
        for (int x=-12;x<=12;x++) for(int y=-12;y<=12;y++) _ground.SetTile(new Vector3Int(x,y), _tile);
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), Origin, Quaternion.identity);
        SceneManager.MoveGameObjectToScene(_player, _scene);
        _player.GetComponent<PlayerInputHandler>().enabled = false;
        _player.GetComponent<PlayerMovement>().enabled = false;
        _player.GetComponent<Rigidbody2D>().simulated = false;
        _clock = new Clock(); FieldSimulationActor.Attach(_player, _clock);
        var input = _player.GetComponent<PlayerInputHandler>();
        typeof(PlayerInputHandler).GetProperty("AimDirection").SetValue(input, Vector2.right);
        typeof(PlayerInputHandler).GetProperty("HasAimTarget").SetValue(input, true);
        typeof(PlayerInputHandler).GetProperty("MouseWorldPosition").SetValue(input, (Vector2)(Origin+Vector3.right*5));
        _skills = _player.GetComponent<PlayerSkillController>();
        _cast = _player.GetComponent<PlayerSkillCastPresentation>();
        typeof(PlayerPlacementSkillBase).GetField("_placementGround", BindingFlags.Instance|BindingFlags.NonPublic)
            .SetValue(_player.GetComponent<PlayerBomb>(), _ground);
        Assert.That(_cast != null && _cast.IsConfigured, Is.True);
        Assert.That(_skills.TryEquip(0, PlayerSkillId.Bomb), Is.True);
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach (var root in _scene.GetRootGameObjects()) Object.Destroy(root);
        Object.Destroy(_tile);
        yield return null;
        yield return SceneManager.UnloadSceneAsync(_scene);
        yield return new ExitPlayMode();
    }

    private void PlaceBomb()
    {
        Assert.That(_skills.TryUseSlot(0), Is.True);
        Assert.That(_player.GetComponent<PlayerBomb>().TryConfirmPlacement(Origin+Vector3.right*4), Is.True,
            _player.GetComponent<PlayerBomb>().PlacementFailure);
    }

    [UnityTest]
    public IEnumerator PreviewCancelAndRejectedPositionsNeverPlayCastingButSuccessfulPlacementDoes()
    {
        int count = _skills.BombCount;
        Assert.That(_skills.TryUseSlot(0), Is.True);
        Assert.That(_cast.IsPlaying, Is.False, "Opening a preview is not a cast.");
        Assert.That(_player.GetComponent<PlayerBomb>().TryConfirmPlacement(Origin+Vector3.right*50), Is.False);
        Assert.That(_cast.IsPlaying, Is.False);
        Assert.That(_skills.TryUseSlot(0), Is.True);
        Assert.That(_cast.IsPlaying, Is.False, "The same key cancels without playing a cast.");
        Assert.That(_skills.BombCount, Is.EqualTo(count));
        PlaceBomb();
        Assert.That(_cast.IsPlaying, Is.True);
        Assert.That(_skills.BombCount, Is.EqualTo(count-1));
        yield break;
    }

    [UnityTest]
    public IEnumerator WristPanelReachAndRecoveryPreserveRootColliderAndReturnTheWeapon()
    {
        // 코루틴은 Update 중 재개됩니다. 총구 동기화 뒤의 최종 표시 단계를 평가한 값을 검사합니다.
        void SamplePresentation() => typeof(PlayerSkillCastPresentation).GetMethod("LateUpdate",
            BindingFlags.Instance|BindingFlags.NonPublic).Invoke(_cast,null);
        var weapon = _player.GetComponent<PlayerWeaponVisual>();
        weapon.Refresh(); Vector3 normalGun = weapon.Renderer.transform.localPosition;
        var colliders = _player.GetComponentsInChildren<Collider2D>(true);
        var offsets = Array.ConvertAll(colliders, item => item.offset);
        var scales = Array.ConvertAll(colliders, item => item.transform.localScale);
        PlaceBomb();
        _clock.Now = .12; yield return null;SamplePresentation();
        Assert.That(_cast.IsPlaying, Is.True);
        Assert.That(_player.GetComponent<SpriteRenderer>().sprite.name, Is.EqualTo("CastBody"));
        var rig = _player.transform.Find("Skill Cast Presentation");
        Assert.That(rig.Find("Wrist panel").GetComponent<SpriteRenderer>().enabled, Is.True);
        Assert.That(Vector3.Distance(normalGun, weapon.Renderer.transform.localPosition), Is.GreaterThan(.05f));
        _clock.Now = .26; yield return null;SamplePresentation();
        Assert.That(rig.Find("Wrist panel").GetComponent<SpriteRenderer>().enabled, Is.False);
        Assert.That(_player.transform.position, Is.EqualTo(Origin));
        for(int i=0;i<colliders.Length;i++)
        {Assert.That(colliders[i].offset, Is.EqualTo(offsets[i]));Assert.That(colliders[i].transform.localScale, Is.EqualTo(scales[i]));}
        _clock.Now = .4; yield return null;SamplePresentation();
        Assert.That(_cast.IsPlaying, Is.False);
        Assert.That(_player.GetComponent<SpriteRenderer>().sprite.name, Does.StartWith("Side_"));
        foreach(var renderer in rig.GetComponentsInChildren<SpriteRenderer>()) Assert.That(renderer.enabled, Is.False);
        Assert.That(Vector3.Distance(normalGun,weapon.Renderer.transform.localPosition), Is.LessThan(.001f));
    }

    [UnityTest]
    public IEnumerator FiringInterruptsCastingImmediatelyWithoutDelayingTheAttack()
    {
        PlaceBomb(); _clock.Now=.12;yield return null;
        Assert.That(_player.GetComponent<PlayerAttack>().TryFire(), Is.True);
        Assert.That(_cast.IsPlaying, Is.False);
    }

    [UnityTest]
    public IEnumerator UiLockInterruptsCastingAndHidesEveryTemporaryRenderer()
    {
        PlaceBomb(); _clock.Now=.12;yield return null;
        _player.GetComponent<PlayerControlState>().SetInputBlocked(true);
        yield return null;
        Assert.That(_cast.IsPlaying, Is.False);
        foreach(var renderer in _player.transform.Find("Skill Cast Presentation").GetComponentsInChildren<SpriteRenderer>())
            Assert.That(renderer.enabled, Is.False);
    }

    [UnityTest]
    public IEnumerator TurretSelectionDoesNotCastUntilADeploymentSucceeds()
    {
        var research = _player.GetComponent<PlayerSkillResearch>();
        Assert.That(research.TryUnlock(PlayerSkillId.PlayerTurret), Is.True);
        Assert.That(PlayerSkillResearchTestHub.Assign(research,1,PlayerSkillId.PlayerTurret), Is.True);
        Assert.That(_skills.TryEquip(1,PlayerSkillId.PlayerTurret), Is.True);
        Assert.That(_skills.TryUseSlot(1), Is.True);
        Assert.That(_cast.IsPlaying, Is.False);
        var turret = _player.GetComponent<PlayerTurretSkill>();
        typeof(PlayerTurretSkill).GetField("_placementGround",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(turret,_ground);
        Assert.That(turret.TrySelectSlot(0), Is.True);
        Assert.That(_cast.IsPlaying, Is.False);
        Assert.That(turret.TryConfirmPlacement(Origin+Vector3.right*4), Is.True, turret.PlacementFeedback.ToString());
        Assert.That(_cast.IsPlaying, Is.True);
        Assert.That(turret.CurrentTurretCount, Is.EqualTo(2));
        yield break;
    }

    [UnityTest]
    public IEnumerator DualWieldCastsOnlyOnSuccessAndKeepsItsExistingDurationAndCooldown()
    {
        _player.GetComponent<PlayerExperience>().AddExperience(400);
        var research = _player.GetComponent<PlayerSkillResearch>();
        Assert.That(research.TryUnlock(PlayerSkillId.DualWield), Is.True);
        Assert.That(PlayerSkillResearchTestHub.Assign(research,1, PlayerSkillId.DualWield), Is.True);
        Assert.That(_skills.TryEquip(1, PlayerSkillId.DualWield), Is.True);
        Assert.That(_skills.TryUseSlot(1), Is.True);
        Assert.That(_cast.IsPlaying, Is.True);
        var dual = _player.GetComponent<PlayerDualWieldSkill>();
        Assert.That(dual.IsActive, Is.True);
        Assert.That(dual.ActiveRemaining, Is.EqualTo(8).Within(.001f));
        Assert.That(dual.RemainingCooldown, Is.EqualTo(20).Within(.001f));
        _clock.Now=.4;yield return null;
        Assert.That(_cast.IsPlaying, Is.False);
        Assert.That(_skills.TryUseSlot(1), Is.False);
        Assert.That(_cast.IsPlaying, Is.False, "Cooldown rejection must not restart a cast.");
        Assert.That(_player.GetComponent<PlayerWeaponVisual>().IsDualWieldVisible, Is.True);
    }

    private sealed class Clock : IMatchTime
    {
        public double Now;
        private readonly Guid _epoch = Guid.NewGuid();
        public bool IsAvailable => true;
        public double ElapsedSeconds => Now;
        public MatchTimeSnapshot Snapshot => new(new MatchId("cast-test"), _epoch, 0, Now, Now, 1, false, true);
    }
}
