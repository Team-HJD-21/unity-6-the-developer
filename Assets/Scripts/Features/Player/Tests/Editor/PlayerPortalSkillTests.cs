using System;
using System.Collections;
using System.IO;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

/// <summary>기존 씬/프리팹을 수정하지 않는 별도 Unity 실행 환경에서 포털 스킬 자체를 검증합니다.</summary>
public sealed class PlayerPortalSkillTests
{
    private Scene _scene;
    private GameObject _actor;
    private PlayerPortalSkill _skill;
    private Tilemap _ground;
    private Tile _tile;
    private Clock _clock;
    private CancellationTokenSource _lifetime;
    private Vector3 _origin = new(56000, 56000, -2);

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _scene = SceneManager.CreateScene("Portal skill isolated test");
        _lifetime = new CancellationTokenSource(); _clock = new Clock();
        var grid = InScene(new GameObject("Portal ground", typeof(Grid)));
        grid.transform.position = new Vector3(_origin.x, _origin.y, 0);
        var map = new GameObject("Ground", typeof(Tilemap), typeof(TilemapCollider2D));
        map.transform.SetParent(grid.transform, false); _ground = map.GetComponent<Tilemap>();
        _tile = ScriptableObject.CreateInstance<Tile>(); _tile.colliderType = Tile.ColliderType.None;
        for (int x = -5; x <= 25; x++) for (int y = -5; y <= 10; y++)
            _ground.SetTile(new Vector3Int(x, y, 0), _tile);
        var template = InScene(new GameObject("Portal fixture template", typeof(SpriteRenderer), typeof(PlayerPortalEndpoint)));
        template.transform.position = _origin + Vector3.up * 100;
        var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Player/Skills/BarrierEmitterSprite.png");
        template.GetComponent<SpriteRenderer>().sprite = sprite;
        Assign(template.GetComponent<PlayerPortalEndpoint>(), "_body", template.GetComponent<SpriteRenderer>());
        _actor = InScene(new GameObject("Portal owner")); _actor.SetActive(false); _actor.transform.position = _origin;
        _actor.layer = LayerMask.NameToLayer("PlayerFeetBox");
        var body = _actor.AddComponent<Rigidbody2D>(); body.gravityScale = 0;
        var feet = _actor.AddComponent<BoxCollider2D>(); feet.size = new Vector2(.8f, .6f); feet.offset = new Vector2(.2f, -.4f);
        _actor.AddComponent<PlayerInputHandler>(); _actor.AddComponent<PlayerMovement>();
        _actor.AddComponent<PlayerInfo>();
        _actor.AddComponent<PlayerActorIdentity>().Bind(new PlayerId("portal-player"), 0);
        _skill = _actor.AddComponent<PlayerPortalSkill>();
        Assign(_skill, "_prefab", template.GetComponent<PlayerPortalEndpoint>());
        Assign(_skill, "_previewPrefab", AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/Prefabs/Player/PlayerBeaconPreview.prefab").GetComponent<PlayerSkillPlacementPreview>());
        Assign(_skill, "_placementGround", _ground);
        FieldSimulationActor.Attach(_actor, _clock, _lifetime.Token, false);
        _actor.SetActive(true);
        Physics2D.SyncTransforms();
        yield return null;
    }

    private GameObject InScene(GameObject gameObject) { SceneManager.MoveGameObjectToScene(gameObject, _scene); return gameObject; }
    private static void Assign(Object target, string property, Object value)
    {
        var data = new SerializedObject(target); data.FindProperty(property).objectReferenceValue = value;
        data.ApplyModifiedPropertiesWithoutUndo();
    }
    private Vector3 At(float x, float y = 0) => _origin + new Vector3(x, y, 0);
    private void StandAt(Vector3 feet)
    {
        var collider = _actor.GetComponent<Collider2D>(); Physics2D.SyncTransforms();
        Vector3 root = _actor.transform.position + feet - collider.bounds.center; root.z = _origin.z;
        _actor.GetComponent<Rigidbody2D>().position = root; _actor.transform.position = root; Physics2D.SyncTransforms();
    }
    private void Place(float x, float y = 0)
    {
        Assert.That(_skill.TryUseSkill(), Is.True);
        Assert.That(_skill.TryConfirmPlacement(At(x, y)), Is.True, _skill.PlacementFailure);
    }
    private void Pair() { Place(4); Place(11); }
    private GameObject Block(Vector3 point)
    {
        var wall = InScene(new GameObject("Portal blocker", typeof(BoxCollider2D)));
        wall.transform.position = point;
        wall.GetComponent<Collider2D>().includeLayers = 1 << _actor.layer;
        Physics2D.SyncTransforms(); return wall;
    }

    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        if (_tile != null) Object.DestroyImmediate(_tile);
        _lifetime?.Dispose();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator KeepsOnlyOnePairAndCancellationDoesNotRemoveInstalledEndpoint()
    {
        Place(4); Assert.That(_skill.AvailableCount, Is.EqualTo(1));
        Assert.That(_skill.TryUseSkill(), Is.True);
        Assert.That(_skill.TryConfirmPlacement(At(5)), Is.False, "The two pads cannot overlap.");
        _skill.CancelPlacement(); Assert.That(_skill.GetEndpoint(0), Is.Not.Null);
        Place(11); Assert.That(_skill.IsLinked, Is.True); Assert.That(_skill.AvailableCount, Is.Zero);
        Assert.That(_skill.TryUseSkill(), Is.False); Assert.That(_skill.IsPlacing, Is.False);
        yield break;
    }

