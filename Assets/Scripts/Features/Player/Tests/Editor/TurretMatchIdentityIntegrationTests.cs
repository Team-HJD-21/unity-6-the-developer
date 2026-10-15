// 실제 Initial_Stage/D Factory·승급·강등을 거쳐 Core 논리 ID와 배치 입력을 대조합니다.
// 네트워크 요청 권한·전투 전체 복제를 대신하지 않습니다.
using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.Bootstrap.SceneComposition;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Editor;
using TeamHJD.Game.Presentation.Scene;
using TeamHJD.Game.Turrets;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using EntityId = TeamHJD.Game.Domain.EntityId;
using Object = UnityEngine.Object;

public sealed class TurretMatchIdentityIntegrationTests
{
    private sealed class Counter { public int Value; public void OnChanged() => Value++; }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        var root = Object.FindAnyObjectByType<AppRoot>(); if (root != null) root.EndCurrentMatch();
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator ActualLevelReplacementAndFactoryPreserveExplicitMatchIdentity()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        NetworkSceneSetup.InstallOnActiveScene();
        yield return new EnterPlayMode(); yield return null;
        var app = Object.FindAnyObjectByType<AppRoot>();
        var binding = Object.FindAnyObjectByType<TurretMatchBinding>();
        var controller = Object.FindAnyObjectByType<TurretController>();
        var source = Object.FindAnyObjectByType<TurretBattlefieldInputSource>();
        Assert.That(binding.BoundMatchId, Is.EqualTo(app.CurrentMatchId));
        Assert.That(binding.Count, Is.GreaterThan(2));
        Assert.That(source.CaptureTurretLayout().Turrets.All(item => item.EntityId.Value.StartsWith("turret/")), Is.True);
        var catalog = AssetDatabase.FindAssets("t:TurretLevelUpgradeCatalog")
            .Select(guid => AssetDatabase.LoadAssetAtPath<TurretLevelUpgradeCatalog>(AssetDatabase.GUIDToAssetPath(guid))).First();
        TurretBase original = null, nextPrefab = null;
        foreach (TurretBase candidate in TurretInstanceRegistry.GetAll())
            if (controller.OwnsTurret(candidate) && !candidate.IsActivated && catalog.TryGetNext(candidate.Definition, out nextPrefab))
            { original = candidate; break; }
        Assert.That(original, Is.Not.Null);
        Assert.That(binding.TryResolveEntityId(original.InstanceId, out EntityId logicalId), Is.True);
        int initialCount = binding.Count;
        var counter = new Counter(); source.LayoutChanged += counter.OnChanged;
        Assert.That(original.RequestLevelUpgrade(nextPrefab, out TurretBase upgraded), Is.EqualTo(TurretLevelUpgradeResult.Upgraded));
        Assert.That(binding.TryResolveLocalId(logicalId, out int localId), Is.True);
        Assert.That(localId, Is.EqualTo(upgraded.InstanceId));
        Assert.That(upgraded.GetComponent<SceneEntityIdentity>().EntityId, Is.EqualTo(logicalId));
        Assert.That(binding.Count, Is.EqualTo(initialCount));
        yield return null;
        Assert.That(counter.Value, Is.Zero, "A level replacement must not publish an empty or differently identified spatial layout.");
        source.LayoutChanged -= counter.OnChanged;
        Assert.That(catalog.TryGetPrevious(upgraded.Definition, out TurretBase previousPrefab), Is.True);
        Assert.That(upgraded.RequestLevelDowngrade(previousPrefab, out TurretBase downgraded), Is.EqualTo(TurretLevelUpgradeResult.Downgraded));
        Assert.That(downgraded.GetComponent<SceneEntityIdentity>().EntityId, Is.EqualTo(logicalId));
        Assert.That(binding.TryResolveEntityId(downgraded.InstanceId, out EntityId afterDowngrade), Is.True);
        Assert.That(afterDowngrade, Is.EqualTo(logicalId));
        downgraded.gameObject.SetActive(false);
        Assert.That(binding.TryResolveLocalId(logicalId, out _), Is.False);
        downgraded.gameObject.SetActive(true);
        Assert.That(binding.TryResolveLocalId(logicalId, out _), Is.True);

        TurretBase created = TurretFactory.Create(previousPrefab,
            TurretInitializationData.CreateDefault(previousPrefab.Definition, false, false), controller,
            Vector3.zero, Quaternion.identity);
        Assert.That(binding.TryResolveEntityId(created.InstanceId, out _), Is.False);
        var runtimeId = new EntityId("turret/runtime-test");
        Assert.That(binding.TryRegisterEntity(new MatchId("wrong"), runtimeId, created.InstanceId, out _), Is.False);
        Assert.That(binding.TryRegisterEntity(app.CurrentMatchId, runtimeId, created.InstanceId, out string reason), Is.True, reason);
        Assert.That(binding.TryResolveLocalId(runtimeId, out localId), Is.True);
        Assert.That(ContainsEntity(source.CaptureTurretLayout(), runtimeId), Is.True);
        Object.Destroy(created.gameObject); yield return null;
        Assert.That(binding.TryResolveLocalId(runtimeId, out _), Is.False);
        binding.UnbindMatch(new MatchId("wrong")); Assert.That(binding.Count, Is.EqualTo(initialCount));
        app.EndCurrentMatch();
        Assert.That(binding.Count, Is.Zero); Assert.That(binding.BoundMatchId.IsEmpty, Is.True);
        Assert.That(binding.TryRegisterEntity(app.CurrentMatchId, runtimeId, localId, out _), Is.False);
        yield return new ExitPlayMode();
    }
    private static bool ContainsEntity(BattlefieldSpatialInput input, EntityId entityId)
    {
        // EnterPlayMode를 넘는 iterator에서 지역 변수를 람다로 캡처하지 않습니다.
        foreach (TurretSpatialInput turret in input.Turrets) if (turret.EntityId == entityId) return true;
        return false;
    }
}
