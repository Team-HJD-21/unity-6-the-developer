// 실제 Initial_Stage의 D 처리기를 정식 요청으로 호출해 결과·중복·종료 수명을 대조합니다.
// 검증용 Host 정책은 제품의 공용 참가자 권한을 확정하는 기본값이 아닙니다.
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
using TeamHJD.Game.Turrets;
using TeamHJD.Game.Turrets.Contracts;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Object = UnityEngine.Object;

public sealed class TurretCommandIntegrationTests
{
    private sealed class ValidationHostPolicy : ITurretCommandAuthorization
    {
        private readonly PlayerId _host;
        public bool Allow = true;
        public ValidationHostPolicy(PlayerId host) { _host = host; }
        public TurretCommandPermission Authorize(TurretCommandRequest request, PlayerId issuer)
            => Allow && issuer == _host ? TurretCommandPermission.Allowed : TurretCommandPermission.Denied("ValidationDenied");
    }
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
        NetworkSceneSetup.InstallOnActiveScene();
        yield return new EnterPlayMode(); yield return null;
        var app = Object.FindAnyObjectByType<AppRoot>();
        var binding = Object.FindAnyObjectByType<TurretMatchBinding>();
        var controller = Object.FindAnyObjectByType<TurretController>();
        Assert.That(controller.HasWriteAuthority, Is.True);
        Assert.That(app.NetworkMatches.TryResolveParticipant(app.NetworkSession.LocalConnectionId, out PlayerId issuer), Is.True);
        VerifyActualAuthorityActor(app.CurrentMatchId, issuer);
        var policy = BindValidationCommands(binding, app, issuer);
        ITurretCommandService commands = binding.Commands;
        var levels = AssetDatabase.LoadAssetAtPath<TurretLevelUpgradeCatalog>("Assets/Data/Turrets/TurretLevelUpgrade_Stage1.asset");
        TurretBase turret = FindLevelCandidate(controller, levels);
        Assert.That(turret, Is.Not.Null);
        Assert.That(binding.TryResolveEntityId(turret.InstanceId, out EntityId target), Is.True);
        MatchId matchId = app.CurrentMatchId;
        policy.Allow = false;
        TurretCommandRequest denied = Request(matchId, issuer, target, TurretCommandKind.UpgradeLevel);
        Assert.That(commands.Submit(denied, issuer).FeatureCode, Is.EqualTo("ValidationDenied"));
        policy.Allow = true;
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
        TurretUpgradeDefinition upgrade = FindCompatibleUpgrade(turret);
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
        Assert.That(commands.Submit(Request(matchId, issuer, new EntityId("turret/missing"), TurretCommandKind.SetActivation), issuer).Status,
            Is.EqualTo(TurretCommandStatus.TargetUnavailable));
        var automatic = Request(matchId, issuer, default, TurretCommandKind.StartAutoActivation);
        var accepted = commands.Submit(automatic, issuer);
        Assert.That(accepted.Status, Is.EqualTo(TurretCommandStatus.Accepted));
        Assert.That(commands.Submit(automatic, issuer).IsReplay, Is.True);
        Assert.That(commands.Submit(Request(matchId, issuer, default, TurretCommandKind.StartAutoActivation), issuer).Status,
            Is.EqualTo(TurretCommandStatus.FeatureRejected));
        app.EndCurrentMatch();
        Assert.That(binding.Commands, Is.Null);
        Assert.That(controller.IsAutoActivationRunning, Is.False);
        Assert.That(controller.Budget.ReservedPower, Is.Zero);
        Assert.That(controller.HasWriteAuthority, Is.False);
        Assert.That(controller.TryStartAutoActivation(), Is.False);
        Assert.That(turret.RequestActivation(true), Is.EqualTo(TurretActivationResult.NotAuthoritative));
        Assert.That(turret.ApplyDamage(1), Is.False);
        Assert.That(commands.Submit(turnOn, issuer).Status, Is.EqualTo(TurretCommandStatus.MatchUnavailable));
        yield return null;
        var network = Object.FindAnyObjectByType<InitialStageNetworkBinding>();
        Assert.That(network.TryPrepareMatch(), Is.True); Assert.That(network.TryStartMatch(), Is.True);
        Assert.That(controller.HasWriteAuthority, Is.True);
        BindValidationCommands(binding, app, issuer);
        Assert.That(binding.Commands.Submit(turnOn, issuer).Status, Is.EqualTo(TurretCommandStatus.DifferentMatch));
        app.EndCurrentMatch();
        yield return new ExitPlayMode();
    }
    private static ValidationHostPolicy BindValidationCommands(TurretMatchBinding binding, AppRoot app, PlayerId issuer)
    {
        // 재로드 이전 iterator에 캡처를 만들지 않도록 일반 함수에서만 delegate를 생성합니다.
        var policy = new ValidationHostPolicy(issuer);
        binding.BindCommands(() => app.NetworkSession.IsServer,
            () => !binding.BoundMatchId.IsEmpty && app.CurrentMatchId == binding.BoundMatchId,
            id => id == issuer, policy);
        return policy;
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
    private static TurretBase FindLevelCandidate(TurretController controller, TurretLevelUpgradeCatalog levels)
    {
        foreach (TurretBase candidate in TurretInstanceRegistry.GetAll())
            if (controller.OwnsTurret(candidate) && !candidate.IsLocked && !candidate.IsActivated && levels.TryGetNext(candidate.Definition, out _)) return candidate;
        return null;
    }
    private static TurretBase Resolve(TurretMatchBinding binding, EntityId id)
    {
        Assert.That(binding.TryResolveLocalId(id, out int localId), Is.True);
        Assert.That(TurretInstanceRegistry.TryGet(localId, out var turret), Is.True); return turret;
    }
    private static TurretUpgradeDefinition FindCompatibleUpgrade(TurretBase turret)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:TurretUpgradeDefinition", new[] { "Assets/Data/Turrets" }))
        {
            var upgrade = AssetDatabase.LoadAssetAtPath<TurretUpgradeDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (upgrade.IsCompatibleWith(turret.Definition.Id)) return upgrade;
        }
        return null;
    }
}
