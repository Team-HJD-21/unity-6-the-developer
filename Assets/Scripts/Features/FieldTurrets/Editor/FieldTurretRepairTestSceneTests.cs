using System.Collections;
using System.Linq;
using NUnit.Framework;
using TeamHJD.Game.Bootstrap;
using TeamHJD.Game.FieldTurrets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class FieldTurretRepairTestSceneTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (!Application.isPlaying) yield break;
        Object.FindAnyObjectByType<AppRoot>()?.EndCurrentMatch();
        yield return new ExitPlayMode();
    }

    [UnityTest]
    public IEnumerator InitialStageStartsRepairTargetsUnlockedAndPreservesPlayerChanges()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Gameplay/Initial_Stage.unity");
        yield return new EnterPlayMode();
        yield return null;

        var setup = Object.FindAnyObjectByType<FieldTurretRepairTestSetup>();
        Assert.That(setup, Is.Not.Null);
        var turrets = setup.GetComponentsInChildren<FieldTurretBase>();
        Assert.That(turrets.Length, Is.EqualTo(2));
        var destroyed = turrets.Single(turret => turret.IsDestroyed);
        var damaged = turrets.Single(turret => !turret.IsDestroyed);
        var controller = Object.FindAnyObjectByType<FieldTurretController>();
        int powerBefore = controller.CurrentPower;
        foreach (var turret in turrets)
        {
            Assert.That(turret, Is.TypeOf<FieldCanonTurretLv1>());
            Assert.That(turret.Definition.Level, Is.EqualTo(1));
            Assert.That(turret.MaxHealth, Is.EqualTo(100));
            Assert.That(turret.IsLocked || turret.IsActivated, Is.False);
            Assert.That(controller.OwnsTurret(turret), Is.True);
            Assert.That(turret.TryGetEntityId(out var id) && !id.IsEmpty, Is.True);
            Assert.That(FieldTurretInstanceRegistry.TryGet(turret.InstanceId, out var registered), Is.True);
            Assert.That(registered, Is.SameAs(turret));
        }

        Assert.That(destroyed.CurrentHealth, Is.Zero);
        Assert.That(damaged.CurrentHealth, Is.EqualTo(50));
        Assert.That(damaged.Repair(10), Is.True);
        Assert.That(damaged.CurrentHealth, Is.EqualTo(60));
        Assert.That(destroyed.Repair(10), Is.False);
        Assert.That(destroyed.Restore(), Is.True);
        Assert.That(destroyed.IsDestroyed || destroyed.IsActivated, Is.False);
        Assert.That(destroyed.CurrentHealth, Is.EqualTo(destroyed.MaxHealth));
        Assert.That(controller.CurrentPower, Is.EqualTo(powerBefore));

        setup.gameObject.SetActive(false);
        setup.gameObject.SetActive(true);
        yield return null;
        Assert.That(damaged.CurrentHealth, Is.EqualTo(60), "재활성화가 플레이어의 수리 결과를 초기화하면 안 됩니다.");
        Assert.That(destroyed.IsDestroyed, Is.False, "복구한 터렛을 다시 파괴 상태로 만들면 안 됩니다.");
    }
}
