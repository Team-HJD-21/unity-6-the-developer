// 실제 PlayMode에서 필드 정지 중 우주선 Player 이동과 사망/UI 잠금의 분리를 확인합니다.
// 입력 수집기만 고정 입력으로 대체하며 실제 Player 이동·스킬·권위 위치 경계를 사용합니다.
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerHubMovementTests
{
    private sealed class Timestamp : ITimestampSource
    { public bool TryReadSeconds(out double seconds) { seconds = Time.unscaledTimeAsDouble; return true; } }
    private SceneSetup[] _setup;
    private MatchClock _clock;
    private MatchPresenceService _presence;
    [SetUp] public void RememberScene()
    { if (!UnityEngine.Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
    [UnityTearDown] public IEnumerator Cleanup()
    {
        _presence?.Dispose(); _clock?.Dispose();
        if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        if (_setup != null && _setup.Any(s => s.isLoaded) && _setup.Count(s => s.isActive) == 1)
            EditorSceneManager.RestoreSceneManagerSetup(_setup);
    }

    [UnityTest]
    public IEnumerator HubNavigationContinuesWhileFieldAttackIsPausedAndOtherLocksStillWork()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var match = new MatchId("hub-navigation"); var playerId = new PlayerId("host");
        var config = new MatchConfig(match, MatchMode.Story, new StageId("stage"), new DefinitionId("normal"), "content", 1,
            new[] { new PlayerLoadoutSnapshot(playerId, null) });
        _clock = new MatchClock(match, new Timestamp());
        _presence = new MatchPresenceService(config, () => true, _clock.SetPaused);
        var actor = new GameObject("Hub player"); actor.SetActive(false);
        var skills = actor.AddComponent<PlayerSkillController>();
        var movement = actor.AddComponent<PlayerMovement>();
        var health = actor.AddComponent<PlayerInfo>();
        var body = actor.GetComponent<Rigidbody2D>(); body.gravityScale = 0;
        actor.SetActive(true); yield return null;
        var input = actor.GetComponent<PlayerInputHandler>(); input.enabled = false;
        typeof(PlayerInputHandler).GetProperty(nameof(PlayerInputHandler.MoveInput)).SetValue(input, Vector2.right);
        var controls = actor.GetComponent<PlayerControlState>();
        var presence = actor.AddComponent<PlayerMatchPresence>();
        presence.Bind(_presence.RegisterActor(playerId), _clock, default);
        presence.Report(MatchPlayerLocation.Spaceship);
        Assert.That(_clock.Snapshot.IsPaused, Is.True);
        Assert.That(skills.CanUse, Is.False, "Field attacks must stay disabled inside the hub.");
        float before = body.position.x;
        yield return WaitForPhysics();
        Assert.That(body.position.x, Is.GreaterThan(before), "Hub movement must not use the field attack gate.");
        controls.SetInputBlocked(true); yield return WaitForPhysics();
        Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        controls.SetInputBlocked(false); yield return WaitForPhysics();
        Assert.That(body.linearVelocity.x, Is.GreaterThan(0));
        health.Die(); yield return WaitForPhysics();
        Assert.That(controls.CanAct, Is.False); Assert.That(body.linearVelocity, Is.EqualTo(Vector2.zero));
        movement.CanMove = false;
    }

    private static IEnumerator WaitForPhysics()
    {
        double target = Time.fixedTimeAsDouble + 0.04, timeout = Time.realtimeSinceStartupAsDouble + 3;
        while (Time.fixedTimeAsDouble < target && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
        Assert.That(Time.fixedTimeAsDouble, Is.GreaterThanOrEqualTo(target));
    }
}
