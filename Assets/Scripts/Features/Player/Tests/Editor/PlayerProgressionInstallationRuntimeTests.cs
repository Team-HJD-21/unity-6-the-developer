using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using TeamHJD.Game.Application;
using TeamHJD.Game.Contracts;
using TeamHJD.Game.Domain;
using TeamHJD.Game.Infrastructure;
using TeamHJD.Game.Presentation.Scene;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class PlayerProgressionInstallationRuntimeTests
{
    private Scene _scene;
    private GameObject _player;
    private PlayerExperience _experience;
    private PlayerSkillResearch _research;
    private PlayerArmory _armory;
    private PlayerInstallationHealthHud _hud;
    private Camera _camera;
    private InputLockService _locks;
    private MatchConfig _config;
    private CheckpointCaptureContext _context;
    private BinaryCheckpointPayloadCodec _codec;

    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _scene = SceneManager.CreateScene("Progression installation regression");
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),new Vector3(60000,60000),Quaternion.identity);
        SceneManager.MoveGameObjectToScene(_player,_scene);
        _player.GetComponent<PlayerInputHandler>().enabled = false;
        _player.GetComponent<PlayerMovement>().enabled = false;
        _player.GetComponent<Rigidbody2D>().simulated = false;
        _experience = _player.GetComponent<PlayerExperience>(); _research = _player.GetComponent<PlayerSkillResearch>();
        _armory = _player.GetComponent<PlayerArmory>(); _hud = _player.GetComponent<PlayerInstallationHealthHud>();
        _locks = new InputLockService(); _hud.BindInputBlocker(_locks);
        var cameraObject = new GameObject("Installation test camera",typeof(Camera)); SceneManager.MoveGameObjectToScene(cameraObject,_scene);
        _camera = cameraObject.GetComponent<Camera>(); _camera.orthographic = true; _camera.orthographicSize = 8;
        _camera.transform.position = _player.transform.position + Vector3.back*10;
        var id = new PlayerId("installation-player");
        (_player.GetComponent<PlayerActorIdentity>()??_player.AddComponent<PlayerActorIdentity>()).Bind(id,0);
        _config = new MatchConfig(new MatchId("installation-regression"),MatchMode.Story,new StageId("stage"),new DefinitionId("normal"),"test",1,new[]{new PlayerLoadoutSnapshot(id,null)});
        _context = new CheckpointCaptureContext(_config,0,Guid.NewGuid()); _codec = new BinaryCheckpointPayloadCodec();
        yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach(var root in _scene.GetRootGameObjects()) Object.Destroy(root);
        yield return null; yield return SceneManager.UnloadSceneAsync(_scene); yield return new ExitPlayMode();
    }
    private CheckpointRestoreContext RestoreContext(CheckpointSection section) => new(_context,
        new CheckpointData(Guid.NewGuid(),DateTimeOffset.UtcNow,_config.MatchId,_config.StageId,_config.DifficultyProfileId,_config.Mode,1,"test",0,1,new[]{section}),CheckpointRestorePolicy.Default);
    private ICheckpointRestoreStep ExperienceStep(long total)
    {
        var writer = (ICheckpointParticipant)_experience; var data = new SerializedObject(_experience);
        var section = _codec.Write(writer.Key,1,CheckpointSectionKind.Progression,w=>
        {
            _codec.WriteText(w,"installation-player");
            w.Write(data.FindProperty("_firstLevelExperience").intValue);w.Write(data.FindProperty("_experienceIncreasePerLevel").intValue);w.Write(data.FindProperty("_maximumLevel").intValue);w.Write(total);
        });
        return writer.PrepareRestore(RestoreContext(section),section,_codec);
    }
    private ICheckpointRestoreStep LegacySkillStep(int tokens)
    {
        var writer = (ICheckpointParticipant)_player.GetComponent<PlayerSkillController>();
        var section = _codec.Write(writer.Key,3,CheckpointSectionKind.World,w=>
        {
            _codec.WriteText(w,"installation-player");w.Write((int)PlayerSkillId.None);w.Write((int)PlayerSkillId.None);w.Write(true);
            w.Write(tokens);w.Write(1);w.Write(1<<(int)PlayerSkillId.Bomb);
            w.Write((int)PlayerSkillId.Bomb);for(int i=0;i<3;i++)w.Write((int)PlayerSkillId.None);
            for(int i=0;i<7;i++)w.Write(0);
        });
        return writer.PrepareRestore(RestoreContext(section),section,_codec);
    }

    [UnityTest]
    public IEnumerator RestoredAndMissedLevelsPaySkillAndWeaponBudgetsWithoutRefundingSpentTokens()
    {
        Assert.That(_research.Tokens,Is.EqualTo(1));Assert.That(_armory.State.Tokens,Is.EqualTo(1));
        int levelUps = 0; _experience.LevelChanged += (_,_)=>levelUps++;
        using(var step=ExperienceStep(400))step.Apply();
        yield return null;
        Assert.That(_experience.Level,Is.EqualTo(5));Assert.That(levelUps,Is.Zero,"Restoring XP is not a new level-up event.");
        Assert.That(_research.Tokens,Is.EqualTo(5));Assert.That(_armory.State.Tokens,Is.EqualTo(5));
        Assert.That(_research.TryUnlock(PlayerSkillId.PlayerTurret),Is.True);Assert.That(_research.TryUpgrade(PlayerSkillId.Bomb),Is.True);
        Assert.That(_armory.State.Upgrade(WeaponType.DefaultGun),Is.True);
        using(var step=ExperienceStep(400))step.Apply();
        yield return null;
        Assert.That(_research.Tokens,Is.EqualTo(3));Assert.That(_armory.State.Tokens,Is.EqualTo(4));
        _research.enabled=false;_armory.enabled=false;
        Assert.That(_experience.AddExperience(200),Is.True);
        _research.enabled=true;_armory.enabled=true;
        Assert.That(_research.Tokens,Is.EqualTo(5));Assert.That(_armory.State.Tokens,Is.EqualTo(6));
        _research.enabled=false;_armory.enabled=false;_research.enabled=true;_armory.enabled=true;
        Assert.That(_research.Tokens,Is.EqualTo(5));Assert.That(_armory.State.Tokens,Is.EqualTo(6));
    }
    [UnityTest]
    public IEnumerator FailedExperienceRestoreDoesNotPayTheIntermediateLevel()
    {
        using(var step=ExperienceStep(800)){step.Apply();step.Rollback();}
        yield return null;
        Assert.That(_experience.Level,Is.EqualTo(1));Assert.That(_research.Tokens,Is.EqualTo(1));Assert.That(_armory.State.Tokens,Is.EqualTo(1));
    }
    [UnityTest]
    public IEnumerator LegacySkillSaveAddsInitialTokenOnceAndRejectsInvalidOldAccounting()
    {
        using(var step=LegacySkillStep(0))
        {
            step.Apply();Assert.That(_research.Tokens,Is.EqualTo(1));
            step.Apply();Assert.That(_research.Tokens,Is.EqualTo(1));step.Rollback();
        }
        yield return null;
        Assert.That(_research.Tokens,Is.EqualTo(1));Assert.Throws<InvalidDataException>(()=>LegacySkillStep(1));
        var writer=(ICheckpointParticipant)_player.GetComponent<PlayerSkillController>();Assert.That(writer.Capture(_context,_codec).Version,Is.EqualTo(4));
    }
    [UnityTest]
    public IEnumerator TimedInstallationsStartAtTwentySecondsAndUpgradeToTwentySix()
    {
        var barrier=_player.GetComponent<PlayerBarrierSkill>();var beacon=_player.GetComponent<PlayerBeaconSkill>();
        Assert.That(barrier.EffectDuration,Is.EqualTo(20));Assert.That(beacon.EffectDuration,Is.EqualTo(20));
        foreach(var skill in new MonoBehaviour[]{barrier,beacon})
        {var data=new SerializedObject(skill);data.FindProperty("_duration").floatValue=8;data.ApplyModifiedPropertiesWithoutUndo();}
        Assert.That(barrier.EffectDuration,Is.EqualTo(20));Assert.That(beacon.EffectDuration,Is.EqualTo(20));
        Assert.That(_experience.AddExperience(800),Is.True);
        foreach(var id in new[]{PlayerSkillId.PlayerTurret,PlayerSkillId.EnergyBarrier,PlayerSkillId.LureBeacon})Assert.That(_research.TryUnlock(id),Is.True);
        foreach(var id in new[]{PlayerSkillId.EnergyBarrier,PlayerSkillId.LureBeacon})
        {Assert.That(_research.TryUpgrade(id),Is.True);Assert.That(_research.TryUpgrade(id),Is.True);}
        Assert.That(barrier.EffectDuration,Is.EqualTo(26).Within(.001));Assert.That(beacon.EffectDuration,Is.EqualTo(26).Within(.001));
        yield break;
    }
    private GameObject Spawn(string name,Vector3 offset)
    {
        var instance=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/"+name+".prefab"),_player.transform.position+offset,Quaternion.identity);
        SceneManager.MoveGameObjectToScene(instance,_scene);return instance;
    }
    [UnityTest]
    public IEnumerator BeaconHoverShowsActualHealthAndHidesForMenusPointerExitOrDestruction()
    {
        var beacon=Spawn("LureBeacon",Vector3.right*3);var device=beacon.GetComponent<PlayerFieldDevice>();
        device.Initialize(PlayerFieldDeviceKind.Beacon,beacon.transform.position,beacon.transform.position,.3f,80,20,12,_player.transform);
        device.TakeDamage(20);var point=(Vector2)beacon.GetComponentInChildren<SpriteRenderer>().bounds.center;
        _hud.Refresh(_camera,point,false);Assert.That(_hud.View.IsVisible,Is.True);Assert.That(_hud.View.HealthText,Is.EqualTo("60 / 80"));Assert.That(_hud.View.HealthRatio,Is.EqualTo(.75f).Within(.001));
        _hud.Refresh(_camera,point,true);Assert.That(_hud.View.IsVisible,Is.False);
        _hud.Refresh(_camera,point+Vector2.up*10,false);Assert.That(_hud.View.IsVisible,Is.False);
        using(_locks.Acquire("Test menu")){_hud.Refresh(_camera,point,false);Assert.That(_hud.View.IsVisible,Is.False);}
        _hud.Refresh(_camera,point,false);Assert.That(_hud.View.IsVisible,Is.True);
        device.TakeDamage(100);_hud.Refresh(_camera,point,false);Assert.That(_hud.View.IsVisible,Is.False);
        yield break;
    }
    [UnityTest]
    public IEnumerator TurretHoverRefreshesAfterRepairAndLocalHudCleansUpOnDisable()
    {
        var turret=Spawn("PlayerTurret",Vector3.left*3);var health=turret.GetComponent<PlayerTurretHealth>();health.TakeDamage(50);
        var point=(Vector2)turret.GetComponentInChildren<SpriteRenderer>().bounds.center;
        _hud.Refresh(_camera,point,false);Assert.That(_hud.View.HealthText,Is.EqualTo("50 / 100"));
        health.Repair(50,1);_hud.Refresh(_camera,point,false);Assert.That(_hud.View.HealthText,Is.EqualTo("100 / 100"));
        _hud.enabled=false;Assert.That(_hud.View,Is.Null);_hud.enabled=true;Assert.That(_hud.View,Is.Not.Null);
        yield break;
    }
    [UnityTest]
    public IEnumerator BothBarrierEmittersAndMembraneShowTheSameSharedHealth()
    {
        var barrier=Spawn("EnergyBarrier",Vector3.up*3);var device=barrier.GetComponent<PlayerFieldDevice>();
        Vector3 start=barrier.transform.position,end=start+Vector3.right*6;
        device.Initialize(PlayerFieldDeviceKind.Barrier,start,end,.3f,300,20,0,_player.transform);device.TakeDamage(75);
        foreach(var point in new[]{start,end,(start+end)*.5f})
        {_hud.Refresh(_camera,point,false);Assert.That(_hud.View.IsVisible,Is.True);Assert.That(_hud.View.HealthText,Is.EqualTo("225 / 300"));}
        var other=SceneManager.CreateScene("Other installation scene");SceneManager.MoveGameObjectToScene(barrier,other);
        _hud.Refresh(_camera,start,false);Assert.That(_hud.View.IsVisible,Is.False);
        Object.Destroy(barrier);yield return null;yield return SceneManager.UnloadSceneAsync(other);
    }
}
