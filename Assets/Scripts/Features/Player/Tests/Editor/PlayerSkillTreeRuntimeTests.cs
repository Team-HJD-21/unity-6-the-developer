using System;
using System.Collections;
using System.Linq;
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
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class PlayerSkillTreeRuntimeTests
{
    private GameObject _player, _hud, _skillsHud;
    private Scene _scene;
    private PlayerSkillController _controller;
    private PlayerSkillResearch _research;
    private Clock _clock;
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    [UnitySetUp]
    public IEnumerator SetUp()
    {
        yield return new EnterPlayMode();
        _scene = SceneManager.CreateScene("Skill tree integration");
        _player = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Player.prefab"),new Vector3(60000,60000),Quaternion.identity);
        SceneManager.MoveGameObjectToScene(_player,_scene);
        _player.GetComponent<PlayerInputHandler>().enabled=false;
        _player.GetComponent<PlayerMovement>().enabled=false;
        _player.GetComponent<Rigidbody2D>().simulated=false;
        _controller=_player.GetComponent<PlayerSkillController>();_research=_player.GetComponent<PlayerSkillResearch>();
        _clock=new Clock();FieldSimulationActor.Attach(_player,_clock);
        Aim(new Vector2(8,3)); yield return null;
    }
    [UnityTearDown]
    public IEnumerator TearDown()
    {
        foreach(var root in _scene.GetRootGameObjects())Object.Destroy(root);
        yield return null;yield return SceneManager.UnloadSceneAsync(_scene);yield return new ExitPlayMode();
    }
    private void Aim(Vector2 offset)
    {
        var input=_player.GetComponent<PlayerInputHandler>();
        typeof(PlayerInputHandler).GetProperty("AimDirection").SetValue(input,offset.normalized);
        typeof(PlayerInputHandler).GetProperty("HasAimTarget").SetValue(input,true);
        typeof(PlayerInputHandler).GetProperty("MouseWorldPosition").SetValue(input,(Vector2)_player.transform.position+offset);
    }
    private void Reward() => Assert.That(_player.GetComponent<PlayerExperience>().AddExperience(400),Is.True);
    private void UnlockAll()
    {
        Reward();
        foreach(var id in new[]{PlayerSkillId.DualWield,PlayerSkillId.PlayerTurret,PlayerSkillId.EnergyBarrier,PlayerSkillId.LureBeacon})
            Assert.That(_research.TryUnlock(id),Is.True,id.ToString());
    }
    private void ActivateDual(WeaponType weapon)
    {
        Reward();Assert.That(_research.TryUnlock(PlayerSkillId.DualWield),Is.True);
        Assert.That(_research.TrySetCandidate(1,PlayerSkillId.DualWield),Is.True);
        Assert.That(_controller.TryEquip(0,PlayerSkillId.DualWield),Is.True);
        var selection=_player.GetComponent<PlayerWeaponSelection>();
        if(weapon==WeaponType.Sniper)_player.GetComponent<PlayerArmory>().State.Equip(0,WeaponType.Sniper);
        Assert.That(selection.TrySelectWeapon(weapon),Is.True);
        Assert.That(_controller.TryUseSlot(0),Is.True);
    }
    [UnityTest]
    public IEnumerator BombIsTheOnlyFreeRootAndLevelRewardsUnlockPrerequisitesOnce()
    {
        Assert.That(_research.Tokens,Is.EqualTo(1));Assert.That(_research.IsUnlocked(PlayerSkillId.Bomb),Is.True);
        Assert.That(_research.Candidate(0),Is.EqualTo(PlayerSkillId.Bomb));Assert.That(_research.Candidate(1),Is.EqualTo(PlayerSkillId.None));
        Assert.That(_controller.TryEquip(0,PlayerSkillId.PlayerTurret),Is.False);
        Assert.That(_research.CanUnlock(PlayerSkillId.PlayerTurret),Is.True);
        Reward();Assert.That(_research.Tokens,Is.EqualTo(5));
        Assert.That(_research.TryUnlock(PlayerSkillId.EnergyBarrier),Is.False);
        Assert.That(_research.TryUnlock(PlayerSkillId.PlayerTurret),Is.True);
        Assert.That(_research.TryUnlock(PlayerSkillId.PlayerTurret),Is.False);
        Assert.That(_research.Tokens,Is.EqualTo(4));
        _research.enabled=false;_research.enabled=true;
        Assert.That(_research.Tokens,Is.EqualTo(4));
        Assert.That(_research.TryUnlock(PlayerSkillId.EnergyBarrier),Is.True);
        Assert.That(_research.TryUnlock(PlayerSkillId.Portal),Is.False,"Portal still requires the beacon prerequisite.");
        yield break;
    }
    [UnityTest]
    public IEnumerator FourCandidatesSwapAndReplacingAnEquippedSkillClearsOnlyItsSlot()
    {
        UnlockAll();Assert.That(_controller.TryEquip(0,PlayerSkillId.Bomb),Is.True);
        Assert.That(_research.TrySetCandidate(1,PlayerSkillId.DualWield),Is.True);
        Assert.That(_controller.TryEquip(1,PlayerSkillId.DualWield),Is.True);
        Assert.That(_research.TrySetCandidate(0,PlayerSkillId.DualWield),Is.True);
        Assert.That(_research.Candidate(1),Is.EqualTo(PlayerSkillId.Bomb));
        Assert.That(_controller.Loadout.Q,Is.EqualTo(PlayerSkillId.Bomb));
        Assert.That(_research.TrySetCandidate(1,PlayerSkillId.PlayerTurret),Is.True);
        Assert.That(_controller.Loadout.Q,Is.EqualTo(PlayerSkillId.None));
        Assert.That(_controller.Loadout.E,Is.EqualTo(PlayerSkillId.DualWield));
        Assert.That(_controller.TryUse(PlayerSkillId.Bomb),Is.False);
        Assert.That(_research.TrySetCandidate(4,PlayerSkillId.Bomb),Is.False);
        yield break;
    }
    [UnityTest]
    public IEnumerator TreeButtonsUnlockAndPopulateRealBottomWedgesWithoutChangingQE()
    {
        Reward();
        _hud=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/InitialStagePlayerHUD.prefab"));SceneManager.MoveGameObjectToScene(_hud,_scene);
        _hud.GetComponent<InitialStagePlayerHUD>().enabled=false;
        _hud.GetComponent<Canvas>().enabled=true;
        _hud.GetComponent<StagePlayerVitalsView>().BindPlayer(_player.GetComponent<PlayerInfo>());
        var status=_hud.GetComponent<PlayerStatusPanel>();Assert.That(status.TryOpen(),Is.True);
        var tree=_hud.GetComponentInChildren<PlayerSkillTreePanel>(true);tree.Open();Assert.That(tree.IsOpen&&tree.IsConfigured,Is.True);
        tree.Select(PlayerSkillId.DualWield);
        var unlock=(Button)typeof(PlayerSkillTreePanel).GetField("_unlock",Private).GetValue(tree);unlock.onClick.Invoke();
        Assert.That(_research.IsUnlocked(PlayerSkillId.DualWield),Is.True);Assert.That(tree.Assign(3),Is.True);
        var candidateNames=(TMPro.TMP_Text[])typeof(PlayerSkillTreePanel).GetField("_candidateNames",Private).GetValue(tree);
        var candidateStates=(TMPro.TMP_Text[])typeof(PlayerSkillTreePanel).GetField("_candidateStates",Private).GetValue(tree);
        var candidateIcons=(Image[])typeof(PlayerSkillTreePanel).GetField("_candidateIcons",Private).GetValue(tree);
        Assert.That(candidateNames[3].text,Is.EqualTo(PlayerSkillDescriptions.Name(PlayerSkillId.DualWield)));
        Assert.That(candidateStates[3].text,Is.EqualTo("배치됨"));Assert.That(candidateIcons[3].enabled&&candidateIcons[3].sprite!=null,Is.True);
        Assert.That(candidateNames[0].text,Is.EqualTo(PlayerSkillDescriptions.Name(PlayerSkillId.Bomb)));
        _skillsHud=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/UI/PlayerSkillHUD.prefab"));SceneManager.MoveGameObjectToScene(_skillsHud,_scene);
        _skillsHud.GetComponent<PlayerSkillHudSceneBinding>().enabled=false;
        var hud=_skillsHud.GetComponent<PlayerSkillHud>();Assert.That(_controller.BindHud(hud),Is.True);
        var wedge=hud.CombatPicker.Wedges.Single(w=>w.Index==3);
        Assert.That(wedge.Skill,Is.EqualTo(PlayerSkillId.DualWield));
        Assert.That(wedge.Name.text,Is.EqualTo(PlayerSkillDescriptions.Name(PlayerSkillId.DualWield)));
        Assert.That(wedge.Name.font.HasCharacters(wedge.Name.text),Is.True);
        Assert.That(_controller.Loadout.Q,Is.EqualTo(PlayerSkillId.None));Assert.That(_controller.Loadout.E,Is.EqualTo(PlayerSkillId.None));
        var statusCanvas=_hud.GetComponent<Canvas>();int order=statusCanvas.sortingOrder;
        statusCanvas.sortingOrder=30000;
        Canvas.ForceUpdateCanvases();ScreenCapture.CaptureScreenshot("Docs/Local/PlayerArmory/skill-tree.png");yield return null;yield return null;
        statusCanvas.sortingOrder=order;
        tree.Close();status.Close();hud.CombatPicker.Open();yield return null;
        var destination=new Vector2(Screen.width*.25f,Screen.height*.55f);
        Assert.That(hud.CombatPicker.BeginDrag(PlayerSkillId.DualWield,Vector2.zero),Is.True);
        Assert.That(hud.CombatPicker.Release(destination),Is.True);
        Assert.That(_controller.Loadout.Q,Is.EqualTo(PlayerSkillId.DualWield));
    }
    [UnityTest]
    public IEnumerator DefaultGunFiresFromBothDisplayedMuzzlesAtOneTargetAndConsumesOneRound()
    { yield return DualProjectile(WeaponType.DefaultGun); }
    [UnityTest]
    public IEnumerator SniperFiresFromBothDisplayedMuzzlesWithoutLosingPenetrationOrExtraAmmo()
    { yield return DualProjectile(WeaponType.Sniper); }
    private IEnumerator DualProjectile(WeaponType weapon)
    {
        ActivateDual(weapon);
        var visual=_player.GetComponent<PlayerWeaponVisual>();visual.Refresh();
        Assert.That(visual.TryGetSecondaryMuzzle(out var muzzle,out var direction),Is.True);
        var target=_player.GetComponent<PlayerInputHandler>().MouseWorldPosition;
        Assert.That(Mathf.Abs(Vector2.SignedAngle(direction,target-(Vector2)muzzle)),Is.LessThan(.01f));
        Assert.That(visual.TryGetMuzzlePosition(out var primary),Is.True);
        Assert.That(Vector2.Distance(primary,muzzle),Is.InRange(.05f,.65f));
        int before=_player.GetComponent<PlayerWeaponAmmo>().CurrentRounds;
        Assert.That(_player.GetComponent<PlayerAttack>().TryFire(),Is.True);
        var bullets=Object.FindObjectsByType<PlayerBullet>(FindObjectsSortMode.None).Where(b=>b.gameObject.scene==_scene).ToArray();
        Assert.That(bullets.Length,Is.EqualTo(2));
        Assert.That(bullets.Select(b=>b.transform.position).Distinct().Count(),Is.EqualTo(2));
        foreach(var origin in new[]{primary,muzzle})
            Assert.That(bullets.Any(b=>Vector2.Distance(b.transform.position,origin)<.01f),Is.True,"A bullet must start at each displayed muzzle.");
        Assert.That(_player.GetComponent<PlayerWeaponAmmo>().CurrentRounds,Is.EqualTo(before-1));
        if(weapon==WeaponType.Sniper)foreach(var b in bullets)Assert.That(b.MaximumEnemyTargets,Is.EqualTo(2));
        yield break;
    }
    [UnityTest]
    public IEnumerator CryoUsesTwoIndependentSprayOriginsAndSameAmmoCost()
    {
        ActivateDual(WeaponType.CryoBlaster);
        var visual=_player.GetComponent<PlayerWeaponVisual>();visual.Refresh();
        Assert.That(visual.TryGetSecondaryMuzzle(out var second,out var direction),Is.True);
        Assert.That(visual.TryGetMuzzlePosition(out var first),Is.True);
        var enemyObject=new GameObject("Dual spray target");SceneManager.MoveGameObjectToScene(enemyObject,_scene);
        enemyObject.transform.position=(first+second)*.5f+(Vector3)direction*2f;
        enemyObject.layer=LayerMask.NameToLayer("EnemyHurtbox");
        var hitbox=enemyObject.AddComponent<CircleCollider2D>();hitbox.isTrigger=true;hitbox.radius=.55f;
        var enemy=enemyObject.AddComponent<EnemyHealth>();
        typeof(EnemyHealth).GetField("maxHealth",Private).SetValue(enemy,100);enemy.Initialize();FieldSimulationActor.Attach(enemyObject,_clock);
        int before=_player.GetComponent<PlayerWeaponAmmo>().CurrentRounds;
        Assert.That(_player.GetComponent<PlayerAttack>().TryFire(),Is.True);
        Assert.That(_player.GetComponent<PlayerWeaponAmmo>().CurrentRounds,Is.EqualTo(before-1));
        Assert.That(enemy.CurrentHealth,Is.EqualTo(84),"Each independent spray must damage the target once.");
        Assert.That(_player.GetComponents<PlayerCryoSpray>().Length,Is.EqualTo(2));
        Assert.That(visual.SecondaryCryoSpray.IsConfigured,Is.True);
        Assert.That(Object.FindObjectsByType<PlayerBullet>(FindObjectsSortMode.None).Any(b=>b.gameObject.scene==_scene),Is.False);
        yield break;
    }
    [UnityTest]
    public IEnumerator DurationCooldownAndSpannerOrDeathRespectTheSameMatchClock()
    {
        ActivateDual(WeaponType.DefaultGun);var skill=_player.GetComponent<PlayerDualWieldSkill>();var visual=_player.GetComponent<PlayerWeaponVisual>();
        Assert.That(skill.ActiveRemaining,Is.EqualTo(8).Within(.001));
        _clock.Now=7;Assert.That(skill.IsActive,Is.True);
        _clock.Paused=true;yield return null;Assert.That(skill.ActiveRemaining,Is.EqualTo(1).Within(.001));
        _clock.Paused=false;
        Assert.That(_player.GetComponent<PlayerWeaponSelection>().TrySelectWeapon(WeaponType.Spanner),Is.True);visual.Refresh();
        Assert.That(visual.TryGetSecondaryMuzzle(out _,out _),Is.False);
        _clock.Now=8;Assert.That(skill.IsActive,Is.False);Assert.That(skill.RemainingCooldown,Is.EqualTo(12).Within(.001));
        _clock.Now=20;Assert.That(_controller.TryUseSlot(0),Is.False,"Cannot activate while using a spanner.");
        _player.GetComponent<PlayerWeaponSelection>().TrySelectWeapon(WeaponType.DefaultGun);Assert.That(_controller.TryUseSlot(0),Is.True);
        _player.GetComponent<PlayerInfo>().curHp=0;Assert.That(skill.IsActive,Is.False);
        typeof(PlayerDualWieldSkill).GetMethod("Update",Private).Invoke(skill,null);
        Assert.That(skill.ActiveRemaining,Is.Zero,"The death update must discard the remaining buff time.");
        _player.GetComponent<PlayerInfo>().curHp=400;Assert.That(skill.IsActive,Is.False,"Reviving must not resume a cancelled buff.");
    }
    [UnityTest]
    public IEnumerator CheckpointRestoresTokensUnlocksPaletteAndQEAtomicallyAndRollsBack()
    {
        ActivateDual(WeaponType.DefaultGun);
        var identity=_player.GetComponent<PlayerActorIdentity>()??_player.AddComponent<PlayerActorIdentity>();identity.Bind(new PlayerId("tree-player"),0);
        var config=new MatchConfig(new MatchId("skill-tree-test"),MatchMode.Story,new StageId("stage"),new DefinitionId("normal"),"test",1,
            new[]{new PlayerLoadoutSnapshot(new PlayerId("tree-player"),null)});
        Assert.That(_research.TryUpgrade(PlayerSkillId.DualWield),Is.True);
        var context=new CheckpointCaptureContext(config,0,Guid.NewGuid());var codec=new BinaryCheckpointPayloadCodec();
        var writer=(ICheckpointParticipant)_controller;var section=writer.Capture(context,codec);
        Assert.That(_research.TryUnlock(PlayerSkillId.PlayerTurret),Is.True);Assert.That(_research.TrySetCandidate(1,PlayerSkillId.PlayerTurret),Is.True);
        var target=new CheckpointData(Guid.NewGuid(),DateTimeOffset.UtcNow,config.MatchId,config.StageId,config.DifficultyProfileId,config.Mode,config.RandomSeed,config.ContentVersion,0,1,new[]{section});
        using(var step=writer.PrepareRestore(new CheckpointRestoreContext(context,target,CheckpointRestorePolicy.Default),section,codec))
        {
            step.Apply();Assert.That(_research.IsUnlocked(PlayerSkillId.PlayerTurret),Is.False);Assert.That(_research.Tokens,Is.EqualTo(3));
            Assert.That(_research.Level(PlayerSkillId.DualWield),Is.EqualTo(2));
            Assert.That(_research.Candidate(1),Is.EqualTo(PlayerSkillId.DualWield));Assert.That(_controller.Loadout.Q,Is.EqualTo(PlayerSkillId.DualWield));
            step.Rollback();Assert.That(_research.IsUnlocked(PlayerSkillId.PlayerTurret),Is.True);Assert.That(_research.Candidate(1),Is.EqualTo(PlayerSkillId.PlayerTurret));
            Assert.That(_controller.Loadout.Q,Is.EqualTo(PlayerSkillId.None));
        }
        yield break;
    }
    [UnityTest]
    public IEnumerator UpgradesConsumeTheSameTokensCapAtThreeAndChangeActualSkillStats()
    {
        Assert.That(_player.GetComponent<PlayerExperience>().AddExperience(2000),Is.True);
        foreach(var id in new[]{PlayerSkillId.DualWield,PlayerSkillId.PlayerTurret,PlayerSkillId.EnergyBarrier,PlayerSkillId.LureBeacon,PlayerSkillId.Portal})
            Assert.That(_research.TryUnlock(id),Is.True);
        foreach(var id in new[]{PlayerSkillId.Bomb,PlayerSkillId.DualWield,PlayerSkillId.PlayerTurret,PlayerSkillId.EnergyBarrier,PlayerSkillId.LureBeacon,PlayerSkillId.Portal})
        {
            float before=_controller.UpgradeStatValue(id), hp=_controller.UpgradeHealthValue(id), duration=_controller.UpgradeDurationValue(id);int tokens=_research.Tokens;
            Assert.That(_research.TryUpgrade(id),Is.True);Assert.That(_research.TryUpgrade(id),Is.True);
            Assert.That(_research.Tokens,Is.EqualTo(tokens-2));Assert.That(_research.Level(id),Is.EqualTo(3));
            Assert.That(_controller.UpgradeStatValue(id),Is.EqualTo(before*1.3f).Within(.001f));
            Assert.That(_controller.UpgradeHealthValue(id),Is.EqualTo(hp*1.3f).Within(.001f));
            Assert.That(_controller.UpgradeDurationValue(id),Is.EqualTo(duration*1.3f).Within(.001f));
            Assert.That(_research.TryUpgrade(id),Is.False);
        }
        var bomb=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/bomb.prefab"));SceneManager.MoveGameObjectToScene(bomb,_scene);
        bomb.GetComponent<PlayerBombExplosion>().BindOwner(_player.transform);
        Assert.That(bomb.GetComponent<PlayerBombExplosion>().Damage,Is.EqualTo(_controller.UpgradeStatValue(PlayerSkillId.Bomb)).Within(.001));
        var turret=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurret.prefab"));SceneManager.MoveGameObjectToScene(turret,_scene);
        turret.GetComponent<PlayerTurret>().AssignOwner(_player.GetComponent<PlayerTurretSkill>());
        Assert.That(turret.GetComponent<PlayerTurret>().ShotDamage,Is.EqualTo(_controller.UpgradeStatValue(PlayerSkillId.PlayerTurret)).Within(.001));
        Assert.That(_research.TrySetCandidate(1,PlayerSkillId.DualWield),Is.True);Assert.That(_controller.TryEquip(0,PlayerSkillId.DualWield),Is.True);
        _player.GetComponent<PlayerWeaponSelection>().TrySelectWeapon(WeaponType.DefaultGun);Assert.That(_controller.TryUseSlot(0),Is.True);
        Assert.That(_player.GetComponent<PlayerDualWieldSkill>().ActiveRemaining,Is.EqualTo(10.4f).Within(.001));
        Assert.That(_player.GetComponent<PlayerAttack>().TryFire(),Is.True);
        var bullets=Object.FindObjectsByType<PlayerBullet>(FindObjectsSortMode.None).Where(b=>b.gameObject.scene==_scene).ToArray();
        Assert.That(bullets.Length,Is.EqualTo(2));foreach(var bullet in bullets)
            Assert.That((float)typeof(PlayerBullet).GetField("_damage",Private).GetValue(bullet),Is.EqualTo(13*1.3f).Within(.001f));
        yield break;
    }
    [UnityTest]
    public IEnumerator UpgradedDevicesKeepTheirSpawnedHealthLifetimeAndCheckpointValues()
    {
        _player.GetComponent<PlayerExperience>().AddExperience(2000);
        foreach(var id in new[]{PlayerSkillId.PlayerTurret,PlayerSkillId.EnergyBarrier,PlayerSkillId.LureBeacon})
        { Assert.That(_research.TryUnlock(id),Is.True);Assert.That(_research.TryUpgrade(id),Is.True);Assert.That(_research.TryUpgrade(id),Is.True); }
        var identity=_player.GetComponent<PlayerActorIdentity>();if(identity==null)identity=_player.AddComponent<PlayerActorIdentity>();identity.Bind(new PlayerId("tree-player"),0);
        var config=new MatchConfig(new MatchId("skill-tree-test"),MatchMode.Story,new StageId("stage"),new DefinitionId("normal"),"test",1,new[]{new PlayerLoadoutSnapshot(new PlayerId("tree-player"),null)});
        var context=new CheckpointCaptureContext(config,0,Guid.NewGuid());var codec=new BinaryCheckpointPayloadCodec();
        var beacon=_player.GetComponent<PlayerBeaconSkill>();var barrier=_player.GetComponent<PlayerBarrierSkill>();
        typeof(PlayerBeaconSkill).GetMethod("TryPlace",Private).Invoke(beacon,new object[]{_player.transform.position+Vector3.right*5,false});
        typeof(PlayerBarrierSkill).GetMethod("TryPlace",Private).Invoke(barrier,new object[]{_player.transform.position+Vector3.up*5,false});
        typeof(PlayerBarrierSkill).GetMethod("TryPlace",Private).Invoke(barrier,new object[]{_player.transform.position+new Vector3(5,5),false});
        barrier.CancelPlacement(); // 실제 배치 확정 경로처럼 첫 지점 선택 상태를 종료합니다.
        foreach(var skill in new PlayerSkillBase[]{beacon,barrier})
        {
            var kind=skill==beacon?PlayerFieldDeviceKind.Beacon:PlayerFieldDeviceKind.Barrier;
            PlayerFieldDevice Device()=>Object.FindObjectsByType<PlayerFieldDevice>(FindObjectsSortMode.None).Single(d=>d.gameObject.scene==_scene&&d.Kind==kind&&d.IsAlive);
            Assert.That(Device().Health,Is.EqualTo(kind==PlayerFieldDeviceKind.Beacon?104:390).Within(.001));
            Assert.That((float)typeof(PlayerFieldDevice).GetField("_remaining",Private).GetValue(Device()),Is.EqualTo(26f).Within(.001));
            var writer=(ICheckpointParticipant)skill;var saved=writer.Capture(context,codec);Device().TakeDamage(10);
            var checkpoint=new CheckpointData(Guid.NewGuid(),DateTimeOffset.UtcNow,config.MatchId,config.StageId,config.DifficultyProfileId,config.Mode,1,"test",0,1,new[]{saved});
            using(var step=writer.PrepareRestore(new CheckpointRestoreContext(context,checkpoint,CheckpointRestorePolicy.Default),saved,codec))
            {step.Apply();Assert.That(Device().Health,Is.EqualTo(kind==PlayerFieldDeviceKind.Beacon?104:390).Within(.001));step.Rollback();Assert.That(Device().Health,Is.EqualTo(kind==PlayerFieldDeviceKind.Beacon?94:380).Within(.001));}
        }
        var turret=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/PlayerTurret.prefab"));SceneManager.MoveGameObjectToScene(turret,_scene);
        var health=turret.GetComponent<PlayerTurretHealth>();typeof(PlayerTurretHealth).GetMethod("SetDeploymentHealth",Private).Invoke(health,new object[]{.5f,1.3f});
        Assert.That(health.MaxHealth,Is.EqualTo(130).Within(.001));Assert.That(health.Health,Is.EqualTo(65).Within(.001));
        var savedHealth=typeof(PlayerTurretHealth).GetMethod("CaptureCheckpoint",Private).Invoke(health,null);
        health.TakeDamage(10);
        typeof(PlayerTurretHealth).GetMethod("RestoreCheckpoint",Private).Invoke(health,new object[]{130f,65f,0f,0f});
        Assert.That(health.HealthRatio,Is.EqualTo(.5f).Within(.001));
        Assert.That(PlayerSkillDescriptions.UpgradeStats(PlayerSkillId.EnergyBarrier,_controller),Does.Contain("체력").And.Contain("지속시간").And.Contain("26"));
        yield break;
    }

    [UnityTest]
    public IEnumerator DualWieldKeepsOriginalGunPoseAndPlacesSecondHandJustToItsLeftForEveryAim()
    {
        var visual=_player.GetComponent<PlayerWeaponVisual>();
        var aims=new[]{Vector2.right*8,Vector2.left*8,Vector2.up*8,Vector2.down*8,new Vector2(-7,5)};
        var positions=new Vector3[aims.Length];var rotations=new Quaternion[aims.Length];var muzzles=new Vector3[aims.Length];
        for(int i=0;i<aims.Length;i++)
        {
            Aim(aims[i]);visual.Refresh();
            positions[i]=visual.Renderer.transform.localPosition;rotations[i]=visual.Renderer.transform.localRotation;
            Assert.That(visual.TryGetMuzzlePosition(out muzzles[i]),Is.True);
        }
        ActivateDual(WeaponType.DefaultGun);
        var secondary=(SpriteRenderer)typeof(PlayerWeaponVisual).GetField("_secondaryWeapon",Private).GetValue(visual);
        var authoredGrip=new SerializedObject(visual).FindProperty("_aimDefaultGun").FindPropertyRelative("grip").vector2Value;
        for(int i=0;i<aims.Length;i++)
        {
            Aim(aims[i]);visual.Refresh();
            Assert.That(Vector3.Distance(visual.Renderer.transform.localPosition,positions[i]),Is.LessThan(.001f),"Activating dual wield must keep the original gun in place.");
            Assert.That(Quaternion.Angle(visual.Renderer.transform.localRotation,rotations[i]),Is.LessThan(.01f));
            Assert.That(visual.TryGetMuzzlePosition(out var primary),Is.True);
            Assert.That(Vector3.Distance(primary,muzzles[i]),Is.LessThan(.01f));
            Vector3 Grip(SpriteRenderer renderer)
            {
                Vector2 p=(renderer.sprite.rect.size*authoredGrip-renderer.sprite.pivot)/renderer.sprite.pixelsPerUnit;
                if(renderer.flipY)p.y=-p.y;return _player.transform.InverseTransformPoint(renderer.transform.TransformPoint(p));
            }
            var first=Grip(visual.Renderer);var second=Grip(secondary);
            Assert.That(second.x,Is.EqualTo(first.x-.45f).Within(.01f));
            Assert.That(first.y,Is.EqualTo(second.y).Within(.01f));
            Assert.That(visual.TryGetSecondaryMuzzle(out var muzzle,out var direction),Is.True);
            Assert.That(Vector2.Angle(direction,_player.GetComponent<PlayerInputHandler>().MouseWorldPosition-(Vector2)muzzle),Is.LessThan(.01));
        }
        yield break;
    }
    [UnityTest]
    public IEnumerator AuthoredPortalPairTravelsWithRReservesReloadAndFRecoversOneEndpoint()
    {
        Reward();foreach(var id in new[]{PlayerSkillId.PlayerTurret,PlayerSkillId.EnergyBarrier,PlayerSkillId.LureBeacon,PlayerSkillId.Portal})Assert.That(_research.TryUnlock(id),Is.True);
        Reward();Assert.That(_research.TryUpgrade(PlayerSkillId.Portal),Is.True);Assert.That(_research.TryUpgrade(PlayerSkillId.Portal),Is.True);
        Assert.That(_research.TrySetCandidate(1,PlayerSkillId.Portal),Is.True);Assert.That(_controller.TryEquip(0,PlayerSkillId.Portal),Is.True);
        var grid=new GameObject("Portal ground",typeof(Grid));SceneManager.MoveGameObjectToScene(grid,_scene);grid.transform.position=_player.transform.position;
        var mapObject=new GameObject("Ground",typeof(UnityEngine.Tilemaps.Tilemap),typeof(UnityEngine.Tilemaps.TilemapCollider2D));mapObject.transform.SetParent(grid.transform,false);
        var map=mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();var tile=ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();tile.colliderType=UnityEngine.Tilemaps.Tile.ColliderType.None;
        var portal=_player.GetComponent<PlayerPortalSkill>();
        var data=new SerializedObject(portal);data.FindProperty("_placementGround").objectReferenceValue=map;data.ApplyModifiedPropertiesWithoutUndo();
        for(int x=-5;x<25;x++)for(int y=-8;y<9;y++)map.SetTile(new Vector3Int(x,y,0),tile);
        Vector3 start=_player.transform.position;
        foreach(float x in new[]{5f,11f})
        {Assert.That(_controller.TryUseSlot(0),Is.True);Assert.That(portal.TryConfirmPlacement(start+Vector3.right*x),Is.True,portal.PlacementFailure);}
        Assert.That(portal.IsLinked,Is.True);
        var firstBody=portal.GetEndpoint(0).GetComponentInChildren<SpriteRenderer>();
        var secondBody=portal.GetEndpoint(1).GetComponentInChildren<SpriteRenderer>();
        Assert.That(firstBody.sprite.name,Is.EqualTo("PortalGateA"));Assert.That(secondBody.sprite.name,Is.EqualTo("PortalGateB"));
        Assert.That(firstBody.bounds.size.y,Is.GreaterThan(firstBody.bounds.size.x));
        Assert.That(portal.GetEndpoint(0).FootprintAt(start).size.y,Is.LessThan(firstBody.bounds.size.y*.3f));
        Assert.That(firstBody.color,Is.EqualTo(Color.white));Assert.That(secondBody.color,Is.EqualTo(Color.white));
        var rigidbody=_player.GetComponent<Rigidbody2D>();rigidbody.simulated=true;
        void Stand(Vector3 feet)
        {var ground=_player.GetComponent<PlayerDirectionalColliders>().GroundPosition;var p=_player.transform.position+feet-(Vector3)ground;_player.transform.position=p;_player.GetComponent<Rigidbody2D>().position=p;Physics2D.SyncTransforms();}
        var endpoint=portal.GetEndpoint(0);
        var footColliders=endpoint.GetComponentsInChildren<BoxCollider2D>();
        Assert.That(footColliders.Length,Is.EqualTo(2));Assert.That(footColliders.All(c=>c.enabled&&!c.isTrigger),Is.True);
        // 물리 시뮬레이션으로 받침은 막히고 중앙 통로는 통과되는지 확인합니다.
        Stand(endpoint.transform.position+Vector3.right*2.2f);
        Assert.That(portal.NearestInteractable,Is.SameAs(endpoint),"The portal's own frame must not hide R/F interaction from the side.");
        rigidbody.linearVelocity=Vector2.left*3;
        for(int i=0;i<Mathf.CeilToInt(2f/(3*Time.fixedDeltaTime));i++)yield return new WaitForFixedUpdate();
        Assert.That(_player.GetComponent<PlayerDirectionalColliders>().GroundPosition.x,Is.GreaterThan(endpoint.transform.position.x+1.2f),"The right foot must physically stop the player.");
        rigidbody.linearVelocity=Vector2.zero;Stand(endpoint.transform.position+Vector3.down*1.2f);
        rigidbody.linearVelocity=Vector2.up*3;
        for(int i=0;i<Mathf.CeilToInt(2f/(3*Time.fixedDeltaTime));i++)yield return new WaitForFixedUpdate();
        Assert.That(_player.GetComponent<PlayerDirectionalColliders>().GroundPosition.y,Is.GreaterThan(endpoint.transform.position.y+.2f),"The center doorway must remain open.");
        rigidbody.linearVelocity=Vector2.zero;
        _player.GetComponent<PlayerInputHandler>().enabled=true;_player.GetComponent<PlayerMovement>().enabled=true;
        Stand(portal.GetEndpoint(0).transform.position);
        var keyboardBefore=UnityEngine.InputSystem.Keyboard.current;var keyboard=UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Keyboard>();
        try
        {
            var input=_player.GetComponent<PlayerInputHandler>();
            void Press(params UnityEngine.InputSystem.Key[] keys)
            {
                keyboard.MakeCurrent();UnityEngine.InputSystem.InputSystem.QueueStateEvent(keyboard,new UnityEngine.InputSystem.LowLevel.KeyboardState(keys));
                UnityEngine.InputSystem.InputSystem.Update();
                typeof(PlayerInputHandler).GetProperty("WasReloadPressed").SetValue(input,keys.Contains(UnityEngine.InputSystem.Key.R));
                typeof(PlayerInputHandler).GetProperty("WasRecoverPressed").SetValue(input,keys.Contains(UnityEngine.InputSystem.Key.F));
                typeof(PlayerSkillController).GetMethod("Update",Private).Invoke(_controller,null);
            }
            Press(UnityEngine.InputSystem.Key.R,UnityEngine.InputSystem.Key.F);Assert.That(_controller.PortalConsumesR,Is.True);
            Assert.That(portal.ShieldHealth,Is.EqualTo(26));Assert.That(portal.ShieldRemaining,Is.EqualTo(2.6f).Within(.001));
            Assert.That(portal.IsLinked,Is.True,"One frame cannot travel and recover the destination.");
            Assert.That(Vector2.Distance(_player.GetComponent<PlayerDirectionalColliders>().GroundPosition,portal.GetEndpoint(1).transform.position),Is.LessThan(.01));
            typeof(PlayerWeaponAmmo).GetMethod("Update",Private).Invoke(_player.GetComponent<PlayerWeaponAmmo>(),null);
            Assert.That(_player.GetComponent<PlayerWeaponAmmo>().IsReloading,Is.False);
            Assert.That(_player.GetComponent<PlayerTurretRepair>().CanRepair,Is.False);
            _clock.Now+=1;
            Press(UnityEngine.InputSystem.Key.R,UnityEngine.InputSystem.Key.LeftShift);
            Assert.That(portal.IsLinked,Is.True,"Shift+R must remain travel, never recovery.");
            Assert.That(Vector2.Distance(_player.GetComponent<PlayerDirectionalColliders>().GroundPosition,portal.GetEndpoint(0).transform.position),Is.LessThan(.01));
            Stand(portal.GetEndpoint(1).transform.position);
            Assert.That(_controller.RecoverablePortal,Is.SameAs(portal.GetEndpoint(1)));
            Press(UnityEngine.InputSystem.Key.F);Assert.That(_controller.PortalConsumesR,Is.False);
            Assert.That(portal.IsLinked,Is.False);Assert.That(portal.GetEndpoint(0),Is.Not.Null);Assert.That(portal.GetEndpoint(1),Is.Null);
        }
        finally{UnityEngine.InputSystem.InputSystem.RemoveDevice(keyboard);if(keyboardBefore!=null)keyboardBefore.MakeCurrent();Object.Destroy(tile);}
        yield break;
    }
    [UnityTest]
    public IEnumerator RecoveryPromptAndFChooseOnlyTheNearestOwnedPortalOrTurret()
    {
        Reward();
        foreach(var id in new[]{PlayerSkillId.PlayerTurret,PlayerSkillId.EnergyBarrier,PlayerSkillId.LureBeacon,PlayerSkillId.Portal})Assert.That(_research.TryUnlock(id),Is.True);
        _research.TrySetCandidate(1,PlayerSkillId.Portal);_research.TrySetCandidate(2,PlayerSkillId.PlayerTurret);
        Assert.That(_controller.TryEquip(0,PlayerSkillId.Portal),Is.True);Assert.That(_controller.TryEquip(1,PlayerSkillId.PlayerTurret),Is.True);
        var grid=new GameObject("Recovery ground",typeof(Grid));SceneManager.MoveGameObjectToScene(grid,_scene);grid.transform.position=_player.transform.position;
        var mapObject=new GameObject("Ground",typeof(UnityEngine.Tilemaps.Tilemap),typeof(UnityEngine.Tilemaps.TilemapCollider2D));mapObject.transform.SetParent(grid.transform,false);
        var map=mapObject.GetComponent<UnityEngine.Tilemaps.Tilemap>();var tile=ScriptableObject.CreateInstance<UnityEngine.Tilemaps.Tile>();tile.colliderType=UnityEngine.Tilemaps.Tile.ColliderType.None;
        var portal=_player.GetComponent<PlayerPortalSkill>();var turret=_player.GetComponent<PlayerTurretSkill>();
        foreach(var component in new Component[]{portal,turret}){var data=new SerializedObject(component);data.FindProperty("_placementGround").objectReferenceValue=map;data.ApplyModifiedPropertiesWithoutUndo();}
        for(int x=-5;x<25;x++)for(int y=-8;y<12;y++)map.SetTile(new Vector3Int(x,y,0),tile);
        Vector3 start=_player.transform.position;
        Assert.That(_controller.TryUseSlot(0),Is.True);Assert.That(portal.TryConfirmPlacement(start+new Vector3(6,0)),Is.True,portal.PlacementFailure);
        Assert.That(_controller.TryUseSlot(1),Is.True);Assert.That(turret.TrySelectSlot(0),Is.True);
        Assert.That(turret.TryConfirmPlacement(start+new Vector3(4,4)),Is.True,turret.PlacementFeedback.ToString());
        void Stand(Vector3 feet)
        {var ground=_player.GetComponent<PlayerDirectionalColliders>().GroundPosition;var p=_player.transform.position+feet-(Vector3)ground;_player.transform.position=p;_player.GetComponent<Rigidbody2D>().position=p;Physics2D.SyncTransforms();}
        Vector3? nearPortal=null,nearTurret=null;
        for(float x=2;x<=8;x+=.5f)for(float y=0;y<=6;y+=.5f)
        {
            var feet=start+new Vector3(x,y);Stand(feet);
            if(portal.NearestInteractable==null||turret.RecoverableTurret==null)continue;
            if(_controller.RecoverablePortal!=null)nearPortal=feet;
            if(_controller.RecoverableTurret!=null)nearTurret=feet;
        }
        Assert.That(nearPortal.HasValue,Is.True,"Overlapping ranges must allow the closer portal.");
        Assert.That(nearTurret.HasValue,Is.True,"Overlapping ranges must allow the closer turret.");
        Stand(nearTurret.Value);Assert.That(_controller.RecoverablePortal,Is.Null);
        var input=_player.GetComponent<PlayerInputHandler>();typeof(PlayerInputHandler).GetProperty("WasRecoverPressed").SetValue(input,true);
        typeof(PlayerSkillController).GetMethod("Update",Private).Invoke(_controller,null);
        Assert.That(turret.CurrentTurretCount,Is.EqualTo(3));Assert.That(portal.GetEndpoint(0),Is.Not.Null,"F must not also recover the portal.");
        Stand(nearPortal.Value);Assert.That(_controller.RecoverableTurret,Is.Null);Assert.That(_controller.RecoverablePortal,Is.Not.Null);
        typeof(PlayerSkillController).GetMethod("Update",Private).Invoke(_controller,null);
        Assert.That(portal.GetEndpoint(0),Is.Null);Assert.That(turret.CurrentTurretCount,Is.EqualTo(3));
        Object.Destroy(tile);yield break;
    }
    private sealed class Clock : IMatchTime
    {
        public double Now;public bool Paused;
        private readonly Guid _epoch=Guid.NewGuid();public bool IsAvailable=>true;public double ElapsedSeconds=>Now;
        public MatchTimeSnapshot Snapshot=>new(new MatchId("skill-tree-test"),_epoch,0,Now,Now,Paused?0:1,Paused,true);
    }
}
