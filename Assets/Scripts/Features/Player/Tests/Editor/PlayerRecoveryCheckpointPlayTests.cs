// PlayMode의 실제 회복 진행을 저장·복원한 뒤 남은 시간부터 이어가는지 확인합니다.
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class PlayerRecoveryCheckpointPlayTests
{
    private SceneSetup[] _setup;
    [SetUp] public void RememberScenes()
    { if (!UnityEngine.Application.isPlaying) _setup = EditorSceneManager.GetSceneManagerSetup(); }
    [UnityTearDown] public IEnumerator RestoreScenes()
    {
        if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
        if (_setup != null && _setup.Any(s => s.isLoaded) && _setup.Count(s => s.isActive) == 1)
            EditorSceneManager.RestoreSceneManagerSetup(_setup);
    }
    [UnityTest]
    public IEnumerator RecoveryResumesFromTheCapturedVitalsAndKeepsAnIndependentUiLock()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        var player = new PlayerId("recovering-player");
        var config = new MatchConfig(new MatchId("recovering"), MatchMode.Story, new StageId("stage"),
            new DefinitionId("normal"), "content", 1, new[] { new PlayerLoadoutSnapshot(player, null) });
        var actor = new GameObject("Recovering Player"); actor.SetActive(false);
        actor.AddComponent<PlayerActorIdentity>().Bind(player, 0);
        var controls = actor.AddComponent<PlayerControlState>();
        var health = actor.AddComponent<PlayerInfo>(); actor.SetActive(true);
        health.TakeDamage(20); health.RecoverHp();
        var writer = (ICheckpointParticipant)health; var codec = new BinaryCheckpointPayloadCodec();
        var context = new CheckpointCaptureContext(config, 5, Guid.NewGuid());
        var section = writer.Capture(context, codec); int savedHealth = health.curHp;
        double until = Time.timeAsDouble + 0.07, timeout = Time.realtimeSinceStartupAsDouble + 3;
        while (Time.timeAsDouble < until && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
        Assert.That(health.curHp, Is.GreaterThan(savedHealth));
        var data = new CheckpointData(Guid.NewGuid(), DateTimeOffset.UtcNow, config.MatchId, config.StageId, config.DifficultyProfileId,
            config.Mode, config.RandomSeed, config.ContentVersion, 5, 1, new[] { section });
        controls.SetInputBlocked(true);
        using var prepared = writer.PrepareRestore(new CheckpointRestoreContext(context, data, CheckpointRestorePolicy.Default), section, codec);
        prepared.Apply();
        Assert.That(health.curHp, Is.EqualTo(savedHealth)); Assert.That(health.IsRecovering, Is.True);
        until = Time.timeAsDouble + 0.07; timeout = Time.realtimeSinceStartupAsDouble + 3;
        while (Time.timeAsDouble < until && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
        Assert.That(health.curHp, Is.GreaterThan(savedHealth)); Assert.That(controls.CanAct, Is.False);
        health.TakeDamage(400); int deadHealth = health.curHp;
        until = Time.timeAsDouble + 0.04; timeout = Time.realtimeSinceStartupAsDouble + 3;
        while (Time.timeAsDouble < until && Time.realtimeSinceStartupAsDouble < timeout) yield return null;
        Assert.That(health.curHp, Is.EqualTo(deadHealth)); Assert.That(health.IsDead, Is.True);
    }
}
