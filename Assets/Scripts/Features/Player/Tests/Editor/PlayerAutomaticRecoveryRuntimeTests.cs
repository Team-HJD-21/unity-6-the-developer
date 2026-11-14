using System.Collections;
using System.Threading;
using NUnit.Framework;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerAutomaticRecoveryRuntimeTests
{
    private Scene _scene;
    private CancellationTokenSource _lifetime;
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        if(_scene.IsValid())yield return SceneManager.UnloadSceneAsync(_scene);
        _lifetime?.Dispose();
        if(Application.isPlaying)yield return new ExitPlayMode();
    }
    [UnityTest]
    public IEnumerator ActualPlayerFixedUpdatesRecoverAtConfiguredRateAndPublishHealthWithoutHitFeedback()
    {
        yield return new EnterPlayMode();
        _scene=SceneManager.CreateScene("Automatic recovery runtime");_lifetime=new CancellationTokenSource();
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),new Vector3(48000,48000),Quaternion.identity);
        SceneManager.MoveGameObjectToScene(actor,_scene);actor.GetComponent<PlayerInputHandler>().enabled=false;
        var clock=new PlayerAutomaticRecoveryTests.Clock();FieldSimulationActor.Attach(actor,clock,_lifetime.Token,false);
        var health=actor.GetComponent<PlayerInfo>();int displayed=0,damageEvents=0;
        health.onHpChange.AddListener((current,max)=>displayed=current);health.Damaged+=_=>damageEvents++;
        Assert.That(health.AutomaticRecoveryDelay,Is.EqualTo(5));Assert.That(health.AutomaticRecoveryFractionPerSecond,Is.EqualTo(.02f));
        int maximum=health.maxHp;Assert.That(health.ApplyDamage(100),Is.True);
        clock.Now=4.99;yield return new WaitForFixedUpdate();yield return null;Assert.That(health.curHp,Is.EqualTo(maximum-100));
        clock.Now=5;yield return new WaitForFixedUpdate();yield return null;Assert.That(health.curHp,Is.EqualTo(maximum-100));
        clock.Now=6;yield return new WaitForFixedUpdate();yield return null;
        Assert.That(health.curHp,Is.EqualTo(maximum-100+Mathf.RoundToInt(maximum*.02f)));Assert.That(displayed,Is.EqualTo(health.curHp));
        Assert.That(damageEvents,Is.EqualTo(1));
        health.ApplyDamage(10);int afterDamage=health.curHp;
        clock.Now=10.99;yield return new WaitForFixedUpdate();yield return null;Assert.That(health.curHp,Is.EqualTo(afterDamage));
        clock.Now=12;yield return new WaitForFixedUpdate();yield return null;Assert.That(health.curHp,Is.EqualTo(afterDamage+Mathf.RoundToInt(maximum*.02f)));
        clock.Paused=true;clock.Now=100;int beforePause=health.curHp;
        yield return new WaitForFixedUpdate();yield return null;Assert.That(health.curHp,Is.EqualTo(beforePause));
    }
}
