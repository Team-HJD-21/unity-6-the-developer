// 실제 무기/탄환의 Scene 귀속·저장 복원과 렌치의 남은 impact 이후 단일 피해를 확인합니다.
// 빈 Scene의 실제 Player prefab과 EnemyHealth를 사용하며 원래 Editor Scene 구성을 복원합니다.
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerWeaponCheckpointPlayTests
{
    private sealed class Timestamp : ITimestampSource
    { public double Now; public bool TryReadSeconds(out double seconds) { seconds = Now; return true; } }
    private SceneSetup[] _setup;
    private Timestamp _source;
    private MatchClock _clock;
    private MatchPresenceService _presence;
    private MatchConfig _config;
    private GameObject _actor;
    private readonly BinaryCheckpointPayloadCodec _codec = new();
    [SetUp] public void RememberScenes()
    { if (!UnityEngine.Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        _presence?.Dispose(); _clock?.Dispose();
        if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        if (_setup != null && _setup.Any(s => s.isLoaded) && _setup.Count(s => s.isActive) == 1)
            EditorSceneManager.RestoreSceneManagerSetup(_setup);
    }

    private void CreatePlayer()
    {
        var player = new PlayerId("weapon-owner"); var match = new MatchId("weapon-checkpoint");
        _config = new MatchConfig(match, MatchMode.Story, new StageId("stage"), new DefinitionId("normal"), "content", 1,
            new[] { new PlayerLoadoutSnapshot(player, null) });
        _source = new Timestamp(); _clock = new MatchClock(match, _source);
        _presence = new MatchPresenceService(_config, () => true, _clock.SetPaused);
        _actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"), new Vector3(12000, 12000, 0), Quaternion.identity);
        var identity = _actor.GetComponent<PlayerActorIdentity>() ?? _actor.AddComponent<PlayerActorIdentity>(); identity.Bind(player, 0);
        var input = _actor.GetComponent<PlayerInputHandler>(); input.enabled = false;
        typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.AimDirection)).SetValue(input, Vector2.down);
        var presence = _actor.GetComponent<PlayerMatchPresence>() ?? _actor.AddComponent<PlayerMatchPresence>();
        presence.Bind(_presence.RegisterActor(player), _clock, default);
    }
    private void Advance(double seconds) { _source.Now += seconds; _clock.Pump(); }
    private CheckpointCaptureContext Context => new(_config, _clock.ElapsedSeconds, _clock.Snapshot.Epoch);
    private CheckpointRestoreContext RestoreContext(CheckpointSection section, double savedSeconds)
    {
        var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, _config.MatchId, _config.StageId, _config.DifficultyProfileId,
            _config.Mode, _config.RandomSeed, _config.ContentVersion, savedSeconds, 1, new[] { section });
        return new CheckpointRestoreContext(Context, data, CheckpointRestorePolicy.Default);
    }
    private PlayerBullet[] Bullets() => Object.FindObjectsByType<PlayerBullet>(FindObjectsSortMode.None)
        .Where(b => b.gameObject.scene == _actor.scene).ToArray();

    [UnityTest]
    public IEnumerator OwnedBulletsRestoreFlightAndCooldownWhileHubIsActiveAndRollbackRemovesOnlyOwnedShots()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode(); CreatePlayer(); yield return null;
        var attack = _actor.GetComponent<PlayerAttack>(); var writer = (ICheckpointParticipant)attack;
        attack.SelectNextWeapon();
        Scene hub = SceneManager.CreateScene("Checkpoint Test Hub"); SceneManager.SetActiveScene(hub);
        Assert.That(attack.TryFire(), Is.True);
        PlayerBullet[] original = Bullets(); Assert.That(original.Length, Is.EqualTo(5));
        Assert.That(Object.FindObjectsByType<PlayerBullet>(FindObjectsSortMode.None).All(b => b.gameObject.scene == _actor.scene), Is.True);
        Assert.That(original.All(b => ReferenceEquals(b.GetComponent<FieldSimulationActor>().TimeSource, _clock)), Is.True);
        Advance(0.1); _clock.SetPaused(true);
        var section = writer.Capture(Context, _codec);
        Vector2[] positions = original.Select(b => b.GetComponent<Rigidbody2D>().position).OrderBy(p => p.x).ToArray();
        Assert.That(original.All(b => !b.GetComponent<Rigidbody2D>().simulated), Is.True);
        _clock.SetPaused(false); Advance(3); yield return null; yield return null;
        Assert.That(Bullets(), Is.Empty);
        attack.SelectNextWeapon(); Assert.That(attack.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
        using var prepared = writer.PrepareRestore(RestoreContext(section, 0.1), section, _codec);
        _clock.Restore(0.1); _clock.SetPaused(true); prepared.Apply();
        var restored = Bullets(); Assert.That(restored.Length, Is.EqualTo(5));
        Assert.That(attack.CurrentWeapon, Is.EqualTo(WeaponType.CryoBlaster));
        Assert.That(restored.Select(b => b.GetComponent<Rigidbody2D>().position).OrderBy(p => p.x), Is.EqualTo(positions));
        Assert.That(restored.All(b => !b.GetComponent<Rigidbody2D>().simulated), Is.True);
        _clock.SetPaused(false); Assert.That(attack.TryFire(), Is.False, "The captured shot cooldown must remain.");
        Advance(0.16); Assert.That(attack.TryFire(), Is.True);
        Assert.That(Bullets().Length, Is.EqualTo(10));
        prepared.Rollback();
        Assert.That(attack.CurrentWeapon, Is.EqualTo(WeaponType.Spanner));
        Assert.That(Bullets(), Is.Empty);
    }

    [UnityTest]
    public IEnumerator PendingWrenchStrikeWaitsForRemainingTimeThenDealsDamageExactlyOnce()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode(); CreatePlayer(); yield return null;
        var attack = _actor.GetComponent<PlayerAttack>(); var writer = (ICheckpointParticipant)attack;
        attack.SelectNextWeapon(); attack.SelectNextWeapon();
        var target = new GameObject("Wrench checkpoint target"); target.transform.position = _actor.transform.position + Vector3.down;
        var collider = target.AddComponent<CircleCollider2D>(); collider.radius = 0.25f; collider.isTrigger = true;
        var health = target.AddComponent<EnemyHealth>();
        var targetData = new SerializedObject(health); targetData.FindProperty("maxHealth").intValue = 100; targetData.ApplyModifiedPropertiesWithoutUndo(); health.Initialize();
        var spanner = _actor.GetComponentInChildren<PlayerSpannerWeapon>(true);
        var weaponData = new SerializedObject(spanner); weaponData.FindProperty("_damage").intValue = 20; weaponData.ApplyModifiedPropertiesWithoutUndo();
        Physics2D.SyncTransforms(); Assert.That(attack.TryFire(), Is.True);
        Advance(0.05); _clock.SetPaused(true); var section = writer.Capture(Context, _codec);
        attack.SelectNextWeapon(); _clock.SetPaused(false); Advance(1); yield return null;
        Assert.That(health.CurrentHealth, Is.EqualTo(100), "Switching weapons cancelled the old strike.");
        using var prepared = writer.PrepareRestore(RestoreContext(section, 0.05), section, _codec);
        _clock.Restore(0.05); _clock.SetPaused(true); prepared.Apply(); yield return null;
        Assert.That(health.CurrentHealth, Is.EqualTo(100));
        _clock.SetPaused(false); Advance(0.1); yield return null; Assert.That(health.CurrentHealth, Is.EqualTo(100));
        Advance(0.11); yield return null; Assert.That(health.CurrentHealth, Is.EqualTo(80));
        Advance(1); yield return null; Assert.That(health.CurrentHealth, Is.EqualTo(80));
    }
}