    [UnityTest]
    public IEnumerator ExplicitTravelAlignsFeetStopsMovementAndDoesNotAutomaticallyBounceBack()
    {
        Place(4); StandAt(At(4)); Assert.That(_skill.TryTravel(), Is.False);
        StandAt(At(0)); Place(11); StandAt(At(4));
        _actor.GetComponent<Rigidbody2D>().linearVelocity = Vector2.right * 5;
        Assert.That(_skill.TryTravel(), Is.True, _skill.InteractionFailure);
        Assert.That(Vector2.Distance(_actor.GetComponent<Collider2D>().bounds.center, At(11)), Is.LessThan(.01f));
        Assert.That(_actor.transform.position.z, Is.EqualTo(_origin.z));
        Assert.That(_actor.GetComponent<Rigidbody2D>().linearVelocity, Is.EqualTo(Vector2.zero));
        Assert.That(_skill.TryTravel(), Is.False, "The same press cannot cause a second trip.");
        _clock.Now = 1; yield return null;
        Assert.That(Vector2.Distance(_actor.GetComponent<Collider2D>().bounds.center, At(11)), Is.LessThan(.01f));
        Assert.That(_skill.TryTravel(), Is.True); Assert.That(Vector2.Distance(_actor.GetComponent<Collider2D>().bounds.center, At(4)), Is.LessThan(.01f));
    }

    [UnityTest]
    public IEnumerator RechecksDestinationObstaclesAndSeaWithoutMovingThePlayer()
    {
        Pair(); StandAt(At(4)); Vector3 before = _actor.transform.position;
        var obstacle = Block(At(11)); Assert.That(_skill.TryTravel(), Is.False); Assert.That(_actor.transform.position, Is.EqualTo(before));
        Object.DestroyImmediate(obstacle);
        _ground.SetTile(new Vector3Int(11, 0, 0), null);
        Assert.That(_skill.TryTravel(), Is.False); Assert.That(_skill.TravelRemaining, Is.Zero);
        _ground.SetTile(new Vector3Int(11, 0, 0), _tile);
        Assert.That(_skill.TryTravel(), Is.True, _skill.InteractionFailure);
        yield break;
    }

    [UnityTest]
    public IEnumerator RecoversOnlyNearbyOwnedEndpointAndRedeploysItsEmptySlot()
    {
        Pair(); Assert.That(_skill.TryRecoverNearest(), Is.False);
        StandAt(At(4)); var b = _skill.GetEndpoint(1);
        var other = Object.Instantiate(_actor); SceneManager.MoveGameObjectToScene(other, _scene);
        Assert.That(other.GetComponent<PlayerPortalSkill>().NearestInteractable, Is.Null, "Another owner's pads cannot be recovered or used.");
        Object.DestroyImmediate(other);
        Assert.That(_skill.TryRecoverNearest(), Is.True); Assert.That(_skill.IsLinked, Is.False);
        Assert.That(_skill.GetEndpoint(1), Is.SameAs(b)); Assert.That(_skill.GetEndpoint(0), Is.Null);
        Place(7); Assert.That(_skill.GetEndpoint(0).SlotIndex, Is.Zero);
        Assert.That(_skill.IsLinked, Is.True); yield break;
    }

