// 실제 Initial_Stage의 D 처리기를 정식 요청으로 호출해 결과·중복·종료 수명을 대조합니다.
// 실제 Scene 조립의 Host 거리 정책·actor 제공자·명령 gateway 수명도 확인합니다.
using System;
using System.Collections;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Content;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Application;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
using TeamHJD.Game.FieldTurrets;
using TeamHJD.Game.FieldTurrets.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Object = UnityEngine.Object;

public sealed class TurretCommandIntegrationTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var app = Object.FindAnyObjectByType<AppRoot>(); if (app != null) app.EndCurrentMatch();
        if (UnityEngine.Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator ActualDCommandsAreScopedIdempotentAndStoppedOnMatchEnd()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        Assert.That(Object.FindAnyObjectByType<InitialStageNetworkBinding>(), Is.Not.Null,
            "제작된 Scene의 네트워크 연결을 검증하며 테스트가 원본 Scene을 다시 저장하지 않습니다.");
        yield return new EnterPlayMode(); yield return null;
        var app = Object.FindAnyObjectByType<AppRoot>();
        var binding = Object.FindAnyObjectByType<TurretMatchBinding>();
        var controller = Object.FindAnyObjectByType<FieldTurretController>();
        Assert.That(controller.HasWriteAuthority, Is.True);
        Assert.That(app.NetworkMatches.TryResolveParticipant(app.NetworkSession.LocalConnectionId, out PlayerId issuer), Is.True);
        VerifyActualAuthorityActor(app.CurrentMatchId, issuer);
        ITurretCommandService commands = binding.Commands;
        Assert.That(commands, Is.Not.Null);
        var networkBinding = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        Assert.That(networkBinding.TurretCommands, Is.Not.Null);
        var levels = AssetDatabase.LoadAssetAtPath<FieldTurretLevelUpgradeCatalog>("Assets/Data/FieldTurrets/FieldTurretLevelUpgrade_Stage1.asset");
        FieldTurretBase turret = FindLevelCandidate(controller, levels);
        Assert.That(turret, Is.Not.Null);
        Assert.That(binding.TryResolveEntityId(turret.InstanceId, out EntityId target), Is.True);
        MatchId matchId = app.CurrentMatchId;
        var actorTransform = Object.FindAnyObjectByType<PlayerActorIdentity>().transform;
        actorTransform.position = turret.transform.position + new Vector3(100, 100, 0);
        TurretCommandRequest denied = Request(matchId, issuer, target, TurretCommandKind.UpgradeLevel);
        Assert.That(commands.Submit(denied, issuer).FeatureCode, Is.EqualTo("OutOfInteractionRange"));
        actorTransform.position = turret.transform.position;
        Assert.That(commands.Submit(denied, issuer).IsReplay, Is.True);

        int initialLevel = turret.Definition.Level;
        var levelUp = Request(matchId, issuer, target, TurretCommandKind.UpgradeLevel);
        Assert.That(commands.Submit(levelUp, issuer).Status, Is.EqualTo(TurretCommandStatus.Applied));
        turret = Resolve(binding, target);
        Assert.That(turret.Definition.Level, Is.EqualTo(initialLevel + 1));
        Assert.That(commands.Submit(levelUp, issuer).IsReplay, Is.True);
        Assert.That(Resolve(binding, target).Definition.Level, Is.EqualTo(initialLevel + 1));
        Assert.That(commands.Submit(Request(matchId, issuer, target, TurretCommandKind.DowngradeLevel), issuer).Status,
            Is.EqualTo(TurretCommandStatus.Applied));
        turret = Resolve(binding, target);
        Assert.That(turret.Definition.Level, Is.EqualTo(initialLevel));
        FieldTurretUpgradeDefinition upgrade = FindCompatibleUpgrade(turret);
        Assert.That(upgrade, Is.Not.Null);
        var specUp = Request(matchId, issuer, target, TurretCommandKind.ApplyUpgrade, upgrade: new DefinitionId(upgrade.Id));
        Assert.That(commands.Submit(specUp, issuer).Status, Is.EqualTo(TurretCommandStatus.Applied));
        Assert.That(turret.RuntimeState.UpgradeLevel, Is.EqualTo(1));
        Assert.That(commands.Submit(specUp, issuer).IsReplay, Is.True);
        Assert.That(turret.RuntimeState.UpgradeLevel, Is.EqualTo(1));
        Assert.That(commands.Submit(Request(matchId, issuer, target, TurretCommandKind.DowngradeUpgrade,
            upgrade: new DefinitionId(upgrade.Id)), issuer).Status, Is.EqualTo(TurretCommandStatus.Applied));
        Assert.That(turret.RuntimeState.UpgradeLevel, Is.Zero);
        Assert.That(Object.FindAnyObjectByType<RegionSceneBinding>().ResetPolicy(RegionPolicyMode.Dynamic, true), Is.True);
        int reservedBefore = controller.Budget.ReservedPower;
        var turnOn = Request(matchId, issuer, target, TurretCommandKind.SetActivation, true);
        Assert.That(commands.Submit(turnOn, issuer).FeatureCode, Is.EqualTo("Activated"));
        Assert.That(turret.IsActivated, Is.True);
        int reserved = controller.Budget.ReservedPower;
        Assert.That(reserved - reservedBefore, Is.EqualTo(turret.EffectivePower));
        Assert.That(commands.Submit(turnOn, issuer).IsReplay, Is.True);
        Assert.That(controller.Budget.ReservedPower, Is.EqualTo(reserved));
        Assert.That(commands.Submit(Request(matchId, issuer, target, TurretCommandKind.SetActivation), issuer).FeatureCode, Is.EqualTo("Deactivated"));
        Assert.That(turret.IsActivated, Is.False);
        var missingTarget = commands.Submit(Request(matchId, issuer, new EntityId("turret/missing"), TurretCommandKind.SetActivation), issuer);
        Assert.That(missingTarget.Status, Is.EqualTo(TurretCommandStatus.Unauthorized));
        Assert.That(missingTarget.FeatureCode, Is.EqualTo("InteractionTargetUnavailable"));
        var automatic = Request(matchId, issuer, default, TurretCommandKind.StartAutoActivation);
        actorTransform.position = Object.FindAnyObjectByType<ControlUnitHealth>().transform.position;
        var accepted = commands.Submit(automatic, issuer);
        Assert.That(accepted.Status, Is.EqualTo(TurretCommandStatus.Accepted));
        Assert.That(commands.Submit(automatic, issuer).IsReplay, Is.True);
        Assert.That(commands.Submit(Request(matchId, issuer, default, TurretCommandKind.StartAutoActivation), issuer).Status,
            Is.EqualTo(TurretCommandStatus.FeatureRejected));
        app.EndCurrentMatch();
        Assert.That(binding.Commands, Is.Null);
        Assert.That(networkBinding.TurretCommands, Is.Null);
        Assert.That(controller.IsAutoActivationRunning, Is.False);
        Assert.That(controller.Budget.ReservedPower, Is.Zero);
        Assert.That(controller.HasWriteAuthority, Is.False);
        int healthAfterEnd = turret.CurrentHealth;
        Assert.That(turret.Repair(10f, .1f), Is.EqualTo(FieldTurretRepairResult.Unauthorized));
        Assert.That(turret.Repair(10), Is.False);
        Assert.That(turret.CurrentHealth, Is.EqualTo(healthAfterEnd));
        Assert.That(controller.TryStartAutoActivation(), Is.False);
        Assert.That(turret.RequestActivation(true), Is.EqualTo(FieldTurretActivationResult.NotAuthoritative));
        Assert.That(turret.ApplyDamage(1), Is.False);
        Assert.That(commands.Submit(turnOn, issuer).Status, Is.EqualTo(TurretCommandStatus.MatchUnavailable));
        yield return null;
        var network = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        Assert.That(network.TryPrepareMatch(), Is.True); Assert.That(network.TryStartMatch(), Is.True);
        Assert.That(controller.HasWriteAuthority, Is.True);
        Assert.That(binding.Commands, Is.Not.Null);
        Assert.That(binding.Commands.Submit(turnOn, issuer).Status, Is.EqualTo(TurretCommandStatus.DifferentMatch));
        app.EndCurrentMatch();
        yield return new ExitPlayMode();
    }
    private static void VerifyActualAuthorityActor(MatchId matchId, PlayerId playerId)
    {
        var identity = Object.FindAnyObjectByType<PlayerActorIdentity>();
        Assert.That(identity.PlayerId, Is.EqualTo(playerId));
        var controls = identity.GetComponent<PlayerControlState>(); bool authority = true;
        using var registry = new PlayerInteractionRegistry(matchId, new[] { playerId });
        using var actor = new PlayerInteractionActorBinding(registry, playerId, identity.gameObject, () => authority);
        Assert.That(registry.TryCapture(matchId, playerId, out var state), Is.True);
        Assert.That(state.IsAlive, Is.True); Assert.That(state.CanInteract, Is.True);
        Assert.That(state.Position.X, Is.EqualTo((double)identity.transform.position.x));
        controls.SetInputBlocked(true);
        Assert.That(registry.TryCapture(matchId, playerId, out state), Is.True);
        Assert.That(state.CanInteract, Is.False); controls.SetInputBlocked(false);
        authority = false; Assert.That(registry.TryCapture(matchId, playerId, out _), Is.False);
        actor.Dispose(); Assert.That(registry.TryCapture(matchId, playerId, out _), Is.False);
    }
    private static TurretCommandRequest Request(MatchId matchId, PlayerId issuer, EntityId target, TurretCommandKind kind,
        bool on = false, DefinitionId upgrade = default)
        => new TurretCommandRequest(new MatchRequestContext(matchId, Guid.NewGuid(), issuer), kind, target, on, upgrade);
    private static FieldTurretBase FindLevelCandidate(FieldTurretController controller, FieldTurretLevelUpgradeCatalog levels)
    {
        foreach (FieldTurretBase candidate in FieldTurretInstanceRegistry.GetAll())
            if (controller.OwnsTurret(candidate) && !candidate.IsLocked && !candidate.IsActivated && levels.TryGetNext(candidate.Definition, out _)) return candidate;
        return null;
    }
    private static FieldTurretBase Resolve(TurretMatchBinding binding, EntityId id)
    {
        Assert.That(binding.TryResolveLocalId(id, out int localId), Is.True);
        Assert.That(FieldTurretInstanceRegistry.TryGet(localId, out var turret), Is.True); return turret;
    }
    private static FieldTurretUpgradeDefinition FindCompatibleUpgrade(FieldTurretBase turret)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:FieldTurretUpgradeDefinition", new[] { "Assets/Data/FieldTurrets" }))
        {
            var upgrade = AssetDatabase.LoadAssetAtPath<FieldTurretUpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (upgrade.IsCompatibleWith(turret.Definition.Id)) return upgrade;
        }
        return null;
    }
}