    [UnityTest]
    public IEnumerator RejectsPlacementOutsideLandInsideObstacleAndAfterMatchCancellation()
    {
        Assert.That(_skill.TryUseSkill(), Is.True);
        _ground.SetTile(new Vector3Int(4, 0, 0), null);
        Assert.That(_skill.TryConfirmPlacement(At(4)), Is.False); Assert.That(_skill.AvailableCount, Is.EqualTo(2));
        _ground.SetTile(new Vector3Int(4, 0, 0), _tile);
        var obstacle = Block(At(4)); Assert.That(_skill.TryConfirmPlacement(At(4)), Is.False);
        Object.DestroyImmediate(obstacle); _lifetime.Cancel();
        Assert.That(_skill.TryConfirmPlacement(At(4)), Is.False); Assert.That(_skill.AvailableCount, Is.EqualTo(2));
        yield break;
    }

    [UnityTest]
    public IEnumerator RejectsWallsMenusDeathPausedTimeAndReplicaAuthority()
    {
        Pair(); StandAt(At(2)); var wall = Block(At(3));
        Assert.That(_skill.NearestInteractable, Is.Null); Assert.That(_skill.TryRecoverNearest(), Is.False);
        Object.DestroyImmediate(wall); StandAt(At(4));
        var controls = _actor.AddComponent<PlayerControlState>(); controls.SetInputBlocked(true);
        Assert.That(_skill.TryTravel(), Is.False); Assert.That(_skill.TryRecoverNearest(), Is.False);
        controls.SetInputBlocked(false); _clock.Paused = true;
        Assert.That(_skill.TryTravel(), Is.False); _clock.Paused = false;
        var presence = _actor.AddComponent<PlayerMatchPresence>(); presence.BindReplica(_clock, _lifetime.Token);
        Assert.That(_skill.TryTravel(), Is.False); Assert.That(_skill.TryRecoverNearest(), Is.False);
        presence.UnbindReplica(_clock); _actor.GetComponent<PlayerInfo>().curHp = 0;
        Assert.That(_skill.TryTravel(), Is.False); Assert.That(_skill.TryUseSkill(), Is.False);
        yield break;
    }

    private CheckpointCaptureContext Context() => new(new MatchConfig(new MatchId("portal-test"), MatchMode.Story,
        new StageId("stage"), new DefinitionId("normal"), "test", 1,
        new[] { new PlayerLoadoutSnapshot(new PlayerId("portal-player"), null) }), _clock.Now, Guid.NewGuid());
    private CheckpointRestoreContext Restore(CheckpointSection section)
    {
        var current = Context();
        return new CheckpointRestoreContext(current, new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow,
            current.Config.MatchId, current.Config.StageId, current.Config.DifficultyProfileId, current.Config.Mode,
            current.Config.RandomSeed, current.Config.ContentVersion, current.GameSeconds, 1, new[] { section }), CheckpointRestorePolicy.Default);
    }

    [UnityTest]
    public IEnumerator CheckpointRestoresPairRemainingTravelDelayAndRollbackWithoutTeleportingPlayer()
    {
        Pair(); StandAt(At(4)); Assert.That(_skill.TryTravel(), Is.True);
        var codec = new BinaryCheckpointPayloadCodec(); var writer = (ICheckpointParticipant)_skill;
        var saved = writer.Capture(Context(), codec);
        _clock.Now = 2; Assert.That(_skill.TryRecoverNearest(), Is.True);
        Vector3 before = _actor.transform.position;
        using (var step = writer.PrepareRestore(Restore(saved), saved, codec))
        {
            step.Apply(); Assert.That(_skill.IsLinked, Is.True); Assert.That(_skill.TravelRemaining, Is.EqualTo(.75f).Within(.001));
            Assert.That(_actor.transform.position, Is.EqualTo(before));
            step.Rollback(); Assert.That(_skill.IsLinked, Is.False); Assert.That(_skill.GetEndpoint(0), Is.Not.Null);
        }
        var corrupt = codec.Write(saved.Key, 1, CheckpointSectionKind.World, w =>
        { codec.WriteText(w, "portal-player"); w.Write(false); w.Write(float.NaN); w.Write(false); w.Write(false); });
        Assert.Throws<InvalidDataException>(() => writer.PrepareRestore(Restore(corrupt), corrupt, codec));
        yield break;
    }

    private sealed class Clock : IMatchTime
    {
        public double Now; public bool Paused;
        private readonly Guid _epoch = Guid.NewGuid();
        public bool IsAvailable => true;
        public double ElapsedSeconds => Now;
        public MatchTimeSnapshot Snapshot => new(new MatchId("portal-test"), _epoch, 0, 0, 0, Paused ? 0 : 1, Paused, true);
    }
}
